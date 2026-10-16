namespace APISacor.Services.Nlp;

public sealed record Intencion(string Nombre, double Confianza);

public sealed class ClasificadorIntenciones
{
    private readonly Dictionary<string, double> _idf = new(StringComparer.Ordinal);
    private readonly List<(string Intencion, Dictionary<string, double> Vector)> _ejemplos = new();
    private double _idfDesconocida = 1.0;

    public int TotalEjemplos => _ejemplos.Count;
    public int TamanoVocabulario => _idf.Count;

    public void Entrenar(IReadOnlyDictionary<string, IReadOnlyList<string>> datos)
    {
        _idf.Clear();
        _ejemplos.Clear();

        var documentos = datos
            .SelectMany(par => par.Value.Select(frase => (Intencion: par.Key, Raices: ProcesadorTexto.Raices(frase))))
            .Where(d => d.Raices.Count > 0)
            .ToList();

        var frecuenciaDocumentos = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var doc in documentos)
            foreach (var raiz in doc.Raices.Distinct())
                frecuenciaDocumentos[raiz] = frecuenciaDocumentos.GetValueOrDefault(raiz) + 1;

        var total = documentos.Count;
        foreach (var (raiz, df) in frecuenciaDocumentos)
            _idf[raiz] = Math.Log((1.0 + total) / (1.0 + df)) + 1.0;
        _idfDesconocida = Math.Log(1.0 + total) + 1.0;

        foreach (var doc in documentos)
            _ejemplos.Add((doc.Intencion, Vectorizar(doc.Raices, false)));
    }

    public Intencion Predecir(string? texto)
    {
        var vector = Vectorizar(ProcesadorTexto.Raices(texto), true);
        if (vector.Count == 0)
            return new Intencion(string.Empty, 0);

        var mejor = new Intencion(string.Empty, 0);
        foreach (var (intencion, ejemplo) in _ejemplos)
        {
            var similitud = Coseno(vector, ejemplo);
            if (similitud > mejor.Confianza)
                mejor = new Intencion(intencion, similitud);
        }
        return mejor with { Confianza = Math.Round(mejor.Confianza, 3) };
    }

    private Dictionary<string, double> Vectorizar(IEnumerable<string> raices, bool incluirDesconocidas)
    {
        var vector = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var raiz in raices)
        {
            if (_idf.TryGetValue(raiz, out var idf))
                vector[raiz] = vector.GetValueOrDefault(raiz) + idf;
            else if (incluirDesconocidas)
                vector[raiz] = vector.GetValueOrDefault(raiz) + _idfDesconocida;
        }

        var norma = Math.Sqrt(vector.Values.Sum(v => v * v));
        if (norma == 0) return new Dictionary<string, double>();
        foreach (var clave in vector.Keys.ToList())
            vector[clave] /= norma;
        return vector;
    }

    private static double Coseno(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        var (menor, mayor) = a.Count <= b.Count ? (a, b) : (b, a);
        var suma = 0.0;
        foreach (var (clave, valor) in menor)
            if (mayor.TryGetValue(clave, out var otro))
                suma += valor * otro;
        return suma;
    }
}
