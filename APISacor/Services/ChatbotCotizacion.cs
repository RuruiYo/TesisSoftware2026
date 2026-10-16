using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Services;

public sealed class ChatbotCotizacion
{
    private static readonly string[] Servicios = { "Acarreo de materiales", "Recoleccion de desechos", "Reciclaje", "Especifico" };
    private static readonly string[] ServiciosVisibles = { "Acarreo de materiales", "Recolección de desechos", "Reciclaje", "Específico" };
    private static readonly HashSet<string> ConDestino = new() { "Acarreo de materiales", "Especifico" };
    private static readonly string[] Frecuencias = { "Diaria", "Semanal", "Quincenal", "Mensual", "A demanda" };
    private static readonly TimeSpan Vencimiento = TimeSpan.FromHours(2);
    private static readonly TimeSpan PausaAsesor = TimeSpan.FromHours(12);

    private const string Omitir = "Escribe *omitir* si no aplica.";

    private static readonly string[] OpcionesMenu = { "Solicitar una cotización", "Hablar con un asesor" };
    private static readonly string[][] ClavesMenu = { new[] { "cotiz", "solicit", "precio", "presupuesto" }, new[] { "asesor", "persona", "humano", "agente", "hablar" } };
    private static readonly string[][] ClavesServicio = { new[] { "acarre", "traslado", "transporte" }, new[] { "recolec", "desecho", "basura", "residuo" }, new[] { "recicl" }, new[] { "especific", "otro" } };
    private static readonly string[] OpcionesModalidad = { "Servicio puntual", "Contrato recurrente" };
    private static readonly string[][] ClavesModalidad = { new[] { "puntual", "una vez", "unica", "solo una" }, new[] { "contrato", "recurrente", "periodic", "varias veces" } };
    private static readonly string[][] ClavesFrecuencia = { new[] { "diari", "todos los dias" }, new[] { "semanal", "cada semana" }, new[] { "quincenal", "cada quince", "cada 15" }, new[] { "mensual", "cada mes" }, new[] { "demanda", "cuando" } };
    private static readonly string[] OpcionesConfirmar = { "Sí, enviar solicitud", "No, cancelar" };
    private static readonly string[][] ClavesConfirmar = { new[] { "si", "enviar", "correcto", "ok", "confirm", "dale" }, new[] { "no" } };

    private readonly SacorDbContext _db;
    private readonly ILogger<ChatbotCotizacion> _logger;

    public ChatbotCotizacion(SacorDbContext db, ILogger<ChatbotCotizacion> logger)
    {
        _db = db;
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

        var respuestas = texto is null
            ? new List<string> { "Por ahora solo puedo leer mensajes de texto. Escribe tu respuesta, por favor." }
            : await ResponderAsync(conversacion, Limpiar(texto), ct);

        await _db.SaveChangesAsync(ct);
        return respuestas;
    }

