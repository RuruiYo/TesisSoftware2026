using System.ComponentModel.DataAnnotations;
using APISacor.Data;
using APISacor.Models;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

[ApiController]
[Route("api/movil")]
[Produces("application/json")]
public sealed class AccesoMovilController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public AccesoMovilController(
        SacorDbContext db,
        ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    // El empleado solo escribe su código.
    public sealed class PeticionIngreso
    {
        [Required]
        [StringLength(30, MinimumLength = 1)]
        public string Codigo { get; set; } = string.Empty;
    }

    // POST /api/movil/ingresar
    [HttpPost("ingresar")]
    [EnableRateLimiting("ActivacionMovil")]
    public async Task<IActionResult> Ingresar(
        [FromBody] PeticionIngreso peticion,
        CancellationToken ct)
    {
        string codigo = peticion.Codigo.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Unauthorized(new
            {
                mensaje = "Código incorrecto o empleado inactivo."
            });
        }

        // Consultar el código directamente en SQL Server.
        var empleado = await _db.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.CodigoEmpleado == codigo,
                ct
            );

        if (empleado == null ||
            !string.Equals(
                empleado.Estado,
                "Activo",
                StringComparison.OrdinalIgnoreCase
            ) ||
            ServicioAccesoMovil.RolApp(empleado.Tipo) == null)
        {
            return Unauthorized(new
            {
                mensaje = "Código incorrecto o empleado inactivo."
            });
        }

        // Generar la sesión del empleado.
        var estrategia = _db.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync<IActionResult>(async () =>
        {
            var ahora = DateTime.UtcNow;

            await using var transaccion =
                await _db.Database.BeginTransactionAsync(ct);

            // Cerrar sesiones anteriores del mismo empleado.
            await _db.SesionesMoviles
                .Where(s =>
                    s.IdEmpleado == empleado.IdEmpleado &&
                    s.RevocadoUtc == null)
                .ExecuteUpdateAsync(
                    cambios => cambios.SetProperty(
                        s => s.RevocadoUtc,
                        (DateTime?)ahora
                    ),
                    ct
                );

            string token = ServicioAccesoMovil.CrearToken();
            DateTime expiracion = ahora.AddDays(30);

            var sesion = new SesionMovil
            {
                IdEmpleado = empleado.IdEmpleado,
                TokenHash = ServicioAccesoMovil.Hash(token),
                CreadoUtc = ahora,
                ExpiraUtc = expiracion
            };

            _db.SesionesMoviles.Add(sesion);

            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            Response.Headers.CacheControl = "no-store";

            return Ok(new
            {
                token,
                expiraUtc = expiracion,
                empleado = ServicioAccesoMovil.Perfil(empleado)
            });
        });
    }

    // GET /api/movil/perfil
    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);

        if (empleado == null)
        {
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });
        }

        return Ok(ServicioAccesoMovil.Perfil(empleado));
    }

    // POST /api/movil/salir
    [HttpPost("salir")]
    public async Task<IActionResult> Salir(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        var token = ServicioAccesoMovil.ObtenerToken(Request);

        if (empleado == null || token == null)
        {
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });
        }

        string hash = ServicioAccesoMovil.Hash(token);

        await _db.SesionesMoviles
            .Where(s =>
                s.TokenHash == hash &&
                s.RevocadoUtc == null)
            .ExecuteUpdateAsync(
                cambios => cambios.SetProperty(
                    s => s.RevocadoUtc,
                    (DateTime?)DateTime.UtcNow
                ),
                ct
            );

        return NoContent();
    }
}