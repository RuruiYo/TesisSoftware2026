using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using APISacor.Data;
using APISacor.Models;
using APISacor.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// Convierte una cotizacion web aprobada en un contrato con sus viajes (ruta_trabajo).
// SEGURIDAD: esta bajo /api/cotizaciones, fuera de /api/movil, asi que exige la API Key
// de la app de escritorio igual que el resto del CRUD.
[ApiController]
[Route("api/cotizaciones")]
[Produces("application/json")]
public sealed class CotizacionViajesController : ControllerBase
{
    private static readonly string[] Frecuencias = { "Unica", "Diaria", "Semanal", "Quincenal", "Mensual" };

    // SEGURIDAD: tope de viajes por cotizacion. Evita que una peticion cree miles de
    // filas de un solo golpe (DoS sobre la base) por un error de captura.
    private const int MaxViajes = 200;

    private readonly SacorDbContext _db;
    private readonly ILogger<CotizacionViajesController> _logger;

    public CotizacionViajesController(SacorDbContext db, ILogger<CotizacionViajesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public sealed class GenerarViajesPeticion
    {
        [Range(1, int.MaxValue)]
        public int IdAdministrador { get; set; }

        [Range(1, MaxViajes)]
        public int Cantidad { get; set; }

        public DateTime FechaInicio { get; set; }

        [Required]
        [StringLength(20)]
        public string Frecuencia { get; set; } = string.Empty;

        [Range(0, 9999999999.99)]
        public decimal Precio { get; set; }

        [StringLength(120)]
        public string? Lugar { get; set; }

        [StringLength(70)]
        public string? Destino { get; set; }
    }

    // POST /api/cotizaciones/{id}/generar-viajes
    [HttpPost("{id:int}/generar-viajes")]
    public async Task<IActionResult> Generar(int id, [FromBody] GenerarViajesPeticion peticion, CancellationToken ct)
    {
        // SEGURIDAD: lista blanca de frecuencias; cualquier otro texto se rechaza.
        var frecuencia = Frecuencias.FirstOrDefault(f => f.Equals(peticion.Frecuencia?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (frecuencia is null)
            return BadRequest(new { mensaje = $"La frecuencia debe ser una de: {string.Join(", ", Frecuencias)}." });

        if (peticion.FechaInicio.Date < DateTime.Today.AddYears(-1) || peticion.FechaInicio.Date > DateTime.Today.AddYears(3))
            return BadRequest(new { mensaje = "La fecha de inicio no es válida." });

        if (decimal.Round(peticion.Precio, 2) != peticion.Precio)
            return BadRequest(new { mensaje = "El precio admite máximo dos decimales." });

        // SEGURIDAD: el administrador debe existir, estar activo y ser de tipo Administrador.
        // Asi nadie con la llave puede atribuir los viajes a un empleado cualquiera.
        var adminValido = await _db.Empleados.AsNoTracking().AnyAsync(e =>
            e.IdEmpleado == peticion.IdAdministrador && e.Estado == "Activo" && e.Tipo == TiposEmpleado.AdministradorOperativo, ct);
        if (!adminValido)
            return BadRequest(new { mensaje = "El administrador indicado no es válido." });

        var cotizacion = await _db.Cotizaciones.AsNoTracking().FirstOrDefaultAsync(c => c.IdCotizacion == id, ct);
        if (cotizacion is null)
            return NotFound(new { mensaje = "La cotización no existe." });

        if (!cotizacion.Estado.Equals("Aprobada", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { mensaje = "Solo se pueden generar viajes de una cotización aprobada." });

        if (await _db.ServiciosContrato.AnyAsync(s => s.IdCotizacion == id, ct))
            return Conflict(new { mensaje = "Esta cotización ya tiene su contrato y viajes." });

        var solicitante = await _db.UsuariosWeb.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuarioWeb == cotizacion.IdUsuarioWeb, ct);
        if (solicitante is null)
            return Conflict(new { mensaje = "La cotización no tiene un solicitante válido." });

        var detalles = LeerDetalles(cotizacion.Descripcion);
        var tipoServicio = Recortar(string.IsNullOrWhiteSpace(cotizacion.TipoServicio) ? "Servicio" : cotizacion.TipoServicio.Trim(), 60);
        var lugar = Recortar(Limpio(peticion.Lugar) ?? detalles.GetValueOrDefault("recoleccion") ?? "Sin lugar", 120);
        var destino = Limpio(peticion.Destino) ?? detalles.GetValueOrDefault("destino");
        var textoRuta = destino is null || destino.Equals(lugar, StringComparison.OrdinalIgnoreCase)
            ? lugar
            : $"{lugar} → {Recortar(destino, 70)}";

        var fechas = CalcularFechas(peticion.FechaInicio.Date, peticion.Cantidad, frecuencia);

        var estrategia = _db.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync<IActionResult>(async () =>
        {
            // SEGURIDAD: todo en una transaccion. Si algo falla a mitad, no quedan
            // clientes, precios o viajes huerfanos a medio crear.
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

            var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Dui == solicitante.Dui, ct);
            var clienteNuevo = cliente is null;
            if (cliente is null)
            {
                cliente = new Cliente
                {
                    Nombre = Recortar(solicitante.Nombre, 150),
                    Tipo = "Natural",
                    Dui = solicitante.Dui,
                    Telefono = solicitante.Telefono,
                    Correo = solicitante.Email,
                    IdAdministradorRegistro = peticion.IdAdministrador
                };
                _db.Clientes.Add(cliente);
            }

            var precio = await _db.PreciosLugar.FirstOrDefaultAsync(p => p.Tipo == tipoServicio && p.Lugar == lugar, ct);
            var precioExistente = precio is not null;
            if (precio is null)
            {
                precio = new PrecioLugar
                {
                    Tipo = tipoServicio,
                    Lugar = lugar,
                    Precio = peticion.Precio,
                    FechaModificacion = DateTime.Today
                };
                _db.PreciosLugar.Add(precio);
            }

            var viajes = fechas.Select((fecha, i) => new RutaTrabajo
            {
                IdAdministrador = peticion.IdAdministrador,
                Destino = Recortar($"[{fecha:yyyy-MM-dd}] {textoRuta}", 200),
                Tipo = Recortar($"{tipoServicio} {i + 1}/{fechas.Count}", 60)
            }).ToList();

            _db.RutasTrabajo.AddRange(viajes);
            await _db.SaveChangesAsync(ct);

            var contrato = new ServicioContrato
            {
                IdRutaTrabajo = viajes[0].IdRutaTrabajo,
                IdCliente = cliente.IdCliente,
                IdPrecioLugar = precio.IdPrecioLugar,
                Fecha = fechas[0],
                TipoTransportaje = Recortar(tipoServicio, 80),
                IdCotizacion = id,
                Observacion = $"Cotización web #{id}: {fechas.Count} viaje(s), frecuencia {frecuencia.ToLowerInvariant()}."
            };
            _db.ServiciosContrato.Add(contrato);
            await _db.SaveChangesAsync(ct);

            // Cada viaje queda ligado a su contrato: un contrato tiene varios viajes
            // y cada viaje tiene su propio personal en ruta_empleado.
            foreach (var viaje in viajes)
                viaje.IdServicioContrato = contrato.IdServicioContrato;

            var resumen = new
            {
                idCotizacion = id,
                cliente.IdCliente,
                precio.IdPrecioLugar,
                viajes = viajes.Select(v => v.IdRutaTrabajo).ToList(),
                frecuencia,
                fechaInicio = fechas[0].ToString("yyyy-MM-dd"),
            };

            // SEGURIDAD: la generacion queda en la auditoria con el administrador que la hizo.
            _db.Auditorias.Add(new Auditoria
            {
                IdEmpleado = peticion.IdAdministrador,
                TablaAfectada = "ruta_trabajo",
                IdRegistro = $"cotizacion-{id}",
                Accion = "INSERT",
                FechaHora = DateTime.Now,
                ValorNuevo = JsonSerializer.Serialize(resumen),
                Detalle = $"SACOR Desktop: {fechas.Count} viaje(s) generados desde la cotización #{id}"
            });

            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            _logger.LogInformation("Cotizacion {Id}: {Cantidad} viajes generados por el empleado {Admin}",
                id, fechas.Count, peticion.IdAdministrador);

            return Ok(new
            {
                idCotizacion = id,
                idCliente = cliente.IdCliente,
                clienteNuevo,
                idPrecioLugar = precio.IdPrecioLugar,
                precioExistente,
                precioUsado = precio.Precio,
                idServicioContrato = contrato.IdServicioContrato,
                viajes = viajes.Select(v => new { v.IdRutaTrabajo, v.Destino, v.Tipo }),
                mensaje = $"Se generaron {fechas.Count} viaje(s)."
            });
        });
    }

    private static List<DateTime> CalcularFechas(DateTime inicio, int cantidad, string frecuencia) =>
        Enumerable.Range(0, cantidad).Select(i => frecuencia switch
        {
            "Diaria" => inicio.AddDays(i),
            "Semanal" => inicio.AddDays(7 * i),
            "Quincenal" => inicio.AddDays(14 * i),
            "Mensual" => inicio.AddMonths(i),
            _ => inicio
        }).ToList();

    // La web guarda los detalles como "Clave: valor | Clave: valor".
    private static Dictionary<string, string> LeerDetalles(string? descripcion)
    {
        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parte in (descripcion ?? string.Empty).Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var dosPuntos = parte.IndexOf(':');
            if (dosPuntos <= 0) continue;
            var clave = parte[..dosPuntos].Trim().ToLowerInvariant();
            var valor = parte[(dosPuntos + 1)..].Trim();
            if (valor.Length > 0) resultado.TryAdd(clave, valor);
        }
        return resultado;
    }

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}
