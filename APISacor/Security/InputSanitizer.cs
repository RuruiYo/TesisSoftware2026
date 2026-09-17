using System.Text;
using System.Text.RegularExpressions;

namespace APISacor.Security;

// SEGURIDAD: sanitizacion de TODO texto que venga del cliente antes de guardarlo.
// EF Core ya parametriza las consultas, asi que la inyeccion SQL clasica esta cubierta,
// pero esta clase ataca los otros tres problemas:
//   1. XSS almacenado: el texto lo van a mostrar tres apps distintas. Si una olvida
//      escapar la salida, el script quedaria guardado en la base esperando.
//   2. Desbordamiento: se recorta a la longitud de la columna para que el motor
//      no rechace el INSERT con un error que filtre el esquema.
//   3. Caracteres de control e invisibles: usados para ocultar contenido y para
//      romper archivos CSV o logs (CRLF injection en los registros).
public static class InputSanitizer
{
    // SEGURIDAD: el timeout del Regex evita ReDoS (denegacion de servicio por
    // expresion regular con retroceso catastrofico). Va en el constructor para que
    // aplique a cada evaluacion de la expresion.
    private static readonly TimeSpan TimeoutRegex = TimeSpan.FromMilliseconds(100);

    // Etiquetas HTML y patrones de script que no tienen razon de existir en
    // un nombre, una direccion o una observacion de servicio.
    private static readonly Regex EtiquetasHtml =
        new(@"<[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeoutRegex);

    private static readonly Regex PatronesPeligrosos =
        new(@"(javascript:|vbscript:|data:text/html|on\w+\s*=)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeoutRegex);

    public static string? Limpiar(string? valor, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        // 1. Normalizacion Unicode. Sin esto, dos cadenas que se ven iguales pueden
        //    tener bytes distintos y saltarse las comparaciones de mas adelante.
        var texto = valor.Normalize(NormalizationForm.FormC);

        // 2. Se quitan caracteres de control salvo el salto de linea y el tabulador.
        var constructor = new StringBuilder(texto.Length);
        foreach (var caracter in texto)
        {
            if (char.IsControl(caracter) && caracter != '\n' && caracter != '\t') continue;
            constructor.Append(caracter);
        }
        texto = constructor.ToString();

        // 3. Se eliminan etiquetas HTML y patrones de ejecucion de script.
        try
        {
            texto = EtiquetasHtml.Replace(texto, string.Empty);
            texto = PatronesPeligrosos.Replace(texto, string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
            // Si la cadena es tan rara que agota el timeout, se descarta completa.
            return null;
        }

        // 4. Se colapsan espacios repetidos y se recorta.
        texto = texto.Trim();

        if (texto.Length == 0) return null;

        // 5. Recorte duro a la longitud de la columna.
        return texto.Length > longitudMaxima
            ? texto[..longitudMaxima]
            : texto;
    }

    // SEGURIDAD: validacion de formato para los identificadores salvadorenos.
    // Una lista blanca (lo que SI se acepta) siempre es mas segura que una
    // lista negra (lo que se intenta bloquear).
    private static readonly Regex FormatoDui =
        new(@"^\d{8}-\d$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FormatoTelefonoSv =
        new(@"^(\+503)?[\s-]?[267]\d{3}[\s-]?\d{4}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool DuiValido(string? dui) =>
        !string.IsNullOrWhiteSpace(dui) && FormatoDui.IsMatch(dui);

    public static bool TelefonoValido(string? telefono) =>
        string.IsNullOrWhiteSpace(telefono) || FormatoTelefonoSv.IsMatch(telefono);

    // SEGURIDAD: tope de paginacion. Sustituye al limite de tokens de un modelo:
    // el objetivo es el mismo, que una sola peticion no pueda pedir una respuesta
    // ilimitada y tumbar el servidor o saturar la red.
    public const int TamanoPaginaPorDefecto = 50;
    public const int TamanoPaginaMaximo = 200;

    public static int NormalizarTamanoPagina(int? solicitado)
    {
        if (solicitado is null or <= 0) return TamanoPaginaPorDefecto;
        return solicitado.Value > TamanoPaginaMaximo ? TamanoPaginaMaximo : solicitado.Value;
    }

    public static int NormalizarPagina(int? solicitada)
    {
        if (solicitada is null or <= 0) return 1;
        // Tope alto para que nadie pida la pagina 2.000.000.000 y provoque overflow
        // al calcular el Skip.
        return solicitada.Value > 100_000 ? 100_000 : solicitada.Value;
    }
}
