using System.Globalization;
using System.Text.RegularExpressions;
using APISacor.Security;

namespace APISacor.Services.Nlp;

public sealed class ConfiguracionEntidades
{
    public Dictionary<string, List<string>> Servicio { get; set; } = new();
    public Dictionary<string, List<string>> Modalidad { get; set; } = new();
    public Dictionary<string, List<string>> Frecuencia { get; set; } = new();
    public List<string> Municipios { get; set; } = new();
}

public sealed class EntidadesExtraidas
{
    public string? Servicio { get; set; }
    public string? Modalidad { get; set; }
    public string? Frecuencia { get; set; }
    public List<string> Materiales { get; } = new();
    public int? DuracionMeses { get; set; }
    public bool DuracionInvalida { get; set; }
    public DateTime? Fecha { get; set; }
    public bool FechaFueraDeRango { get; set; }
    public string? Dui { get; set; }
    public bool DuiInvalido { get; set; }
    public string? Correo { get; set; }
    public string? Volumen { get; set; }
    public string? Origen { get; set; }
    public string? Destino { get; set; }
    public string? Nombre { get; set; }
}

public sealed class ExtractorEntidades
{
    private static readonly TimeSpan Limite = TimeSpan.FromMilliseconds(100);
    private const RegexOptions Opciones = RegexOptions.CultureInvariant;

