using System.Globalization;
using System.Text.Json;
using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using APISacor.Services.Nlp;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Services;

public sealed class ChatbotCotizacion
{
    private static readonly TimeSpan Vencimiento = TimeSpan.FromHours(2);
    private static readonly TimeSpan PausaAsesor = TimeSpan.FromHours(12);

    private readonly SacorDbContext _db;
    private readonly MotorConversacion _motor;
    private readonly ILogger<ChatbotCotizacion> _logger;

    public ChatbotCotizacion(SacorDbContext db, MotorConversacion motor, ILogger<ChatbotCotizacion> logger)
    {
        _db = db;
        _motor = motor;
        _logger = logger;
    }

    public async Task<List<string>> ProcesarAsync(string telefono, string mensajeId, string? texto, CancellationToken ct)
    {
        var conversacion = await _db.ConversacionesWhatsApp.FirstOrDefaultAsync(c => c.Telefono == telefono, ct);
        if (conversacion is null)
        {
            conversacion = new ConversacionWhatsApp { Telefono = telefono, Paso = "inicio", ActualizadoUtc = DateTime.UtcNow };
            _db.ConversacionesWhatsApp.Add(conversacion);
        }
        else if (conversacion.UltimoMensajeId == mensajeId)
        {
            return new List<string>();
        }

        var ahora = DateTime.UtcNow;
        var inactivo = ahora - conversacion.ActualizadoUtc;
        if (conversacion.Paso == "asesor" && inactivo > PausaAsesor)
            Reiniciar(conversacion);
        else if (conversacion.Paso != "inicio" && conversacion.Paso != "asesor" && inactivo > Vencimiento)
            Reiniciar(conversacion);

        conversacion.UltimoMensajeId = mensajeId;
        conversacion.ActualizadoUtc = ahora;

        var respuestas = string.IsNullOrWhiteSpace(texto)
            ? new List<string> { "Por ahora solo puedo leer mensajes de texto. Escríbeme lo que necesitas, por favor." }
            : await ResponderAsync(conversacion, Limpiar(texto), ct);

        await _db.SaveChangesAsync(ct);
        return respuestas;
    }

    private async Task<List<string>> ResponderAsync(ConversacionWhatsApp c, string texto, CancellationToken ct)
    {
        var estado = new EstadoConversacion { Paso = c.Paso, Datos = LeerDatos(c) };
        var materiales = await MaterialesAsync(ct);
        var resultado = _motor.Procesar(estado, texto, materiales, ConsultasMovil.HoyLocal());

        _logger.LogInformation("Chatbot: intencion {Intencion} con confianza {Confianza}, paso {Paso}",
            string.IsNullOrEmpty(resultado.Intencion) ? "ninguna" : resultado.Intencion, resultado.Confianza, estado.Paso);

        if (resultado.Confirmada)
        {
            var id = await GuardarAsync(c.Telefono, estado.Datos, ct);
            Reiniciar(c);
            c.IdCotizacion = id;
            return new List<string>
            {
                $"¡Listo! Registramos tu solicitud con el número de cotización *#{id}*.\n\nUn asesor de SACOR la revisará y te contactará en menos de 24 horas hábiles. Gracias por escribirnos."
            };
        }

        c.Paso = estado.Paso;
        GuardarDatos(c, estado.Datos);
        return resultado.Mensajes;
    }

    private async Task<List<string>> MaterialesAsync(CancellationToken ct)
    {
        var nombresServicio = MotorConversacion.Servicios.Select(s => s.ToLowerInvariant()).ToHashSet();
        return (await _db.PreciosLugar.AsNoTracking().Select(p => p.Tipo).Distinct().ToListAsync(ct))
            .Where(t => !string.IsNullOrWhiteSpace(t) && !nombresServicio.Contains(t.ToLowerInvariant()))
            .OrderBy(t => t)
            .Take(15)
            .ToList();
    }

