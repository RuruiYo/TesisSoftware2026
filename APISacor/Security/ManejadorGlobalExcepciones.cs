using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Security;

// SEGURIDAD: manejador global de excepciones. Es la ultima red: si un controlador
// deja escapar un error, aqui se convierte en una respuesta JSON limpia.
// El punto clave es que el cliente NUNCA recibe el stack trace, el nombre del
// servidor SQL, la cadena de conexion ni el nombre de las columnas, porque toda
// esa informacion le sirve a un atacante para mapear el sistema.
public class ManejadorGlobalExcepciones : IExceptionHandler
{
    private readonly ILogger<ManejadorGlobalExcepciones> _logger;
    private readonly IHostEnvironment _entorno;

    public ManejadorGlobalExcepciones(
        ILogger<ManejadorGlobalExcepciones> logger,
        IHostEnvironment entorno)
    {
        _logger = logger;
        _entorno = entorno;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken cancellationToken)
    {
        // El detalle completo va SOLO al log del servidor, nunca a la respuesta.
        _logger.LogError(excepcion,
            "Error no controlado en {Metodo} {Ruta} (app: {App})",
            contexto.Request.Method,
            contexto.Request.Path,
            contexto.Items["AppConsumidora"] ?? "desconocida");

        var (codigo, mensaje) = MapearExcepcion(excepcion);

        contexto.Response.StatusCode = codigo;
        contexto.Response.ContentType = "application/json";

        await contexto.Response.WriteAsJsonAsync(new
        {
            estado = codigo,
            mensaje,
            // SEGURIDAD: el detalle tecnico se expone unicamente en Development.
            // En produccion la propiedad viaja nula.
            detalle = _entorno.IsDevelopment() ? excepcion.Message : null,
            traza = contexto.TraceIdentifier
        }, cancellationToken);

        return true;
    }

    // Traduccion de excepcion a codigo HTTP correcto. Devolver siempre 500 es un
    // error: el cliente no puede distinguir entre su culpa y la del servidor.
    private static (int codigo, string mensaje) MapearExcepcion(Exception excepcion) =>
        excepcion switch
        {
            // El cliente canselo o se agoto el timeout de la peticion.
            OperationCanceledException or TaskCanceledException =>
                (StatusCodes.Status408RequestTimeout,
                 "La peticion tardo demasiado y fue cancelada."),

            // Timeout de la llamada HTTP saliente.
            TimeoutException =>
                (StatusCodes.Status504GatewayTimeout,
                 "Un servicio externo no respondio en el tiempo permitido."),

            HttpRequestException =>
                (StatusCodes.Status502BadGateway,
                 "Un servicio externo devolvio un error."),

            // Violacion de llave unica o de llave foranea en SQL Server.
            DbUpdateException dbEx when EsViolacionRestriccion(dbEx) =>
                (StatusCodes.Status409Conflict,
                 "El registro entra en conflicto con datos existentes."),

            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict,
                 "El registro fue modificado por otro usuario. Volve a cargarlo."),

            DbUpdateException =>
                (StatusCodes.Status400BadRequest,
                 "Los datos enviados no se pudieron guardar."),

            ArgumentException or FormatException =>
                (StatusCodes.Status400BadRequest,
                 "Los datos enviados no tienen el formato esperado."),

            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden,
                 "No tenes permiso para esta operacion."),

            KeyNotFoundException =>
                (StatusCodes.Status404NotFound,
                 "El recurso solicitado no existe."),

            _ => (StatusCodes.Status500InternalServerError,
                  "Ocurrio un error interno. El equipo tecnico ya fue notificado.")
        };

    // Codigos de error de SQL Server:
    //   2601 / 2627 -> indice o llave unica duplicada
    //   547         -> conflicto de llave foranea o CHECK
    private static bool EsViolacionRestriccion(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException sql &&
        sql.Number is 2601 or 2627 or 547;
}
