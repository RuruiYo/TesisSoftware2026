using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using APISacor.Data;
using APISacor.Security;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// SEGURIDAD: variables de entorno como fuente de configuracion.
// AddEnvironmentVariables se agrega de ultimo para que TENGA la prioridad mas
// alta: lo que este en el entorno del servidor gana sobre appsettings.json.
// Asi el archivo del repositorio puede quedar sin un solo secreto real.
//   setx Seguridad__ApiKeys__AppWeb "valor-largo-aleatorio"
//   setx ConnectionStrings__SacorDb "Server=...;"
// ---------------------------------------------------------------------------
builder.Configuration.AddEnvironmentVariables();

// ---------------------------------------------------------------------------
// SEGURIDAD: limite de tamano del cuerpo de la peticion.
// Sustituye al max_tokens de un modelo: el objetivo es el mismo, que nadie
// pueda mandar una carga ilimitada y consumir memoria y ancho de banda sin tope.
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(opciones =>
{
    opciones.Limits.MaxRequestBodySize = 1_048_576;                       // 1 MB por peticion
    opciones.Limits.MaxRequestHeadersTotalSize = 32_768;                  // 32 KB de headers
    opciones.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(20);     // corta slowloris
    opciones.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    opciones.AddServerHeader = false;                                     // no revelar "Kestrel"
});

// ---------------------------------------------------------------------------
// Base de datos por inyeccion de dependencias.
// ---------------------------------------------------------------------------
var cadenaConexion = builder.Configuration.GetConnectionString("SacorDb");

// SEGURIDAD: si falta la cadena de conexion la aplicacion no arranca. Es mejor
// fallar de una vez que quedar corriendo a medias y filtrar errores en cada peticion.
if (string.IsNullOrWhiteSpace(cadenaConexion))
{
    throw new InvalidOperationException(
        "Falta la cadena de conexion 'SacorDb'. Configurala en variables de entorno " +
        "(ConnectionStrings__SacorDb) o con dotnet user-secrets.");
}

builder.Services.AddDbContext<SacorDbContext>(opciones =>
{
    opciones.UseSqlServer(cadenaConexion, sql =>
    {
        // SEGURIDAD: timeout de comando. Una consulta pesada o un bloqueo en la
        // base no puede dejar la peticion colgada para siempre ocupando un hilo.
        sql.CommandTimeout(30);

        // Reintentos con espera creciente ante fallos transitorios de red.
        sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    });

    // SEGURIDAD: los datos sensibles NUNCA se escriben en los logs de EF, ni en
    // desarrollo. Habilitar EnableSensitiveDataLogging deja DUI y sueldos en texto
    // plano en el archivo de log.
    opciones.EnableSensitiveDataLogging(false);
    opciones.EnableDetailedErrors(builder.Environment.IsDevelopment());
});

builder.Services.AddScoped<ServicioAccesoMovil>();

// ---------------------------------------------------------------------------
// SEGURIDAD: lectura de las API Keys desde configuracion, nunca del codigo.
// ---------------------------------------------------------------------------
builder.Services.Configure<ApiKeyOptions>(
    builder.Configuration.GetSection(ApiKeyOptions.SeccionConfiguracion));

// ---------------------------------------------------------------------------
// SEGURIDAD: cliente HTTP por inyeccion de dependencias con IHttpClientFactory.
// El timeout se define aqui, una sola vez, y aplica a todas las llamadas salientes.
// ---------------------------------------------------------------------------
var opcionesExterno = builder.Configuration
    .GetSection(OpcionesServicioExterno.SeccionConfiguracion)
    .Get<OpcionesServicioExterno>() ?? new OpcionesServicioExterno();

builder.Services.AddSingleton(opcionesExterno);

builder.Services.AddHttpClient<ClienteServicioExterno>(cliente =>
{
    if (!string.IsNullOrWhiteSpace(opcionesExterno.BaseUrl))
        cliente.BaseAddress = new Uri(opcionesExterno.BaseUrl);

    // SEGURIDAD: timeout duro del HttpClient, ademas del CancellationToken
    // que aplica el propio servicio. Doble red de proteccion contra DoS.
    cliente.Timeout = TimeSpan.FromSeconds(opcionesExterno.TimeoutSegundos);

    // SEGURIDAD: tope de bytes en los headers de respuesta ajena.
    cliente.MaxResponseContentBufferSize = opcionesExterno.MaxBytesRespuesta;

    if (!string.IsNullOrWhiteSpace(opcionesExterno.ApiKey))
        cliente.DefaultRequestHeaders.Add("X-Api-Key", opcionesExterno.ApiKey);
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5));  // renueva DNS cada 5 minutos

