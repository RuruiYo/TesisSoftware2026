namespace APISacor.Security;

// SEGURIDAD: las llaves NUNCA estan escritas en el codigo. Esta clase solo declara
// la forma de la configuracion; los valores reales llegan desde IConfiguration, que
// en tiempo de ejecucion los lee (en orden de prioridad) de:
//   1. Variables de entorno       -> Seguridad__ApiKeys__AppWeb=valor
//   2. User Secrets en desarrollo -> dotnet user-secrets set "Seguridad:ApiKeys:AppWeb" "valor"
//   3. appsettings.json           -> solo para dejar la estructura visible, sin secretos
// Asi la llave no queda en el repositorio ni en el binario compilado.
public class ApiKeyOptions
{
    public const string SeccionConfiguracion = "Seguridad";

    // Nombre del header donde cada app envia su llave.
    public string NombreHeader { get; set; } = "X-Api-Key";

    // Diccionario nombreApp -> llave. Permite revocar una sola app sin tocar las otras
    // y saber en los logs cual de las tres consumidoras hizo cada peticion.
    public Dictionary<string, string> ApiKeys { get; set; } = new();

    // Rutas que no exigen llave (Swagger y health check).
    public string[] RutasPublicas { get; set; } =
    {
        "/swagger",
        "/health"
    };
}