    private async Task<int> GuardarAsync(string telefonoWhatsApp, Dictionary<string, string> datos, CancellationToken ct)
    {
        var nombre = InputSanitizer.Limpiar(datos["nombre"], 150)!;
        var dui = datos["dui"];
        var telefono = FormatearTelefono(telefonoWhatsApp);
        var correo = InputSanitizer.Limpiar(datos.GetValueOrDefault("correo"), 254);
        var estrategia = _db.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var usuario = await _db.UsuariosWeb.FirstOrDefaultAsync(u => u.Dui == dui, ct);
            if (usuario is null)
            {
                usuario = new UsuarioWeb { Nombre = nombre, Dui = dui, Telefono = telefono, Email = correo };
                _db.UsuariosWeb.Add(usuario);
            }
            else
            {
                usuario.Nombre = nombre;
                usuario.Telefono = telefono;
                if (correo is not null) usuario.Email = correo;
            }
            await _db.SaveChangesAsync(ct);

            var cotizacion = new Cotizacion
            {
                IdUsuarioWeb = usuario.IdUsuarioWeb,
                Fecha = ConsultasMovil.HoyLocal(),
                Estado = "Pendiente",
                TipoServicio = datos["servicio"],
                Descripcion = Descripcion(datos)
            };
            _db.Cotizaciones.Add(cotizacion);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Cotizacion {Id} registrada desde WhatsApp", cotizacion.IdCotizacion);
            return cotizacion.IdCotizacion;
        });
    }

    private static string Descripcion(Dictionary<string, string> d)
    {
        var partes = new List<string?>();
        var recurrente = d["modalidad"] == "recurrente";
        partes.Add(recurrente ? "Modalidad: contrato recurrente" : "Modalidad: servicio puntual");
        partes.Add($"Recoleccion: {d["lugar"]}");
        if (d.TryGetValue("destino", out var destino)) partes.Add($"Destino: {destino}");
        if (d.TryGetValue("materiales", out var materiales)) partes.Add($"Materiales: {materiales}");
        if (recurrente)
        {
            var meses = int.Parse(d["duracion"], CultureInfo.InvariantCulture);
            partes.Add($"Duracion: {meses} {(meses == 1 ? "mes" : "meses")}");
            partes.Add($"Frecuencia: {d["frecuencia"]}");
            var viajes = EstimarViajes(meses, d["frecuencia"]);
            if (viajes is not null) partes.Add($"Viajes: {viajes}");
            if (d.TryGetValue("fecha", out var inicio)) partes.Add($"Inicio: {inicio}");
        }
        else
        {
            if (d.TryGetValue("fecha", out var fecha)) partes.Add($"Fecha estimada: {fecha}");
            if (d.TryGetValue("volumen", out var volumen)) partes.Add($"Volumen: {volumen}");
        }
        partes.Add("Origen: WhatsApp");
        if (d.TryGetValue("descripcion", out var detalle)) partes.Add(detalle);
        var texto = string.Join(" | ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return InputSanitizer.Limpiar(texto, 1000) ?? string.Empty;
    }

    private static int? EstimarViajes(int meses, string frecuencia) => frecuencia switch
    {
        "Diaria" => (int)Math.Round(meses * 365 / 12.0),
        "Semanal" => (int)Math.Round(meses * 52 / 12.0),
        "Quincenal" => meses * 2,
        "Mensual" => meses,
        _ => null
    };

    private static void Reiniciar(ConversacionWhatsApp c)
    {
        c.Paso = "inicio";
        c.Datos = null;
    }

    private static Dictionary<string, string> LeerDatos(ConversacionWhatsApp c)
    {
        if (string.IsNullOrWhiteSpace(c.Datos)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(c.Datos) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static void GuardarDatos(ConversacionWhatsApp c, Dictionary<string, string> datos)
    {
        var json = JsonSerializer.Serialize(datos);
        c.Datos = json.Length <= 2000 ? json : null;
    }

    private static string Limpiar(string texto)
    {
        var limpio = InputSanitizer.Limpiar(texto, 600) ?? string.Empty;
        return limpio.Replace('|', '/').Trim();
    }

    public static string FormatearTelefono(string telefonoWhatsApp)
    {
        var d = new string(telefonoWhatsApp.Where(char.IsDigit).ToArray());
        if (d.Length == 11 && d.StartsWith("503"))
            return $"+503 {d.Substring(3, 4)}-{d.Substring(7, 4)}";
        return ("+" + d).Length > 25 ? ("+" + d)[..25] : "+" + d;
    }
}