// ---------------------------------------------------------------------------
// SEGURIDAD: rate limiting por app consumidora.
// Con tres apps conocidas, una que entre en un bucle de reintentos podria
// dejar sin servicio a las otras dos. La ventana fija corta eso de raiz y
// devuelve 429 Too Many Requests, que es el codigo correcto.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Limite especifico por IP para intentos de activacion, adicional al global.
    opciones.AddPolicy("ActivacionMovil", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
    {
        // Se reparte por llave de app; si no hay llave todavia, por direccion IP.
        var particion = contexto.Request.Headers["X-Api-Key"].ToString();

        if (string.IsNullOrWhiteSpace(particion))
            particion = contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo";

        return RateLimitPartition.GetFixedWindowLimiter(particion, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,                      // 120 peticiones
                Window = TimeSpan.FromMinutes(1),       // por minuto
                QueueLimit = 0,                         // sin cola: se rechaza de inmediato
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    opciones.OnRejected = async (contexto, token) =>
    {
        contexto.HttpContext.Response.Headers.RetryAfter = "60";
        await contexto.HttpContext.Response.WriteAsJsonAsync(new
        {
            estado = 429,
            mensaje = "Demasiadas peticiones. Intenta de nuevo en un minuto."
        }, token);
    };
});

// ---------------------------------------------------------------------------
// SEGURIDAD: CORS con lista blanca de origenes.
// AllowAnyOrigin dejaria que cualquier sitio web hiciera peticiones desde el
// navegador de un empleado. Solo se permiten los origenes de las tres apps.
// ---------------------------------------------------------------------------
var origenesPermitidos = builder.Configuration
    .GetSection("Seguridad:OrigenesPermitidos").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("AppsSacor", politica =>
    {
        if (origenesPermitidos.Length > 0)
        {
            politica.WithOrigins(origenesPermitidos)
                    .WithHeaders("Content-Type", "X-Api-Key", "Authorization")
                    .WithMethods("GET", "POST", "PUT", "DELETE");
        }
    });
});

// ---------------------------------------------------------------------------
// Controladores y serializacion.
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(opciones =>
    {
        // SEGURIDAD: tope de profundidad del JSON de entrada. Un objeto anidado
        // miles de niveles provoca desbordamiento de pila al deserializar.
        opciones.JsonSerializerOptions.MaxDepth = 16;

        // No serializar ciclos de referencia ni propiedades nulas innecesarias.
        opciones.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opciones.JsonSerializerOptions.DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull;
    });

// SEGURIDAD: se personaliza la respuesta de validacion automatica para que
// devuelva 400 con el detalle de los campos invalidos, sin trazas internas.
builder.Services.Configure<ApiBehaviorOptions>(opciones =>
{
    opciones.InvalidModelStateResponseFactory = contexto =>
    {
        var errores = contexto.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new
        {
            estado = 400,
            mensaje = "Los datos enviados no son validos.",
            errores
        });
    };
});

// SEGURIDAD: manejador global de excepciones registrado como servicio.
builder.Services.AddExceptionHandler<ManejadorGlobalExcepciones>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API SACOR",
        Version = "v1",
        Description = "API de gestion para SACOR. Requiere API Key en el header X-Api-Key."
    });

    // Swagger declara el esquema de seguridad para poder probar con la llave.
    opciones.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Llave asignada a la app consumidora."
    });

    // Las rutas /api/movil usan Bearer personal, no X-Api-Key incluida en una APK.
    opciones.AddSecurityDefinition("BearerMovil", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "opaque",
        Description = "Token personal entregado por POST /api/movil/activacion."
    });

    opciones.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Orden del pipeline. El orden importa por seguridad.
// ---------------------------------------------------------------------------

// 1. Excepciones primero, para que atrape todo lo que venga despues.
app.UseExceptionHandler();

// 2. SEGURIDAD: cabeceras de proteccion del navegador. Se agregan a mano porque
//    las tres apps consumidoras pueden mostrar datos que vienen de este API.
app.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras["X-Content-Type-Options"] = "nosniff";      // no adivinar el tipo MIME
    cabeceras["X-Frame-Options"] = "DENY";                // no embebible en iframe
    cabeceras["Referrer-Policy"] = "no-referrer";
    cabeceras["X-Permitted-Cross-Domain-Policies"] = "none";

    // SEGURIDAD: la CSP se ajusta segun lo que devuelve la ruta.
    // Las respuestas del API son JSON puro: no cargan scripts, estilos ni imagenes,
    // asi que reciben la politica mas estricta posible ('none' para todo).
    // Swagger UI en cambio es una aplicacion JavaScript con estilos propios; con
    // 'none' el navegador bloquea su JS y la pagina queda en blanco. Por eso esa
    // ruta recibe una politica acotada pero funcional, y solo existe en Development.
    var esSwagger = contexto.Request.Path.StartsWithSegments("/swagger");

    cabeceras["Content-Security-Policy"] = esSwagger
        ? "default-src 'self'; " +
          "script-src 'self' 'unsafe-inline'; " +
          "style-src 'self' 'unsafe-inline'; " +
          "img-src 'self' data:; " +
          "font-src 'self' data:; " +
          "connect-src 'self'; " +
          "frame-ancestors 'none'"
        : "default-src 'none'; frame-ancestors 'none'";

    await siguiente();
});

// 3. Swagger solo en desarrollo. En produccion exponer el esquema completo del
//    API le entrega el mapa del sistema a cualquiera.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // SEGURIDAD: HSTS obliga al navegador a usar HTTPS en visitas posteriores.
    app.UseHsts();

    // SEGURIDAD: la redireccion a HTTPS solo se aplica fuera de Development.
    // En desarrollo se deja HTTP disponible porque los clientes que no son
    // navegador (el emulador de Android y la app de JavaFX) no confian en el
    // certificado autofirmado de dotnet dev-certs y la redireccion les rompe
    // las peticiones. En produccion la redireccion es obligatoria.
    app.UseHttpsRedirection();
}

// 4. CORS antes del filtro de llave, para que las peticiones preflight funcionen.
app.UseCors("AppsSacor");

// 5. Rate limiting antes de la autenticacion: frena la fuerza bruta de llaves
//    sin gastar CPU comparando cada intento.
app.UseRateLimiter();

// 6. SEGURIDAD: filtro de API Key. Nada llega a los controladores sin llave valida.
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

// Endpoint publico de verificacion. No toca la base de datos ni revela version.
app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

app.Run();
