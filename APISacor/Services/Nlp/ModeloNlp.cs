using System.Text.Json;

namespace APISacor.Services.Nlp;

public sealed class DefinicionIntencion
{
    public string Nombre { get; set; } = string.Empty;
    public List<string> Ejemplos { get; set; } = new();
    public List<string> Respuestas { get; set; } = new();
}

public sealed class ArchivoIntenciones
{
    public double UmbralConfianza { get; set; } = 0.5;
    public List<DefinicionIntencion> Intenciones { get; set; } = new();
}

public sealed class ModeloNlp
{
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public ClasificadorIntenciones Clasificador { get; }
    public ExtractorEntidades Extractor { get; }
    public double Umbral { get; }
    public IReadOnlyDictionary<string, List<string>> Respuestas { get; }

    public ModeloNlp(ArchivoIntenciones intenciones, ConfiguracionEntidades entidades)
    {
        Umbral = intenciones.UmbralConfianza;
        Clasificador = new ClasificadorIntenciones();
        Clasificador.Entrenar(intenciones.Intenciones.ToDictionary(
            i => i.Nombre,
            i => (IReadOnlyList<string>)i.Ejemplos));
        Respuestas = intenciones.Intenciones.ToDictionary(i => i.Nombre, i => i.Respuestas);
        Extractor = new ExtractorEntidades(entidades);
    }

    public static ModeloNlp Cargar(string carpeta)
    {
        var intenciones = JsonSerializer.Deserialize<ArchivoIntenciones>(
            File.ReadAllText(Path.Combine(carpeta, "intenciones.json")), OpcionesJson)
            ?? throw new InvalidOperationException("intenciones.json está vacío.");
        var entidades = JsonSerializer.Deserialize<ConfiguracionEntidades>(
            File.ReadAllText(Path.Combine(carpeta, "entidades.json")), OpcionesJson)
            ?? throw new InvalidOperationException("entidades.json está vacío.");
        return new ModeloNlp(intenciones, entidades);
    }

    public string? Respuesta(string intencion)
    {
        if (!Respuestas.TryGetValue(intencion, out var lista) || lista.Count == 0) return null;
        return lista[Random.Shared.Next(lista.Count)];
    }
}
