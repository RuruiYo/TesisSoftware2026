namespace APISacor.Services;

// Valores de la columna limpieza.etapa que usan las apps moviles.
public static class EtapasEvidencia
{
    public const string Limpieza = "Evidencia de limpieza";
    public const string Ruta = "Evidencia de ruta";
    public const string InformacionAdicional = "Información adicional";
    public const string Recoleccion = "Recolección finalizada";

    public static readonly string[] DelTransportista = { Limpieza, Ruta };
}

public static class ValidadorFoto
{
    // SEGURIDAD: tope por foto ya decodificada. Evita que un celular llene la base
    // con archivos enormes o mande un cuerpo gigante para agotar memoria (DoS).
    public const int MaxBytes = 3 * 1024 * 1024;

    // SEGURIDAD: limite de fotos por evidencia para acotar el crecimiento de la tabla.
    public const int MaxFotosPorEvidencia = 3;

    // SEGURIDAD: el limite del cuerpo HTTP considera el +33 % del base64 mas el JSON.
    public const long MaxCuerpoPeticion = 5 * 1024 * 1024;

    // SEGURIDAD: lista blanca de formatos. No se confia en el tipo que declara el
    // cliente: se revisan los "magic bytes" del archivo. Asi no se puede guardar
    // un ejecutable o un HTML renombrado como foto y servirlo despues.
    public static string? DetectarTipo(byte[] datos)
    {
        if (datos.Length > 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF)
            return "image/jpeg";

        if (datos.Length > 8 &&
            datos[0] == 0x89 && datos[1] == 0x50 && datos[2] == 0x4E && datos[3] == 0x47 &&
            datos[4] == 0x0D && datos[5] == 0x0A && datos[6] == 0x1A && datos[7] == 0x0A)
            return "image/png";

        return null;
    }

    // SEGURIDAD: decodificacion defensiva. Se rechaza el texto antes de decodificar si
    // ya excede el tamano maximo posible, y se usa TryFromBase64String para no lanzar
    // excepciones con entradas malformadas.
    public static byte[]? Decodificar(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        var limpio = base64.Trim();
        var coma = limpio.IndexOf(',');
        if (limpio.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma > 0)
            limpio = limpio[(coma + 1)..];

        if (limpio.Length > (MaxBytes / 3 + 1) * 4)
            return null;

        var buffer = new byte[limpio.Length * 3 / 4 + 3];
        if (!Convert.TryFromBase64String(limpio, buffer, out var escritos))
            return null;

        return escritos == 0 ? null : buffer[..escritos];
    }
}
