namespace APISacor.Services;

public static class ReglasGastoViaje
{
    public static readonly string[] Conceptos = { "Combustible", "Peaje", "Alimentacion", "Mantenimiento", "Otro" };

    public static string? Normalizar(string? concepto) =>
        Conceptos.FirstOrDefault(c => c.Equals(concepto?.Trim(), StringComparison.OrdinalIgnoreCase));

    public const decimal LibrasPorTonelada = 2200m;

    // SEGURIDAD: una carga de un solo viaje no puede pasar de 45 toneladas
    // (100000 libras, el mismo tope que valida el CRUD de pesajes).
    public const decimal MaxToneladasPorCarga = 45m;
}
