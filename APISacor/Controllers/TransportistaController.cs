using APISacor.Data;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
}