    private static readonly Dictionary<string, int> Numeros = new()
    {
        ["un"] = 1, ["una"] = 1, ["uno"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5,
        ["seis"] = 6, ["siete"] = 7, ["ocho"] = 8, ["nueve"] = 9, ["diez"] = 10, ["once"] = 11, ["doce"] = 12,
        ["dieciocho"] = 18, ["veinticuatro"] = 24, ["treinta y seis"] = 36
    };

    private static readonly string[] Meses =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private static readonly Dictionary<string, DayOfWeek> Dias = new()
    {
        ["lunes"] = DayOfWeek.Monday, ["martes"] = DayOfWeek.Tuesday, ["miercoles"] = DayOfWeek.Wednesday,
        ["jueves"] = DayOfWeek.Thursday, ["viernes"] = DayOfWeek.Friday, ["sabado"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday
    };

    private static readonly Regex PatronDuracion = new(
        @"\b(?<n>\d{1,3}|un|una|uno|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|dieciocho|veinticuatro|treinta y seis|medio)\s+(?<u>mes|meses|ano|anos)\b",
        Opciones, Limite);

    private static readonly Regex PatronFechaNumerica = new(
        @"(?<![\d/])(?<d>\d{1,2})[/-](?<m>\d{1,2})(?:[/-](?<a>\d{2,4}))?(?![\d/])", Opciones, Limite);

    private static readonly Regex PatronFechaTexto = new(
        @"\b(?<d>\d{1,2})\s+de\s+(?<m>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)(?:\s+(?:de|del)\s+(?<a>\d{4}))?\b",
        Opciones, Limite);

    private static readonly Regex PatronDia = new(
        @"\b(?:(?<prox>proximo|siguiente)\s+|el\s+|este\s+)?(?<dia>lunes|martes|miercoles|jueves|viernes|sabado|domingo)(?:\s+(?<prox2>proximo|que viene|siguiente))?\b",
        Opciones, Limite);

    private static readonly Regex PatronEnDias = new(@"\ben\s+(?<n>\d{1,2}|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|quince)\s+dias\b", Opciones, Limite);

    private static readonly Regex PatronDui = new(@"(?<!\d)(?<a>\d{8})\s*-?\s*(?<b>\d)(?!\d)", Opciones, Limite);

    private static readonly Regex PatronCorreo = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)+", Opciones, Limite);

    private static readonly Regex PatronVolumen = new(
        @"\b(?<n>\d+(?:[.,]\d+)?|un|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|media)\s+(?<u>camion|camiones|viajes?|toneladas?|ton|m3|metros? cubicos?|quintal(?:es)?|libras?|bolsas?|sacos?|volquetas?|cargas?|yardas?)\b",
        Opciones, Limite);

    private static readonly Regex PatronNombre = new(
        @"(?i)\b(?:me llamo|mi nombre es|el nombre es|a nombre de|soy|les saluda|le saluda|te saluda|le escribe|les escribe|de la empresa|somos la empresa|somos)\s+(?<n>[A-Za-zÁÉÍÓÚÑÜáéíóúñü][A-Za-zÁÉÍÓÚÑÜáéíóúñü0-9 .&'\-]{1,80}?)(?=\s*(?:[,.;!?]|$)|\s+(?:y|necesit\w*|quier\w*|quisier\w*|busc\w*|para|con|desde|ocup\w*|tenemos|tengo)\b)",
        Opciones, Limite);

    private readonly ConfiguracionEntidades _config;
    private readonly List<(string Normalizado, string Visible)> _municipios;

    public ExtractorEntidades(ConfiguracionEntidades config)
    {
        _config = config;
        _municipios = config.Municipios
            .Select(m => (ProcesadorTexto.Normalizar(m), m))
            .OrderByDescending(m => m.Item1.Length)
            .ToList();
    }

    public EntidadesExtraidas Extraer(string texto, IReadOnlyList<string> materiales, DateTime hoy)
    {
        var e = new EntidadesExtraidas();
        var n = ProcesadorTexto.Normalizar(texto);
        if (n.Length == 0) return e;

        e.Correo = PatronCorreo.Match(texto) is { Success: true } correo ? correo.Value.ToLowerInvariant() : null;
        var sinCorreo = e.Correo is null ? n : n.Replace(e.Correo, " ");

        LeerDui(sinCorreo, e);
        var sinDui = e.Dui is null ? sinCorreo : PatronDui.Replace(sinCorreo, " ");

        e.Servicio = BuscarSinonimo(sinDui, _config.Servicio);
        e.Frecuencia = BuscarSinonimo(sinDui, _config.Frecuencia);
        LeerDuracion(sinDui, e);
        e.Modalidad = e.Frecuencia is not null || e.DuracionMeses is not null
            ? "recurrente"
            : BuscarSinonimo(sinDui, _config.Modalidad);

        LeerFecha(sinDui, hoy, e);
        LeerMateriales(sinDui, materiales, e);
        LeerLugares(sinDui, e);

        var volumen = PatronVolumen.Match(sinDui);
        if (volumen.Success) e.Volumen = volumen.Value;

        LeerNombre(texto, e);
        return e;
    }

    public static string? NombreDesdeTexto(string texto)
    {
        var m = PatronNombre.Match(texto);
        return m.Success ? m.Groups["n"].Value.Trim() : null;
    }

    private static string? BuscarSinonimo(string n, Dictionary<string, List<string>> valores)
    {
        string? elegido = null;
        var mejorPosicion = int.MaxValue;
        var mejorLargo = 0;
        foreach (var (valor, sinonimos) in valores)
            foreach (var sinonimo in sinonimos)
            {
                var s = ProcesadorTexto.Normalizar(sinonimo);
                var m = Regex.Match(n, $@"\b{Regex.Escape(s)}\b", Opciones, Limite);
                if (!m.Success) continue;
                if (s.Length > mejorLargo || (s.Length == mejorLargo && m.Index < mejorPosicion))
                {
                    elegido = valor;
                    mejorLargo = s.Length;
                    mejorPosicion = m.Index;
                }
            }
        return elegido;
    }

    private static void LeerDui(string n, EntidadesExtraidas e)
    {
        var m = PatronDui.Match(n);
        if (!m.Success) return;
        var dui = $"{m.Groups["a"].Value}-{m.Groups["b"].Value}";
        if (InputSanitizer.DuiValido(dui)) e.Dui = dui;
        else e.DuiInvalido = true;
    }

    private static void LeerDuracion(string n, EntidadesExtraidas e)
    {
        var m = PatronDuracion.Match(n);
        if (!m.Success) return;
        var texto = m.Groups["n"].Value;
        int valor;
        if (texto == "medio") valor = 6;
        else if (!int.TryParse(texto, out valor)) valor = Numeros.GetValueOrDefault(texto, 0);
        var unidad = m.Groups["u"].Value;
        if (texto == "medio" && unidad.StartsWith("mes", StringComparison.Ordinal)) return;
        if (unidad.StartsWith("ano", StringComparison.Ordinal) && texto != "medio") valor *= 12;
        if (valor is >= 1 and <= 36) e.DuracionMeses = valor;
        else e.DuracionInvalida = true;
    }

    private static void LeerFecha(string n, DateTime hoy, EntidadesExtraidas e)
    {
        DateTime? fecha = null;

        var numerica = PatronFechaNumerica.Match(n);
        var texto = PatronFechaTexto.Match(n);
        if (numerica.Success)
        {
            var dia = int.Parse(numerica.Groups["d"].Value, CultureInfo.InvariantCulture);
            var mes = int.Parse(numerica.Groups["m"].Value, CultureInfo.InvariantCulture);
            int? anio = numerica.Groups["a"].Success ? int.Parse(numerica.Groups["a"].Value, CultureInfo.InvariantCulture) : null;
            if (anio is < 100) anio += 2000;
            fecha = Construir(dia, mes, anio, hoy);
            if (fecha is null) { e.FechaFueraDeRango = true; return; }
        }
        else if (texto.Success)
        {
            var nombreMes = texto.Groups["m"].Value == "setiembre" ? "septiembre" : texto.Groups["m"].Value;
            var mes = Array.IndexOf(Meses, nombreMes) + 1;
            int? anio = texto.Groups["a"].Success ? int.Parse(texto.Groups["a"].Value, CultureInfo.InvariantCulture) : null;
            fecha = Construir(int.Parse(texto.Groups["d"].Value, CultureInfo.InvariantCulture), mes, anio, hoy);
            if (fecha is null) { e.FechaFueraDeRango = true; return; }
        }
        else if (Regex.IsMatch(n, @"\bpasado\s+manana\b", Opciones, Limite))
            fecha = hoy.AddDays(2);
        else if (Regex.IsMatch(n, @"(?<!\bla\s)(?<!\bde\s)\bmanana\b", Opciones, Limite))
            fecha = hoy.AddDays(1);
        else if (Regex.IsMatch(n, @"\b(?:ayer|anteayer|antier)\b", Opciones, Limite))
            fecha = hoy.AddDays(-1);
        else if (Regex.IsMatch(n, @"\b(?:hoy|hoy mismo|este dia)\b", Opciones, Limite))
            fecha = hoy;
        else if (Regex.IsMatch(n, @"\b(?:proxima semana|semana que viene|la otra semana|siguiente semana)\b", Opciones, Limite))
            fecha = Siguiente(hoy, DayOfWeek.Monday);
        else if (Regex.IsMatch(n, @"\b(?:proximo mes|mes que viene|el otro mes|siguiente mes)\b", Opciones, Limite))
            fecha = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1);
        else if (PatronEnDias.Match(n) is { Success: true } enDias)
        {
            var valor = enDias.Groups["n"].Value;
            var dias = int.TryParse(valor, out var d) ? d : valor == "quince" ? 15 : Numeros.GetValueOrDefault(valor, 0);
            fecha = hoy.AddDays(dias);
        }
        else if (PatronDia.Match(n) is { Success: true } dia)
        {
            fecha = Siguiente(hoy, Dias[dia.Groups["dia"].Value]);
        }

        if (fecha is null) return;
        if (fecha.Value.Date < hoy.Date || fecha.Value.Date > hoy.Date.AddYears(2))
            e.FechaFueraDeRango = true;
        else
            e.Fecha = fecha.Value.Date;
    }

