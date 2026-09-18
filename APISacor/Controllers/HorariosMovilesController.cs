using System.Data;
using APISacor.Data;
using APISacor.Models;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

[ApiController]
[Route("api/movil/horarios")]
public sealed class HorariosMovilesController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public HorariosMovilesController(
        SacorDbContext db,
        ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    // Hora local de El Salvador.
    private static DateTimeOffset Ahora()
    {
        return DateTimeOffset.UtcNow.ToOffset(
            TimeSpan.FromHours(-6)
        );
    }

    private static bool TienePermiso(Empleado empleado)
    {
        var rol = ServicioAccesoMovil.RolApp(empleado.Tipo);

        return rol is "transportista" or "tecnico";
    }

    // GET: api/movil/horarios
    [HttpGet]
    public async Task<IActionResult> Listar(
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(
            Request, ct
        );

        if (empleado == null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });

        if (!TienePermiso(empleado))
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        var horarios = await _db.HorariosTrabajo
            .AsNoTracking()
            .Where(h =>
                h.IdEmpleadoMarco == empleado.IdEmpleado)
            .OrderByDescending(h => h.Fecha)
            .ThenByDescending(h => h.IdHorarioTrabajo)
            .Take(50)
            .Select(h => new
            {
                h.IdHorarioTrabajo,
                h.Fecha,
                h.Entrada,
                h.Salida
            })
            .ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(horarios);
    }

    // POST: api/movil/horarios/entrada
    [HttpPost("entrada")]
    public async Task<IActionResult> Entrada(
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(
            Request, ct
        );

        if (empleado == null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });

        if (!TienePermiso(empleado))
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        // Ejecutar la comprobación y el INSERT
        // dentro de una transacción serializable.
        var estrategia = _db.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync<IActionResult>(
            async () =>
            {
                await using var transaccion =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        ct
                    );

                bool tieneJornadaAbierta =
                    await _db.HorariosTrabajo.AnyAsync(
                        h =>
                            h.IdEmpleadoMarco == empleado.IdEmpleado
                            && h.Salida == null,
                        ct
                    );

                if (tieneJornadaAbierta)
                    return Conflict(new
                    {
                        mensaje = "Ya tienes una jornada abierta."
                    });

                var ahora = Ahora();

                var horario = new HorarioTrabajo
                {
                    IdEmpleadoMarco = empleado.IdEmpleado,
                    Fecha = ahora.Date,
                    Entrada = ahora.TimeOfDay,
                    Salida = null
                };

                _db.HorariosTrabajo.Add(horario);

                await _db.SaveChangesAsync(ct);

                await transaccion.CommitAsync(ct);

                return Ok(new
                {
                    horario.IdHorarioTrabajo,
                    horario.Fecha,
                    horario.Entrada,
                    horario.Salida,
                    mensaje = "Entrada registrada correctamente."
                });
            }
        );
    }

    // PATCH: api/movil/horarios/5/salida
    [HttpPatch("{id:int}/salida")]
    public async Task<IActionResult> Salida(
        int id,
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(
            Request, ct
        );

        if (empleado == null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });

        if (!TienePermiso(empleado))
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        if (id <= 0)
            return BadRequest(new
            {
                mensaje = "Horario inválido."
            });

        var horaSalida = Ahora().TimeOfDay;

        // Actualizar únicamente una jornada abierta
        // que pertenezca al empleado autenticado.
        int modificados = await _db.HorariosTrabajo
            .Where(h =>
                h.IdHorarioTrabajo == id
                && h.IdEmpleadoMarco == empleado.IdEmpleado
                && h.Salida == null)
            .ExecuteUpdateAsync(
                cambios => cambios.SetProperty(
                    h => h.Salida,
                    (TimeSpan?)horaSalida
                ),
                ct
            );

        if (modificados == 0)
            return Conflict(new
            {
                mensaje =
                    "La jornada no existe, ya está cerrada o no te pertenece."
            });

        return Ok(new
        {
            idHorarioTrabajo = id,
            salida = horaSalida,
            mensaje = "Salida registrada correctamente."
        });
    }
}