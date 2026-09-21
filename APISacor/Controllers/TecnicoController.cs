using APISacor.Data;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using APISacor.Models;

namespace APISacor.Controllers;

// Solo datos propios del técnico identificado por el token individual.
[ApiController]
[Route("api/movil/tecnico")]
[Produces("application/json")]
public sealed class TecnicoController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public TecnicoController(SacorDbContext db, ServicioAccesoMovil acceso)
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
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new { mensaje = "No tienes permiso para consultar este módulo." });

        var id = empleado.IdEmpleado;
        Response.Headers.CacheControl = "no-store";

        var horarios = await _db.HorariosTrabajo.AsNoTracking()
            .Where(h => h.IdEmpleadoMarco == id)
            .OrderByDescending(h => h.Fecha)
            .ThenByDescending(h => h.IdHorarioTrabajo)
            .Take(20)
            .Select(h => new { h.IdHorarioTrabajo, h.Fecha, h.Entrada, h.Salida })
            .ToListAsync(ct);

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

        // Limpiezas que el propio técnico registró. No se exponen las de otros.
        var limpiezas = await _db.Limpiezas.AsNoTracking()
            .Where(l => l.IdEmpleadoSubio == id)
            .OrderByDescending(l => l.Fecha)
            .ThenByDescending(l => l.IdLimpieza)
            .Take(20)
            .Select(l => new { l.IdLimpieza, l.IdServicioContrato, l.Fecha, l.Observacion, l.Etapa })
            .ToListAsync(ct);

        // Un pesaje puede corresponder al técnico como responsable o como autor.
        var pesajes = await _db.Pesajes.AsNoTracking()
            .Where(p => p.IdTecnicoTransporte == id || p.IdEmpleadoSubio == id)
            .OrderByDescending(p => p.Fecha)
            .ThenByDescending(p => p.IdPesaje)
            .Take(20)
            .Select(p => new { p.IdPesaje, p.IdServicioContrato, p.Fecha, p.PesoTotal, p.Etapa })
            .ToListAsync(ct);

        return Ok(new
        {
            empleado = ServicioAccesoMovil.Perfil(empleado),
            rutas,
            horarios,
            limpiezas,
            pesajes
        });
    }
    // Datos recibidos desde la aplicación.
    public sealed class PeticionRecoleccion
    {
        [Range(1, int.MaxValue)]
        public int IdServicioContrato { get; set; }

        [Required]
        [StringLength(1000)]
        public string Observacion { get; set; } = string.Empty;
    }

    // GET: api/movil/tecnico/servicios
    // Devuelve los servicios de las rutas asignadas al técnico.
    [HttpGet("servicios")]
    public async Task<IActionResult> MisServicios(
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);

        if (empleado is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });

        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        var servicios = await (
            from servicio in _db.ServiciosContrato.AsNoTracking()

            join asignacion in _db.RutasEmpleado.AsNoTracking()
                on servicio.IdRutaTrabajo
                equals asignacion.IdRutaTrabajo

            join ruta in _db.RutasTrabajo.AsNoTracking()
                on servicio.IdRutaTrabajo
                equals ruta.IdRutaTrabajo

            where asignacion.IdEmpleado == empleado.IdEmpleado

            select new
            {
                servicio.IdServicioContrato,
                servicio.IdRutaTrabajo,
                ruta.Destino
            }
        )
        .Distinct()
        .OrderBy(s => s.IdServicioContrato)
        .Take(100)
        .ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(servicios);
    }

    // POST: api/movil/tecnico/recoleccion
    [HttpPost("recoleccion")]
    public async Task<IActionResult> RegistrarRecoleccion(
        [FromBody] PeticionRecoleccion peticion,
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);

        if (empleado is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });

        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        var observacion = peticion.Observacion?.Trim();

        if (string.IsNullOrWhiteSpace(observacion))
            return BadRequest(new
            {
                mensaje = "Debes escribir una observación."
            });

        // Comprobar que el servicio pertenece a una ruta
        // realmente asignada al técnico.
        bool servicioPermitido = await (
            from servicio in _db.ServiciosContrato

            join asignacion in _db.RutasEmpleado
                on servicio.IdRutaTrabajo
                equals asignacion.IdRutaTrabajo

            where servicio.IdServicioContrato
                    == peticion.IdServicioContrato

                  && asignacion.IdEmpleado
                    == empleado.IdEmpleado

            select servicio.IdServicioContrato
        ).AnyAsync(ct);

        if (!servicioPermitido)
            return BadRequest(new
            {
                mensaje = "El servicio no está asignado a este técnico."
            });

        var fechaLocal = DateTimeOffset.UtcNow
            .ToOffset(TimeSpan.FromHours(-6))
            .Date;

        var limpieza = new Limpieza
        {
            // El empleado se obtiene de su sesión.
            IdEmpleadoSubio = empleado.IdEmpleado,

            IdServicioContrato = peticion.IdServicioContrato,

            Fecha = fechaLocal,

            Observacion = observacion,

            Etapa = "Recolección finalizada"
        };

        _db.Limpiezas.Add(limpieza);

        await _db.SaveChangesAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(new
        {
            limpieza.IdLimpieza,
            limpieza.IdServicioContrato,
            limpieza.Fecha,
            limpieza.Observacion,
            limpieza.Etapa,
            mensaje = "Recolección registrada correctamente."
        });
    }
}
