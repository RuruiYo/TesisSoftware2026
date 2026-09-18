using System.Security.Cryptography;
using System.Text;
using APISacor.Security;
using Microsoft.Extensions.Options;

namespace APISacor.Security;

// SEGURIDAD: middleware que corta cualquier peticion sin llave valida antes de que
// llegue a los controladores y antes de abrir conexion a la base de datos.
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ApiKeyOptions _opciones;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(
        RequestDelegate siguiente,
        IOptions<ApiKeyOptions> opciones,
        ILogger<ApiKeyMiddleware> logger)
    {
        _siguiente = siguiente;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        var ruta = contexto.Request.Path.Value ?? string.Empty;

        // La APK no puede ocultar una llave compartida. Las rutas moviles
        // validan el codigo de un solo uso o el token individual en su controlador.
        // StartsWithSegments no acepta rutas parecidas como /api/movil-falso.
        if (contexto.Request.Path.StartsWithSegments("/api/movil"))
        {
            await _siguiente(contexto);
            return;
        }

        // Swagger y health quedan fuera del filtro.
        if (_opciones.RutasPublicas.Any(r => ruta.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
        {
            await _siguiente(contexto);
            return;
        }

        if (!contexto.Request.Headers.TryGetValue(_opciones.NombreHeader, out var valoresHeader))
        {
            // 401 Unauthorized: no se presento credencial.
            await ResponderAsync(contexto, StatusCodes.Status401Unauthorized,
                "Falta el header de autenticacion.");
            return;
        }

        var llaveRecibida = valoresHeader.ToString();

        // SEGURIDAD: se descarta de inmediato una llave vacia o absurdamente larga
        // para no gastar CPU comparando basura (vector de DoS por hashing).
        if (string.IsNullOrWhiteSpace(llaveRecibida) || llaveRecibida.Length > 256)
        {
            await ResponderAsync(contexto, StatusCodes.Status401Unauthorized,
                "Credencial invalida.");
            return;
        }

        var appAutorizada = BuscarAppPorLlave(llaveRecibida);

        if (appAutorizada is null)
        {
            // SEGURIDAD: se registra el intento fallido con la IP pero NUNCA la llave
            // recibida, para que los logs no se conviertan en un archivo de secretos.
            _logger.LogWarning(
                "Intento de acceso con llave invalida desde {Ip} hacia {Ruta}",
                contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                ruta);

            // 403 Forbidden: presento credencial pero no es valida.
            await ResponderAsync(contexto, StatusCodes.Status403Forbidden,
                "Credencial no autorizada.");
            return;
        }

        // Se guarda el nombre de la app en el contexto para trazabilidad en los logs.
        contexto.Items["AppConsumidora"] = appAutorizada;
        await _siguiente(contexto);
    }

    private string? BuscarAppPorLlave(string llaveRecibida)
    {
        var bytesRecibidos = Encoding.UTF8.GetBytes(llaveRecibida);
        string? encontrada = null;

        // SEGURIDAD: se recorre el diccionario completo SIN cortar al primer acierto y
        // se compara con FixedTimeEquals. Las dos cosas juntas hacen que el tiempo de
        // respuesta no dependa de que tan parecida es la llave enviada, lo que bloquea
        // los ataques de temporizacion (timing attack) para adivinarla caracter por caracter.
        foreach (var par in _opciones.ApiKeys)
        {
            if (string.IsNullOrWhiteSpace(par.Value)) continue;

            var bytesEsperados = Encoding.UTF8.GetBytes(par.Value);

            if (bytesRecibidos.Length == bytesEsperados.Length &&
                CryptographicOperations.FixedTimeEquals(bytesRecibidos, bytesEsperados))
            {
                encontrada = par.Key;
            }
        }

        return encontrada;
    }

    private static async Task ResponderAsync(HttpContext contexto, int codigo, string mensaje)
    {
        contexto.Response.StatusCode = codigo;
        contexto.Response.ContentType = "application/json";

        // SEGURIDAD: el mensaje es generico a proposito. No se dice si el header
        // existia, si la llave era de otra app o si estaba expirada, porque cada
        // detalle extra le sirve al atacante para ir acercandose.
        await contexto.Response.WriteAsJsonAsync(new
        {
            estado = codigo,
            mensaje
        });
    }
}
