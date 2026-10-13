using APISacor.Data;
using APISacor.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// Calendario de viajes del motorista y del tecnico autenticado.
[ApiController]
[Route("api/movil/viajes")]
[Produces("application/json")]
public sealed class ViajesMovilController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public ViajesMovilController(SacorDbContext db, ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    // GET /api/movil/viajes
    [HttpGet]
    public async Task<IActionResult> MisViajes(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });

        var rol = ServicioAccesoMovil.RolApp(empleado.Tipo);
        if (rol is not ("transportista" or "tecnico"))
            return StatusCode(403, new { mensaje = "No tienes permiso." });

        // SEGURIDAD: los viajes se buscan por el empleado de la sesion, nunca por un
        // id que mande el celular. Tope de 500 para no devolver respuestas enormes.
        var ids = await _db.RutasEmpleado.AsNoTracking()
            .Where(a => a.IdEmpleado == empleado.IdEmpleado)
            .OrderByDescending(a => a.IdRutaTrabajo)
            .Select(a => a.IdRutaTrabajo)
            .Distinct()
            .Take(500)
            .ToListAsync(ct);

        var viajes = await ConsultasMovil.ResumirViajesAsync(_db, ids, ct);

        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            hoy = ConsultasMovil.HoyLocal().ToString("yyyy-MM-dd"),
            viajes
        });
    }
}
