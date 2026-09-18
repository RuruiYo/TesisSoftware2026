using System.Security.Cryptography;
using System.Text;
using APISacor.Data;
using APISacor.Models;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Services;

// Los tokens y codigos sin hash solo existen en memoria durante la respuesta HTTPS.
public sealed class ServicioAccesoMovil
{
    private readonly SacorDbContext _db;

    public ServicioAccesoMovil(SacorDbContext db) => _db = db;

    public static string CrearCodigo() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public static string CrearToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    public static string? RolApp(string tipo) => tipo.ToUpperInvariant() switch
    {
        "MOTORISTA" => "transportista",
        "TECNICO" => "tecnico",
        "ADMINISTRADOR" => "administrador",
        _ => null
    };

    public static string? ObtenerToken(HttpRequest solicitud)
    {
        var autorizacion = solicitud.Headers.Authorization.ToString();
        if (!autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = autorizacion[7..].Trim();
        return token.Length == 64 && token.All(Uri.IsHexDigit) ? token.ToUpperInvariant() : null;
    }

    public async Task<Empleado?> IdentificarAsync(HttpRequest solicitud, CancellationToken ct)
    {
        var token = ObtenerToken(solicitud);
        if (token is null) return null;

        var hash = Hash(token);
        var ahora = DateTime.UtcNow;
        var sesion = await _db.SesionesMoviles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.ExpiraUtc > ahora && s.RevocadoUtc == null, ct);
        if (sesion is null) return null;

        var empleado = await _db.Empleados.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdEmpleado == sesion.IdEmpleado, ct);

        return empleado is not null &&
               empleado.Estado.Equals("Activo", StringComparison.OrdinalIgnoreCase) &&
               RolApp(empleado.Tipo) is not null
            ? empleado : null;
    }

    public static object Perfil(Empleado e) => new
    {
        idEmpleado = e.IdEmpleado,
        codigoEmpleado = e.CodigoEmpleado,
        nombre = e.Nombre,
        rol = RolApp(e.Tipo),
        tipo = e.Tipo
    };
}