    private async Task<List<string>> ResponderAsync(ConversacionWhatsApp c, string texto, CancellationToken ct)
    {
        var datos = LeerDatos(c);
        var comando = Normalizar(texto);

        if (comando is "cancelar" or "salir")
        {
            Reiniciar(c);
            return new() { "Listo, cancelé la solicitud. Escribe cualquier mensaje cuando quieras empezar de nuevo." };
        }

        if (c.Paso == "asesor")
        {
            if (comando is "menu" or "menú" or "inicio")
            {
                c.Paso = "menu";
                return new() { Menu() };
            }
            return new();
        }

        switch (c.Paso)
        {
            case "inicio":
                c.Paso = "menu";
                return new() { "Hola, soy el asistente virtual de *SACOR, Soluciones Ambientales Corporativas*.\n\n" + Menu() };

            case "menu":
                var opcionMenu = Elegir(texto, OpcionesMenu, ClavesMenu);
                if (opcionMenu == 0)
                    return Avanzar(c, datos, "nombre", "Perfecto, vamos a armar tu solicitud de cotización. Puedes escribir *cancelar* en cualquier momento.\n\n¿Cuál es tu *nombre completo* o el nombre de tu empresa o institución?");
                if (opcionMenu == 1)
                {
                    c.Paso = "asesor";
                    return new() { "Un asesor de SACOR te responderá en este chat en horario de oficina, de lunes a viernes de 8:00 a 17:00.\n\nSi luego quieres usar el asistente, escribe *menu*." };
                }
                return new() { "No entendí tu respuesta. Escribe el número o el nombre de una opción.\n\n" + Menu() };

            case "nombre":
                if (texto.Length < 3 || texto.Length > 150)
                    return new() { "Escribe tu nombre completo, por favor (entre 3 y 150 caracteres)." };
                datos["nombre"] = texto;
                return Avanzar(c, datos, "dui", "Gracias. Ahora escribe tu *DUI* con el formato 00000000-0.");

            case "dui":
                var dui = FormatearDui(texto);
                if (dui is null)
                    return new() { "El DUI no es válido. Escríbelo con el formato 00000000-0, por ejemplo 01234567-8." };
                datos["dui"] = dui;
                return Avanzar(c, datos, "correo", "¿Cuál es tu *correo electrónico*? " + Omitir);

            case "correo":
                if (comando != "omitir")
                {
                    if (texto.Length > 254 || !new EmailAddressAttribute().IsValid(texto) || !texto.Contains('.'))
                        return new() { "Ese correo no parece válido. Escríbelo de nuevo o escribe *omitir*." };
                    datos["correo"] = texto;
                }
                return Avanzar(c, datos, "servicio", "¿Qué *tipo de servicio* necesitas?\n\n" + Lista(ServiciosVisibles));

            case "servicio":
                var indiceServicio = Elegir(texto, ServiciosVisibles, ClavesServicio);
                if (indiceServicio < 0)
                    return new() { "No entendí el servicio. Escribe el número o el nombre.\n\n" + Lista(ServiciosVisibles) };
                datos["servicio"] = Servicios[indiceServicio];
                return Avanzar(c, datos, "modalidad", "¿Qué *modalidad* buscas?\n\n1. Servicio puntual (una sola vez)\n2. Contrato recurrente");

            case "modalidad":
                var indiceModalidad = Elegir(texto, OpcionesModalidad, ClavesModalidad);
                if (indiceModalidad < 0)
                    return new() { "Responde *1* o *puntual* para un servicio de una sola vez, o *2* o *contrato* para un contrato recurrente." };
                datos["modalidad"] = indiceModalidad == 0 ? "puntual" : "recurrente";
                return Avanzar(c, datos, "lugar", "¿Cuál es el *lugar de recolección*? Escribe la dirección o el municipio.");

            case "lugar":
                if (texto.Length < 3 || texto.Length > 120)
                    return new() { "Escribe la dirección o el municipio de recolección (entre 3 y 120 caracteres)." };
                datos["lugar"] = texto;
                if (ConDestino.Contains(datos["servicio"]))
                    return Avanzar(c, datos, "destino", "¿Cuál es el *destino*? Escribe la dirección o el municipio a donde se llevará el material.");
                return await PreguntarMaterialesAsync(c, datos, ct);

            case "destino":
                if (texto.Length < 3 || texto.Length > 120)
                    return new() { "Escribe la dirección o el municipio de destino (entre 3 y 120 caracteres)." };
                datos["destino"] = texto;
                return await PreguntarMaterialesAsync(c, datos, ct);

            case "materiales":
                if (comando != "omitir")
                {
                    var lista = (datos.GetValueOrDefault("lista_materiales") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries);
                    var elegidos = new List<string>();
                    foreach (var parte in PartesLista(texto))
                    {
                        var indice = ElegirMaterial(parte, lista);
                        if (indice < 0)
                            return new() { $"No reconocí \"{parte}\". Escribe los números o los nombres de los materiales separados por coma, por ejemplo *1,3* o *ripio, tierra*. " + Omitir };
                        if (!elegidos.Contains(lista[indice])) elegidos.Add(lista[indice]);
                    }
                    if (elegidos.Count > 0) datos["materiales"] = string.Join(", ", elegidos);
                }
                return PreguntarFechas(c, datos);

            case "duracion":
                if (!int.TryParse(comando, out var meses) || meses < 1 || meses > 36)
                    return new() { "Escribe la duración en meses con un número del 1 al 36." };
                datos["duracion"] = meses.ToString(CultureInfo.InvariantCulture);
                return Avanzar(c, datos, "frecuencia", "¿Con qué *frecuencia* necesitas los viajes?\n\n" + Lista(Frecuencias));

            case "frecuencia":
                var indiceFrecuencia = Elegir(texto, Frecuencias, ClavesFrecuencia);
                if (indiceFrecuencia < 0)
                    return new() { "No entendí la frecuencia. Escribe el número o el nombre.\n\n" + Lista(Frecuencias) };
                datos["frecuencia"] = Frecuencias[indiceFrecuencia];
                return Avanzar(c, datos, "fecha", "¿Desde qué *fecha* quieres iniciar? Escríbela como dd/mm/aaaa. " + Omitir);

            case "fecha":
                if (comando != "omitir")
                {
                    if (!DateTime.TryParseExact(texto, new[] { "d/M/yyyy", "d-M-yyyy", "d/M/yy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                        return new() { "No entendí la fecha. Escríbela como dd/mm/aaaa, por ejemplo 25/11/2026, o escribe *omitir*." };
                    var hoy = ConsultasMovil.HoyLocal();
                    if (fecha.Date < hoy || fecha.Date > hoy.AddYears(2))
                        return new() { "La fecha debe ser de hoy en adelante y dentro de los próximos dos años." };
                    datos["fecha"] = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                if (datos["modalidad"] == "puntual")
                    return Avanzar(c, datos, "volumen", "¿Cuál es el *volumen aproximado*? Por ejemplo: 2 camiones, 10 toneladas o 5 m³. " + Omitir);
                return Avanzar(c, datos, "descripcion", PreguntaDescripcion());

            case "volumen":
                if (comando != "omitir")
                {
                    if (texto.Length > 60)
                        return new() { "Resume el volumen en menos de 60 caracteres, por favor." };
                    datos["volumen"] = texto;
                }
                return Avanzar(c, datos, "descripcion", PreguntaDescripcion());

            case "descripcion":
                if (comando != "omitir")
                {
                    if (texto.Length > 500)
                        return new() { "Resume los detalles en menos de 500 caracteres, por favor." };
                    datos["descripcion"] = texto;
                }
                return Avanzar(c, datos, "confirmar", Resumen(datos) + "\n\n¿Los datos son correctos?\n1. Sí, enviar solicitud\n2. No, cancelar");

            case "confirmar":
                var confirmacion = Elegir(texto, OpcionesConfirmar, ClavesConfirmar);
                if (confirmacion == 1)
                {
                    Reiniciar(c);
                    return new() { "Listo, no enviamos nada. Escribe cualquier mensaje cuando quieras empezar de nuevo." };
                }
                if (confirmacion != 0)
                    return new() { "Responde *1* o *sí* para enviar la solicitud, o *2* o *no* para cancelar." };
                var id = await GuardarAsync(c.Telefono, datos, ct);
                Reiniciar(c);
                c.IdCotizacion = id;
                return new() { $"Listo, registramos tu solicitud con el número de cotización *#{id}*.\n\nUn asesor de SACOR la revisará y te contactará en menos de 24 horas hábiles. Gracias por escribirnos." };

            default:
                Reiniciar(c);
                c.Paso = "menu";
                return new() { Menu() };
        }
    }

    private async Task<List<string>> PreguntarMaterialesAsync(ConversacionWhatsApp c, Dictionary<string, string> datos, CancellationToken ct)
    {
        var nombresServicio = Servicios.Select(s => s.ToLowerInvariant()).ToHashSet();
        var tipos = (await _db.PreciosLugar.AsNoTracking().Select(p => p.Tipo).Distinct().ToListAsync(ct))
            .Where(t => !nombresServicio.Contains(t.ToLowerInvariant()))
            .OrderBy(t => t)
            .Take(15)
            .ToList();
        if (tipos.Count == 0)
            return PreguntarFechas(c, datos);
        datos["lista_materiales"] = string.Join('|', tipos);
        return Avanzar(c, datos, "materiales", "¿Qué *tipos de material* se moverán? Responde con los números o los nombres separados por coma, por ejemplo 1,3.\n\n" + Lista(tipos) + "\n\n" + Omitir);
    }

    private static List<string> PreguntarFechas(ConversacionWhatsApp c, Dictionary<string, string> datos)
    {
        if (datos["modalidad"] == "recurrente")
            return Avanzar(c, datos, "duracion", "¿Cuántos *meses* durará el contrato? Escribe un número, por ejemplo 6.");
        return Avanzar(c, datos, "fecha", "¿Para qué *fecha* necesitas el servicio? Escríbela como dd/mm/aaaa. " + Omitir);
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

    private static string Resumen(Dictionary<string, string> d)
    {
        var sb = new StringBuilder("*Resumen de tu solicitud*\n\n");
        sb.Append($"Nombre: {d["nombre"]}\nDUI: {d["dui"]}\n");
        if (d.TryGetValue("correo", out var correo)) sb.Append($"Correo: {correo}\n");
        sb.Append($"Servicio: {ServiciosVisibles[Array.IndexOf(Servicios, d["servicio"])]}\n");
        sb.Append($"Modalidad: {(d["modalidad"] == "recurrente" ? "Contrato recurrente" : "Servicio puntual")}\n");
        sb.Append($"Recolección: {d["lugar"]}\n");
        if (d.TryGetValue("destino", out var destino)) sb.Append($"Destino: {destino}\n");
        if (d.TryGetValue("materiales", out var materiales)) sb.Append($"Materiales: {materiales}\n");
        if (d.TryGetValue("duracion", out var duracion)) sb.Append($"Duración: {duracion} mes(es)\n");
        if (d.TryGetValue("frecuencia", out var frecuencia)) sb.Append($"Frecuencia: {frecuencia}\n");
        if (d.TryGetValue("fecha", out var fecha))
            sb.Append($"Fecha: {DateTime.ParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture):dd/MM/yyyy}\n");
        if (d.TryGetValue("volumen", out var volumen)) sb.Append($"Volumen: {volumen}\n");
        if (d.TryGetValue("descripcion", out var detalle)) sb.Append($"Detalles: {detalle}\n");
        return sb.ToString().TrimEnd();
    }

    private static string Menu() =>
        "¿En qué te puedo ayudar? Responde con el número o el nombre de una opción:\n\n1. Solicitar una cotización\n2. Hablar con un asesor";

    private static string PreguntaDescripcion() =>
        "Por último, ¿quieres agregar algún *detalle*? Por ejemplo horarios de acceso o si se necesita carga y descarga. " + Omitir;

    private static List<string> Avanzar(ConversacionWhatsApp c, Dictionary<string, string> datos, string paso, string pregunta)
    {
        c.Paso = paso;
        GuardarDatos(c, datos);
        return new List<string> { pregunta };
    }

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

    private static string Lista(IEnumerable<string> opciones) =>
        string.Join("\n", opciones.Select((o, i) => $"{i + 1}. {o}"));

    private static int Elegir(string texto, IReadOnlyList<string> opciones, string[][] claves)
    {
        var t = SinAcentos(texto).Trim('.', '!', '*', ',', ' ');
        if (int.TryParse(t, out var n))
            return n >= 1 && n <= opciones.Count ? n - 1 : -1;
        if (t.Length < 2)
            return -1;

        var coincidencias = new List<int>();
        for (var i = 0; i < opciones.Count; i++)
        {
            var opcion = SinAcentos(opciones[i]);
            var porClave = i < claves.Length && claves[i].Any(k => Regex.IsMatch(t, $@"\b{Regex.Escape(k)}", RegexOptions.None, TimeSpan.FromMilliseconds(50)));
            if (t == opcion || (t.Length >= 4 && opcion.Contains(t)) || t.Contains(opcion) || porClave)
                coincidencias.Add(i);
        }
        return coincidencias.Count == 1 ? coincidencias[0] : -1;
    }

    private static int ElegirMaterial(string parte, string[] lista)
    {
        var t = SinAcentos(parte);
        if (int.TryParse(t, out var n))
            return n >= 1 && n <= lista.Length ? n - 1 : -1;
        if (t.Length < 3)
            return -1;
        var coincidencias = Enumerable.Range(0, lista.Length)
            .Where(i => SinAcentos(lista[i]) == t || SinAcentos(lista[i]).Contains(t) || t.Contains(SinAcentos(lista[i])))
            .ToList();
        return coincidencias.Count == 1 ? coincidencias[0] : -1;
    }

    private static IEnumerable<string> PartesLista(string texto) =>
        Regex.Split(texto.Trim(), @"\s*(?:,|;|\s+y\s+|\s+e\s+)\s*|\s+(?=\d)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50))
            .Select(p => p.Trim('.', '*', ' '))
            .Where(p => p.Length > 0);

    private static string SinAcentos(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var ch in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Trim();
    }

    private static string Limpiar(string texto)
    {
        var limpio = InputSanitizer.Limpiar(texto, 600) ?? string.Empty;
        return limpio.Replace('|', '/').Trim();
    }

    private static string Normalizar(string texto) =>
        texto.Trim().Trim('.', '!', '*').ToLowerInvariant();

    private static string? FormatearDui(string texto)
    {
        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (digitos.Length != 9) return null;
        var dui = $"{digitos[..8]}-{digitos[8]}";
        return InputSanitizer.DuiValido(dui) ? dui : null;
    }

    public static string FormatearTelefono(string telefonoWhatsApp)
    {
        var d = new string(telefonoWhatsApp.Where(char.IsDigit).ToArray());
        if (d.Length == 11 && d.StartsWith("503"))
            return $"+503 {d.Substring(3, 4)}-{d.Substring(7, 4)}";
        return ("+" + d).Length > 25 ? ("+" + d)[..25] : "+" + d;
    }
}