    private static DateTime? Construir(int dia, int mes, int? anio, DateTime hoy)
    {
        if (mes is < 1 or > 12 || dia < 1) return null;
        var a = anio ?? hoy.Year;
        if (dia > DateTime.DaysInMonth(a, mes)) return null;
        var fecha = new DateTime(a, mes, dia);
        if (anio is null && fecha < hoy.Date) fecha = fecha.AddYears(1);
        return fecha;
    }

    private static DateTime Siguiente(DateTime hoy, DayOfWeek dia)
    {
        var diferencia = ((int)dia - (int)hoy.DayOfWeek + 7) % 7;
        return hoy.Date.AddDays(diferencia == 0 ? 7 : diferencia);
    }

    private static void LeerMateriales(string n, IReadOnlyList<string> materiales, EntidadesExtraidas e)
    {
        var raicesTexto = ProcesadorTexto.Raices(n).ToHashSet(StringComparer.Ordinal);
        foreach (var material in materiales)
        {
            var nm = ProcesadorTexto.Normalizar(material);
            var raices = ProcesadorTexto.Raices(material);
            var porTexto = Regex.IsMatch(n, $@"\b{Regex.Escape(nm)}(?:s|es)?\b", Opciones, Limite);
            var porRaiz = raices.Count > 0 && raices.All(raicesTexto.Contains);
            if ((porTexto || porRaiz) && !e.Materiales.Contains(material))
                e.Materiales.Add(material);
        }
    }

    private void LeerLugares(string n, EntidadesExtraidas e)
    {
        var encontrados = new List<(int Inicio, int Fin, string Visible)>();
        foreach (var (normalizado, visible) in _municipios)
            foreach (Match m in Regex.Matches(n, $@"\b{Regex.Escape(normalizado)}\b", Opciones, Limite))
                if (!encontrados.Any(x => m.Index < x.Fin && x.Inicio < m.Index + m.Length))
                    encontrados.Add((m.Index, m.Index + m.Length, visible));

        encontrados = encontrados.OrderBy(x => x.Inicio).ToList();
        if (encontrados.Count == 0) return;

        bool EsDestino((int Inicio, int Fin, string Visible) lugar) =>
            Regex.IsMatch(n[..lugar.Inicio], @"\b(?:a|al|hacia|hasta|para|rumbo a)\s+(?:la\s+|el\s+)?$", Opciones, Limite);

        if (encontrados.Count >= 2)
        {
            var destino = encontrados.FirstOrDefault(EsDestino);
            if (destino.Visible is null)
            {
                e.Origen = encontrados[0].Visible;
                e.Destino = encontrados[1].Visible;
            }
            else
            {
                e.Destino = destino.Visible;
                e.Origen = encontrados.First(x => x != destino).Visible;
            }
            return;
        }

        if (EsDestino(encontrados[0])) e.Destino = encontrados[0].Visible;
        else e.Origen = encontrados[0].Visible;
    }

    private void LeerNombre(string texto, EntidadesExtraidas e)
    {
        var nombre = NombreDesdeTexto(texto);
        if (nombre is null) return;
        var nn = ProcesadorTexto.Normalizar(nombre);
        if (Regex.IsMatch(nn, @"^(?:de|del|el|la|los|las|un|una|muy|tu|su|yo|parte)\b", Opciones, Limite)) return;
        if (_municipios.Any(m => m.Normalizado == nn)) return;
        if (nombre.Length is < 3 or > 150) return;
        e.Nombre = nombre;
    }
}
