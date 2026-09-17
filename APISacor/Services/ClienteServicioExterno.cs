using System.Net.Http.Headers;
using System.Text.Json;

namespace APISacor.Services;

public class OpcionesServicioExterno
{
    public const string SeccionConfiguracion = "ServicioExterno";

    public string BaseUrl { get; set; } = string.Empty;

    // SEGURIDAD: la llave sale de configuracion, igual que las de entrada.
    // Variable de entorno: ServicioExterno__ApiKey=valor
    public string ApiKey { get; set; } = string.Empty;

    // SEGURIDAD: timeout obligatorio. Sin el, una peticion saliente colgada
    // mantiene ocupado un hilo y una conexion del pool de forma indefinida;
    // con suficientes peticiones asi el API se cae sola (DoS por agotamiento).
    public int TimeoutSegundos { get; set; } = 15;

    // SEGURIDAD: tope de bytes que aceptamos leer de la respuesta ajena.
    // Impide que un servicio externo comprometido nos envie 10 GB y nos
    // reviente la memoria.
    public int MaxBytesRespuesta { get; set; } = 1_048_576;
}

// SEGURIDAD: cliente HTTP registrado por inyeccion de dependencias mediante
// IHttpClientFactory (ver Program.cs). Esto importa por tres razones:
//   1. Reutiliza sockets desde un pool. Crear "new HttpClient()" en cada llamada
//      agota los puertos del sistema (socket exhaustion) y se vuelve un DoS propio.
//   2. Renueva las entradas de DNS cada cierto tiempo, cosa que un HttpClient
//      estatico no hace y termina hablando con una IP vieja.
//   3. Permite configurar timeout y politicas en un solo lugar auditable.
public class ClienteServicioExterno
{
    private readonly HttpClient _http;
    private readonly OpcionesServicioExterno _opciones;
    private readonly ILogger<ClienteServicioExterno> _logger;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        // SEGURIDAD: se limita la profundidad del JSON para que un payload
        // profundamente anidado no provoque desbordamiento de pila al deserializar.
        MaxDepth = 16
    };

    public ClienteServicioExterno(
        HttpClient http,
        OpcionesServicioExterno opciones,
        ILogger<ClienteServicioExterno> logger)
    {
        _http = http;
        _opciones = opciones;
        _logger = logger;
    }

    public async Task<TRespuesta?> EnviarAsync<TPeticion, TRespuesta>(
        string rutaRelativa,
        TPeticion cuerpo,
        CancellationToken cancellationToken)
    {
        // SEGURIDAD: se valida que la ruta sea relativa. Si se aceptara una URL
        // absoluta desde afuera, un atacante podria redirigir la llamada (y la
        // llave de autenticacion que va en el header) a un servidor suyo: SSRF.
        if (rutaRelativa.Contains("://", StringComparison.Ordinal) ||
            rutaRelativa.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("Solo se permiten rutas relativas.", nameof(rutaRelativa));
        }

        using var peticion = new HttpRequestMessage(HttpMethod.Post, rutaRelativa.TrimStart('/'));
        peticion.Content = JsonContent.Create(cuerpo);
        peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // SEGURIDAD: se combina el token de cancelacion de la peticion entrante con
        // un timeout propio, de modo que la llamada muere por cualquiera de los dos.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_opciones.TimeoutSegundos));

        using var respuesta = await _http.SendAsync(
            peticion,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        if (!respuesta.IsSuccessStatusCode)
        {
            // SEGURIDAD: se registra el codigo pero NO el cuerpo completo del error,
            // que podria traer datos sensibles del otro sistema.
            _logger.LogWarning(
                "El servicio externo respondio {Codigo} en {Ruta}",
                (int)respuesta.StatusCode, rutaRelativa);

            throw new HttpRequestException(
                $"El servicio externo respondio {(int)respuesta.StatusCode}.");
        }

        // SEGURIDAD: lectura acotada. Se rechaza la respuesta si declara mas bytes
        // del tope configurado, sin siquiera empezar a leerla.
        if (respuesta.Content.Headers.ContentLength > _opciones.MaxBytesRespuesta)
        {
            throw new HttpRequestException("La respuesta del servicio externo excede el limite.");
        }

        await using var flujo = await respuesta.Content.ReadAsStreamAsync(cts.Token);
        return await JsonSerializer.DeserializeAsync<TRespuesta>(flujo, OpcionesJson, cts.Token);
    }
}
