using APISacor.Data;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
}
