using APISacor.Data;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace APISacor.Controllers;

// Consultas de solo lectura. El empleado se identifica por su propio Bearer,
// nunca por un id enviado por el celular y nunca por una API Key incrustada.
[ApiController]
[Route("api/movil/transportista")]
[Produces("application/json")]
public sealed class TransportistaController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;
    private static readonly Regex PatronEvidencia = new(
        @"^\[ENRUTA-EV\|(?<empleado>\d+)\|(?<fecha>\d{17})\] (?<texto>.+)$",
        RegexOptions.CultureInvariant
    );

    private sealed record EvidenciaRespuesta(
        int IdServicioContrato,
        string Destino,
        DateTime FechaUtc,
        string Texto
    );

    public TransportistaController(SacorDbContext db, ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    [HttpGet("inicio")]
    public async Task<IActionResult> Inicio(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new { mensaje = "No tienes permiso para consultar este módulo." });

        var id = empleado.IdEmpleado;
        Response.Headers.CacheControl = "no-store";

        // La asignación real se resuelve contra el empleado autenticado.
        var rutas = await (
            from asignacion in _db.RutasEmpleado.AsNoTracking()
            join ruta in _db.RutasTrabajo.AsNoTracking()
                on asignacion.IdRutaTrabajo equals ruta.IdRutaTrabajo
            where asignacion.IdEmpleado == id
            orderby asignacion.IdRutaEmpleado descending
            select new
            {
                asignacion.IdRutaEmpleado,
                ruta.IdRutaTrabajo,
                ruta.Destino,
                ruta.Tipo
            }).Take(30).ToListAsync(ct);

        var horarios = await _db.HorariosTrabajo.AsNoTracking()
            .Where(h => h.IdEmpleadoMarco == id)
            .OrderByDescending(h => h.Fecha)
            .ThenByDescending(h => h.IdHorarioTrabajo)
            .Take(20)
            .Select(h => new { h.IdHorarioTrabajo, h.Fecha, h.Entrada, h.Salida })
            .ToListAsync(ct);

        var camiones = await (
            from asignacion in _db.CamionConductores.AsNoTracking()
            join camion in _db.Camiones.AsNoTracking()
                on asignacion.IdCamion equals camion.IdCamion
            where asignacion.IdConductor == id
            orderby asignacion.Fecha descending, asignacion.IdCamionConductor descending
            select new
            {
                asignacion.IdCamionConductor,
                asignacion.Fecha,
                camion.IdCamion,
                camion.Placa,
                camion.Nombre,
                camion.Estado
            }).Take(20).ToListAsync(ct);

        var viajesExtra = await (
            from asignacion in _db.ViajeExtraEmpleados.AsNoTracking()
            join viaje in _db.ViajesExtra.AsNoTracking()
                on asignacion.IdViajeExtra equals viaje.IdViajeExtra
            where asignacion.IdEmpleado == id
            orderby viaje.IdViajeExtra descending
            select new
            {
                viaje.IdViajeExtra,
                viaje.IdCamion,
                viaje.Tipo,
                viaje.Cantidad
            }).Take(20).ToListAsync(ct);

        var limpiezas = await _db.Limpiezas.AsNoTracking()
            .Where(l => l.IdEmpleadoSubio == id)
            .OrderByDescending(l => l.Fecha)
            .ThenByDescending(l => l.IdLimpieza)
            .Take(20)
            .Select(l => new { l.IdLimpieza, l.IdServicioContrato, l.Fecha, l.Observacion, l.Etapa })
            .ToListAsync(ct);

        return Ok(new
        {
            empleado = ServicioAccesoMovil.Perfil(empleado),
            rutas,
            horarios,
            camiones,
            viajesExtra,
            limpiezas
        });
    }
    // GET /api/movil/transportista/servicios
    [HttpGet("servicios")]
    public async Task<IActionResult> Servicios(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);

        if (empleado is null)
            return Unauthorized(new
            {
                mensaje = "La sesión es inválida o ha vencido."
            });

        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        var servicios = await (
            from s in _db.ServiciosContrato.AsNoTracking()
            join ruta in _db.RutasTrabajo.AsNoTracking()
                on s.IdRutaTrabajo equals ruta.IdRutaTrabajo
            where _db.RutasEmpleado.Any(a =>
                a.IdRutaTrabajo == s.IdRutaTrabajo &&
                a.IdEmpleado == empleado.IdEmpleado)
            orderby s.IdServicioContrato descending
            select new
            {
                s.IdServicioContrato,
                s.IdRutaTrabajo,
                ruta.Destino,
                s.Fecha
            }
        ).Take(100).ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(servicios);
    }


    // GET /api/movil/transportista/evidencias
    [HttpGet("evidencias")]
    public async Task<IActionResult> Evidencias(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);

        if (empleado is null)
            return Unauthorized(new
            {
                mensaje = "La sesión es inválida o ha vencido."
            });

        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        var servicios = await (
            from s in _db.ServiciosContrato.AsNoTracking()
            join ruta in _db.RutasTrabajo.AsNoTracking()
                on s.IdRutaTrabajo equals ruta.IdRutaTrabajo
            where _db.RutasEmpleado.Any(a =>
                a.IdRutaTrabajo == s.IdRutaTrabajo &&
                a.IdEmpleado == empleado.IdEmpleado)
            orderby s.IdServicioContrato descending
            select new
            {
                s.IdServicioContrato,
                ruta.Destino,
                s.Observacion
            }
        ).Take(100).ToListAsync(ct);

        var evidencias = new List<EvidenciaRespuesta>();

        foreach (var servicio in servicios)
        {
            var lineas = (servicio.Observacion ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var linea in lineas)
            {
                var coincidencia = PatronEvidencia.Match(
                    linea.TrimEnd('\r')
                );

                if (!coincidencia.Success)
                    continue;

                if (!int.TryParse(
                        coincidencia.Groups["empleado"].Value,
                        out var autor))
                    continue;

                // Mostrar solo las evidencias del empleado autenticado.
                if (autor != empleado.IdEmpleado)
                    continue;

                var fechaTexto = coincidencia.Groups["fecha"].Value;

                if (!DateTime.TryParseExact(
                        fechaTexto,
                        "yyyyMMddHHmmssfff",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal |
                        DateTimeStyles.AdjustToUniversal,
                        out var fechaUtc))
                    continue;

                evidencias.Add(new EvidenciaRespuesta(
                    servicio.IdServicioContrato,
                    servicio.Destino,
                    fechaUtc,
                    coincidencia.Groups["texto"].Value
                ));
            }
        }

        Response.Headers.CacheControl = "no-store";

        return Ok(
            evidencias.OrderByDescending(e => e.FechaUtc)
        );
    }


    // POST /api/movil/transportista/evidencias
    [HttpPost("evidencias")]
    public async Task<IActionResult> GuardarEvidencia(
        [FromBody] GuardarEvidenciaTransportista peticion,
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);

        if (empleado is null)
            return Unauthorized(new
            {
                mensaje = "La sesión es inválida o ha vencido."
            });

        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        var texto = (peticion.Texto ?? "")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(texto))
            return BadRequest(new
            {
                mensaje = "Debes escribir la evidencia."
            });

        if (texto.Length > 500)
            return BadRequest(new
            {
                mensaje = "La evidencia no puede superar 500 caracteres."
            });

        var fechaUtc = DateTime.UtcNow;

        // La marca permite distinguir las evidencias de
        // las observaciones normales y reconocer a su autor.
        var marca = fechaUtc.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture
        );

        var linea =
            $"[ENRUTA-EV|{empleado.IdEmpleado}|{marca}] {texto}";

        // Reintentar si otro usuario modificó la observación
        // mientras estábamos preparando el guardado.
        for (int intento = 0; intento < 3; intento++)
        {
            var servicio = await (
                from s in _db.ServiciosContrato.AsNoTracking()
                join ruta in _db.RutasTrabajo.AsNoTracking()
                    on s.IdRutaTrabajo equals ruta.IdRutaTrabajo
                where s.IdServicioContrato ==
                    peticion.IdServicioContrato
                where _db.RutasEmpleado.Any(a =>
                    a.IdRutaTrabajo == s.IdRutaTrabajo &&
                    a.IdEmpleado == empleado.IdEmpleado)
                select new
                {
                    s.Observacion,
                    ruta.Destino
                }
            ).FirstOrDefaultAsync(ct);

            if (servicio is null)
                return NotFound(new
                {
                    mensaje = "El servicio no está asignado a este transportista."
                });

            var anterior = servicio.Observacion ?? "";

            var separador = anterior.Length == 0 ||
                            anterior.EndsWith('\n')
                ? ""
                : "\n";

            var nuevo = anterior + separador + linea;

            // No exceder el tamaño del campo existente.
            if (nuevo.Length > 1000)
                return Conflict(new
                {
                    mensaje = "Las observaciones de este servicio ya no tienen espacio suficiente para otra evidencia."
                });

            // Actualizar solo si la observación sigue siendo
            // igual a la que acabamos de consultar.
            var actualizados = await _db.ServiciosContrato
                .Where(s =>
                    s.IdServicioContrato ==
                        peticion.IdServicioContrato &&
                    s.Observacion == servicio.Observacion &&
                    _db.RutasEmpleado.Any(a =>
                        a.IdRutaTrabajo == s.IdRutaTrabajo &&
                        a.IdEmpleado == empleado.IdEmpleado))
                .ExecuteUpdateAsync(
                    cambios => cambios.SetProperty(
                        s => s.Observacion,
                        nuevo
                    ),
                    ct
                );

            if (actualizados == 1)
            {
                Response.Headers.CacheControl = "no-store";

                return Ok(new
                {
                    idServicioContrato = peticion.IdServicioContrato,
                    destino = servicio.Destino,
                    fechaUtc,
                    texto,
                    mensaje = "Evidencia guardada correctamente."
                });
            }
        }

        return Conflict(new
        {
            mensaje = "El servicio fue modificado simultáneamente. Intenta guardar nuevamente."
        });
    }
    public sealed class GuardarEvidenciaTransportista
    {
        [Range(1, int.MaxValue)]
        public int IdServicioContrato { get; set; }

        [Required]
        [StringLength(500)]
        public string Texto { get; set; } = string.Empty;
    }
}
