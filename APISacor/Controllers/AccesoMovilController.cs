using System.ComponentModel.DataAnnotations;
using APISacor.Data;
using APISacor.Models;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// No usa X-Api-Key incrustada en APK: activacion anonima de codigo fuerte y
// resto de acciones con bearer individual verificable y revocable en SQL Server.
[ApiController]
[Route("api/movil")]
[Produces("application/json")]
public sealed class AccesoMovilController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public AccesoMovilController(SacorDbContext db, ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    public sealed class PeticionActivacion
    {
        [Required, StringLength(32, MinimumLength = 32)]
        public string Codigo { get; set; } = string.Empty;
    }

    public sealed class PeticionGenerarCodigo
    {
        [Range(1, int.MaxValue)]
        public int IdEmpleado { get; set; }
    }

    [HttpPost("activacion")]
    [EnableRateLimiting("ActivacionMovil")]
    public async Task<IActionResult> Activar([FromBody] PeticionActivacion peticion, CancellationToken ct)
    {
        var codigo = peticion.Codigo.Trim().ToUpperInvariant();
        if (codigo.Length != 32 || !codigo.All(Uri.IsHexDigit))
            return Unauthorized(new { mensaje = "Código inválido o vencido." });

        var ahora = DateTime.UtcNow;
        var hash = ServicioAccesoMovil.Hash(codigo);
        // EF tiene EnableRetryOnFailure: toda transaccion explicita debe ejecutarse
        // dentro de su estrategia de reintentos, como el resto de esta API.
        var estrategia = _db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);
            var registro = await _db.CodigosActivacionMovil
                .FirstOrDefaultAsync(c => c.CodigoHash == hash && c.UsadoUtc == null &&
                                          c.RevocadoUtc == null && c.ExpiraUtc > ahora, ct);
            if (registro is null)
                return Unauthorized(new { mensaje = "Código inválido o vencido." });

            var empleado = await _db.Empleados.AsNoTracking()
                .FirstOrDefaultAsync(e => e.IdEmpleado == registro.IdEmpleado, ct);
            if (empleado is null || !empleado.Estado.Equals("Activo", StringComparison.OrdinalIgnoreCase)
                || ServicioAccesoMovil.RolApp(empleado.Tipo) is null)
                return Unauthorized(new { mensaje = "Código inválido o vencido." });

            // Consumo atomico: solo una solicitud puede canjear el mismo codigo.
            var consumidos = await _db.CodigosActivacionMovil
                .Where(c => c.IdCodigoActivacion == registro.IdCodigoActivacion && c.UsadoUtc == null
                            && c.RevocadoUtc == null && c.ExpiraUtc > ahora)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(c => c.UsadoUtc, (DateTime?)ahora), ct);
            if (consumidos != 1)
                return Unauthorized(new { mensaje = "Código inválido o vencido." });

            // Una reactivacion reemplaza todas las sesiones anteriores del empleado.
            await _db.SesionesMoviles.Where(s => s.IdEmpleado == empleado.IdEmpleado && s.RevocadoUtc == null)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(s => s.RevocadoUtc, (DateTime?)ahora), ct);

            var token = ServicioAccesoMovil.CrearToken();
            var expiracion = ahora.AddDays(30);
            _db.SesionesMoviles.Add(new SesionMovil
            {
                IdEmpleado = empleado.IdEmpleado,
                TokenHash = ServicioAccesoMovil.Hash(token),
                CreadoUtc = ahora,
                ExpiraUtc = expiracion
            });
            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            Response.Headers.CacheControl = "no-store";
            return Ok(new { token, expiraUtc = expiracion, empleado = ServicioAccesoMovil.Perfil(empleado) });
        });
    }

    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        return empleado is null
            ? Unauthorized(new { mensaje = "Sesión inválida o vencida." })
            : Ok(ServicioAccesoMovil.Perfil(empleado));
    }

    [HttpPost("salir")]
    public async Task<IActionResult> Salir(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        var token = ServicioAccesoMovil.ObtenerToken(Request);
        if (empleado is null || token is null)
            return Unauthorized(new { mensaje = "Sesión inválida o vencida." });

        var hash = ServicioAccesoMovil.Hash(token);
        await _db.SesionesMoviles.Where(s => s.TokenHash == hash && s.RevocadoUtc == null)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(s => s.RevocadoUtc, (DateTime?)DateTime.UtcNow), ct);
        return NoContent();
    }

    [HttpPost("administracion/codigos")]
    public async Task<IActionResult> GenerarCodigo([FromBody] PeticionGenerarCodigo peticion, CancellationToken ct)
    {
        var administrador = await _acceso.IdentificarAsync(Request, ct);
        if (administrador is null)
            return Unauthorized(new { mensaje = "Sesión inválida o vencida." });
        if (ServicioAccesoMovil.RolApp(administrador.Tipo) != "administrador")
            return StatusCode(403, new { mensaje = "No tienes permiso para generar códigos." });

        var empleado = await _db.Empleados.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdEmpleado == peticion.IdEmpleado, ct);
        if (empleado is null || !empleado.Estado.Equals("Activo", StringComparison.OrdinalIgnoreCase)
            || ServicioAccesoMovil.RolApp(empleado.Tipo) is null)
            return BadRequest(new { mensaje = "El empleado no existe, no está activo o no tiene un rol móvil." });

        var ahora = DateTime.UtcNow;
        var codigo = ServicioAccesoMovil.CrearCodigo();
        var estrategia = _db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);
            await _db.CodigosActivacionMovil.Where(c => c.IdEmpleado == empleado.IdEmpleado &&
                    c.UsadoUtc == null && c.RevocadoUtc == null)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(c => c.RevocadoUtc, (DateTime?)ahora), ct);

            _db.CodigosActivacionMovil.Add(new CodigoActivacionMovil
            {
                IdEmpleado = empleado.IdEmpleado,
                IdAdministradorGenerador = administrador.IdEmpleado,
                CodigoHash = ServicioAccesoMovil.Hash(codigo),
                CreadoUtc = ahora,
                ExpiraUtc = ahora.AddHours(24)
            });
            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            // Se devuelve una sola vez; nunca se guarda ni registra en texto claro.
            Response.Headers.CacheControl = "no-store";
            return Ok(new { codigo, expiraUtc = ahora.AddHours(24), idEmpleado = empleado.IdEmpleado });
        });
    }
}
