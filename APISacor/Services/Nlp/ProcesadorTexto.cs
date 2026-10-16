using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace APISacor.Services.Nlp;

public static class ProcesadorTexto
{
    private static readonly TimeSpan Limite = TimeSpan.FromMilliseconds(100);
    private static readonly Regex Separadores = new(@"[^a-z0-9@./\-]+", RegexOptions.CultureInvariant, Limite);

    public static readonly HashSet<string> PalabrasVacias = new(StringComparer.Ordinal)
    {
        "a", "al", "algo", "algun", "alguna", "algunas", "alguno", "algunos", "ante", "antes", "aqui", "asi",
        "aun", "cada", "como", "con", "contra", "cual", "cuales", "de", "del", "desde", "donde", "durante",
        "e", "el", "ella", "ellas", "ellos", "en", "entre", "era", "es", "esa", "esas", "ese", "eso", "esos",
        "esta", "estas", "este", "esto", "estos", "fue", "ha", "hay", "la", "las", "le", "les", "lo", "los",
        "me", "mi", "mis", "mucho", "muy", "nos", "nosotros", "o", "os", "otra", "otro", "para", "pero", "por",
        "porque", "que", "se", "sea", "ser", "sin", "sobre", "su", "sus", "tambien", "te", "ti", "tu", "tus",
        "u", "un", "una", "unas", "uno", "unos", "usted", "ustedes", "vos", "y", "ya", "yo"
    };

    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ", RegexOptions.None, Limite);
    }

    public static List<string> Tokenizar(string textoNormalizado) =>
        Separadores.Split(textoNormalizado)
            .Select(t => t.Trim('.', '/', '-'))
            .Where(t => t.Length > 0)
            .ToList();

    public static List<string> QuitarPalabrasVacias(IEnumerable<string> tokens) =>
        tokens.Where(t => !PalabrasVacias.Contains(t)).ToList();

    public static List<string> Raices(string? texto)
    {
        var tokens = QuitarPalabrasVacias(Tokenizar(Normalizar(texto)));
        return tokens.Select(StemmerEspanol.Raiz).Where(r => r.Length > 0).ToList();
    }
}
