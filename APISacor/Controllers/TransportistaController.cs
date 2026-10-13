using APISacor.Data;
using APISacor.Models;
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
            from s in ConsultasMovil.ServiciosDelEmpleado(_db, empleado.IdEmpleado).AsNoTracking()
            join ruta in _db.RutasTrabajo.AsNoTracking()
                on s.IdRutaTrabajo equals ruta.IdRutaTrabajo
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
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new { mensaje = "No tienes permiso." });

        // SEGURIDAD: solo se devuelven las evidencias que subio el empleado autenticado.
        var registros = await (
            from l in _db.Limpiezas.AsNoTracking()
            join r in _db.RutasTrabajo.AsNoTracking() on l.IdRutaTrabajo equals (int?)r.IdRutaTrabajo
            where l.IdEmpleadoSubio == empleado.IdEmpleado &&
                  (l.Etapa == EtapasEvidencia.Limpieza || l.Etapa == EtapasEvidencia.Ruta)
            orderby l.IdLimpieza descending
            select new { l.IdLimpieza, r.IdRutaTrabajo, r.Destino, l.Fecha, l.Etapa, l.Observacion }
        ).Take(100).ToListAsync(ct);

        var ids = registros.Select(r => r.IdLimpieza).ToList();
        var fotos = await _db.EvidenciasFoto.AsNoTracking()
            .Where(f => ids.Contains(f.IdLimpieza))
            .OrderBy(f => f.IdEvidenciaFoto)
            .Select(f => new { f.IdLimpieza, f.IdEvidenciaFoto })
            .ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(registros.Select(r =>
        {
            var (fechaViaje, destino) = ConsultasMovil.LeerDestino(r.Destino);
            return new
            {
                r.IdLimpieza,
                r.IdRutaTrabajo,
                destino,
                fechaViaje = fechaViaje?.ToString("yyyy-MM-dd"),
                r.Fecha,
                tipo = r.Etapa == EtapasEvidencia.Ruta ? "ruta" : "limpieza",
                texto = r.Observacion ?? string.Empty,
                fotos = fotos.Where(f => f.IdLimpieza == r.IdLimpieza).Select(f => f.IdEvidenciaFoto).ToList()
            };
        }));
    }

    // POST /api/movil/transportista/evidencias
    [HttpPost("evidencias")]
    public async Task<IActionResult> GuardarEvidencia(
        [FromBody] GuardarEvidenciaTransportista peticion,
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new { mensaje = "No tienes permiso." });

        // SEGURIDAD: lista blanca del tipo de evidencia; el texto libre del cliente
        // nunca llega directo a la columna etapa.
        var etapa = (peticion.Tipo ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "limpieza" => EtapasEvidencia.Limpieza,
            "ruta" => EtapasEvidencia.Ruta,
            _ => null
        };
        if (etapa is null)
            return BadRequest(new { mensaje = "El tipo de evidencia debe ser limpieza o ruta." });

        var texto = (peticion.Texto ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return BadRequest(new { mensaje = "Debes escribir la evidencia." });
        if (texto.Length > 500)
            return BadRequest(new { mensaje = "La evidencia no puede superar 500 caracteres." });

        if (!await ConsultasMovil.EstaAsignadoAsync(_db, peticion.IdRutaTrabajo, empleado.IdEmpleado, ct))
            return NotFound(new { mensaje = "No estás asignado a ese viaje." });

        var limpieza = new Limpieza
        {
            // SEGURIDAD: el autor sale de la sesion, nunca de un id enviado por el celular.
            IdEmpleadoSubio = empleado.IdEmpleado,
            IdRutaTrabajo = peticion.IdRutaTrabajo,
            IdServicioContrato = await ConsultasMovil.ContratoDelViajeAsync(_db, peticion.IdRutaTrabajo, ct),
            Fecha = ConsultasMovil.HoyLocal(),
            Observacion = texto,
            Etapa = etapa
        };

        _db.Limpiezas.Add(limpieza);
        await _db.SaveChangesAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(new
        {
            limpieza.IdLimpieza,
            limpieza.IdRutaTrabajo,
            limpieza.Fecha,
            tipo = etapa == EtapasEvidencia.Ruta ? "ruta" : "limpieza",
            texto,
            fotos = Array.Empty<int>(),
            mensaje = "Evidencia guardada correctamente."
        });
    }

    public sealed class GuardarEvidenciaTransportista
    {
        [Range(1, int.MaxValue)]
        public int IdRutaTrabajo { get; set; }

        [Required]
        [StringLength(10)]
        public string Tipo { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Texto { get; set; } = string.Empty;
    }

    // GET /api/movil/transportista/informacion-adicional
    [HttpGet("informacion-adicional")]
    public async Task<IActionResult> ConsultarInformacionAdicional(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new { mensaje = "No tienes permiso." });

        var registros = await (
            from l in _db.Limpiezas.AsNoTracking()
            join r in _db.RutasTrabajo.AsNoTracking() on l.IdRutaTrabajo equals (int?)r.IdRutaTrabajo
            where l.IdEmpleadoSubio == empleado.IdEmpleado && l.Etapa == EtapasEvidencia.InformacionAdicional
            orderby l.IdLimpieza descending
            select new { l.IdLimpieza, r.IdRutaTrabajo, r.Destino, l.Fecha, l.Observacion }
        ).Take(100).ToListAsync(ct);

        var respuesta = new List<object>();
        foreach (var r in registros)
        {
            var match = PatronInformacion.Match(r.Observacion ?? string.Empty);
            if (!match.Success)
                continue;

            var (fechaViaje, destino) = ConsultasMovil.LeerDestino(r.Destino);
            respuesta.Add(new
            {
                r.IdLimpieza,
                r.IdRutaTrabajo,
                destino,
                fechaViaje = fechaViaje?.ToString("yyyy-MM-dd"),
                r.Fecha,
                kilometros = decimal.Parse(match.Groups["km"].Value, CultureInfo.InvariantCulture),
                combustibleLitros = decimal.Parse(match.Groups["litros"].Value, CultureInfo.InvariantCulture),
                gastoUsd = decimal.Parse(match.Groups["gasto"].Value, CultureInfo.InvariantCulture)
            });
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(respuesta);
    }

    // POST /api/movil/transportista/informacion-adicional
    [HttpPost("informacion-adicional")]
    public async Task<IActionResult> RegistrarInformacionAdicional(
        [FromBody] RegistrarInformacionAdicionalPeticion peticion,
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "transportista")
            return StatusCode(403, new { mensaje = "No tienes permiso." });

        if (peticion.IdRutaTrabajo <= 0)
            return BadRequest(new { mensaje = "Selecciona un viaje válido." });

        // SEGURIDAD: rangos y precision acotados para no guardar valores absurdos.
        if (peticion.Kilometros < 0 || peticion.CombustibleLitros < 0 || peticion.GastoUsd < 0 ||
            peticion.Kilometros > 1000000 || peticion.CombustibleLitros > 1000000 || peticion.GastoUsd > 1000000)
            return BadRequest(new { mensaje = "Los valores deben estar entre 0 y 1,000,000." });

        if (decimal.Round(peticion.Kilometros, 2) != peticion.Kilometros ||
            decimal.Round(peticion.CombustibleLitros, 2) != peticion.CombustibleLitros ||
            decimal.Round(peticion.GastoUsd, 2) != peticion.GastoUsd)
            return BadRequest(new { mensaje = "Utiliza un máximo de dos decimales." });

        if (peticion.Kilometros == 0 && peticion.CombustibleLitros == 0 && peticion.GastoUsd == 0)
            return BadRequest(new { mensaje = "Debes registrar al menos un valor mayor que cero." });

        if (!await ConsultasMovil.EstaAsignadoAsync(_db, peticion.IdRutaTrabajo, empleado.IdEmpleado, ct))
            return NotFound(new { mensaje = "No estás asignado a ese viaje." });

        var km = peticion.Kilometros.ToString("0.##", CultureInfo.InvariantCulture);
        var litros = peticion.CombustibleLitros.ToString("0.##", CultureInfo.InvariantCulture);
        var gasto = peticion.GastoUsd.ToString("0.##", CultureInfo.InvariantCulture);

        var limpieza = new Limpieza
        {
            IdEmpleadoSubio = empleado.IdEmpleado,
            IdRutaTrabajo = peticion.IdRutaTrabajo,
            IdServicioContrato = await ConsultasMovil.ContratoDelViajeAsync(_db, peticion.IdRutaTrabajo, ct),
            Fecha = ConsultasMovil.HoyLocal(),
            Observacion = $"KM={km};L={litros};USD={gasto}",
            Etapa = EtapasEvidencia.InformacionAdicional
        };

        _db.Limpiezas.Add(limpieza);

        // El gasto del viaje tambien entra a gasto_viaje para que la app de
        // escritorio lo sume a los gastos de la empresa.
        if (peticion.GastoUsd > 0)
        {
            _db.GastosViaje.Add(new GastoViaje
            {
                IdRutaTrabajo = peticion.IdRutaTrabajo,
                IdEmpleado = empleado.IdEmpleado,
                Fecha = ConsultasMovil.HoyLocal(),
                Concepto = "Combustible",
                Monto = peticion.GastoUsd,
                Descripcion = $"{km} km, {litros} L"
            });
        }

        await _db.SaveChangesAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(new
        {
            limpieza.IdLimpieza,
            limpieza.IdRutaTrabajo,
            limpieza.Fecha,
            kilometros = peticion.Kilometros,
            combustibleLitros = peticion.CombustibleLitros,
            gastoUsd = peticion.GastoUsd,
            mensaje = "Información guardada correctamente."
        });
    }

    public sealed class RegistrarInformacionAdicionalPeticion
    {
        public int IdRutaTrabajo { get; set; }
        public decimal Kilometros { get; set; }
        public decimal CombustibleLitros { get; set; }
        public decimal GastoUsd { get; set; }
    }

    private static readonly Regex PatronInformacion = new(
        @"^KM=(?<km>[0-9]+(?:\.[0-9]{1,2})?);L=(?<litros>[0-9]+(?:\.[0-9]{1,2})?);USD=(?<gasto>[0-9]+(?:\.[0-9]{1,2})?)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));
}
