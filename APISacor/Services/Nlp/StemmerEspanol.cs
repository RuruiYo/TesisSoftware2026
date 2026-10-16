namespace APISacor.Services.Nlp;

public static class StemmerEspanol
{
    private const string Vocales = "aeiou";

    private static readonly string[] Pronombres =
        { "selas", "selos", "sela", "selo", "las", "les", "los", "nos", "me", "se", "la", "le", "lo" };

    private static readonly string[] AntesDePronombre = { "iendo", "ando", "ar", "er", "ir" };

    private static readonly string[] Paso2bGrupoGu = { "emos", "en", "es" };

    private static readonly string[] Paso2bGrupo =
    {
        "aremos", "eremos", "iremos", "aba", "ada", "ida", "ara", "iera", "ad", "ed", "id", "ase", "iese",
        "aste", "iste", "an", "aban", "aran", "ieran", "asen", "iesen", "aron", "ieron", "ado", "ido", "ando",
        "iendo", "ar", "er", "ir", "as", "abas", "adas", "idas", "aras", "ieras", "ases", "ieses", "abais",
        "arais", "ierais", "aseis", "ieseis", "asteis", "isteis", "ados", "idos", "amos", "imos"
    };

    public static string Raiz(string palabra)
    {
        if (palabra.Length < 3) return palabra;

        var p = palabra;
        var rv = RegionRv(p);
        var r1 = RegionR(p, 0);
        var r2 = RegionR(p, r1);

        p = QuitarPronombre(p, rv);

        var antes = p;
        p = Paso1(p, r1, r2);
        if (p == antes)
        {
            p = Paso2a(p, rv);
            if (p == antes)
                p = Paso2b(p, rv);
        }

        return Paso3(p, rv);
    }

    private static bool EsVocal(char c) => Vocales.IndexOf(c) >= 0;

    private static int RegionRv(string p)
    {
        if (p.Length < 2) return p.Length;
        if (!EsVocal(p[1]))
        {
            for (var i = 2; i < p.Length; i++)
                if (EsVocal(p[i])) return i + 1;
            return p.Length;
        }
        if (EsVocal(p[0]) && EsVocal(p[1]))
        {
            for (var i = 2; i < p.Length; i++)
                if (!EsVocal(p[i])) return i + 1;
            return p.Length;
        }
        return Math.Min(3, p.Length);
    }

    private static int RegionR(string p, int desde)
    {
        for (var i = desde + 1; i < p.Length; i++)
            if (!EsVocal(p[i]) && EsVocal(p[i - 1])) return i + 1;
        return p.Length;
    }

    private static bool En(string p, string sufijo, int region) =>
        p.EndsWith(sufijo, StringComparison.Ordinal) && p.Length - sufijo.Length >= region;

    private static string Cortar(string p, string sufijo) => p[..^sufijo.Length];

    private static string QuitarPronombre(string p, int rv)
    {
        foreach (var pron in Pronombres)
        {
            if (!En(p, pron, rv)) continue;
            var resto = Cortar(p, pron);
            foreach (var verbo in AntesDePronombre)
                if (resto.EndsWith(verbo, StringComparison.Ordinal) && resto.Length - verbo.Length >= rv)
                    return resto;
            if (resto.EndsWith("yendo", StringComparison.Ordinal) && resto.Length >= 6 && resto[^6] == 'u')
                return resto;
            return p;
        }
        return p;
    }

    private static string Paso1(string p, int r1, int r2)
    {
        string[] grupoA = { "amientos", "imientos", "amiento", "imiento", "anzas", "ismos", "ables", "ibles", "istas", "anza",
                            "icos", "icas", "ismo", "able", "ible", "ista", "osos", "osas", "ico", "ica", "oso", "osa" };
        foreach (var s in grupoA)
            if (En(p, s, r2)) return Cortar(p, s);

        string[] grupoB = { "aciones", "adoras", "adores", "ancias", "acion", "adora", "antes", "ancia", "ador", "ante" };
        foreach (var s in grupoB)
            if (En(p, s, r2))
            {
                var q = Cortar(p, s);
                return En(q, "ic", r2) ? Cortar(q, "ic") : q;
            }

        if (En(p, "logias", r2)) return Cortar(p, "logias") + "log";
        if (En(p, "logia", r2)) return Cortar(p, "logia") + "log";
        if (En(p, "uciones", r2)) return Cortar(p, "uciones") + "u";
        if (En(p, "ucion", r2)) return Cortar(p, "ucion") + "u";
        if (En(p, "encias", r2)) return Cortar(p, "encias") + "ente";
        if (En(p, "encia", r2)) return Cortar(p, "encia") + "ente";

        if (En(p, "amente", r1))
        {
            var q = Cortar(p, "amente");
            if (En(q, "iv", r2))
            {
                q = Cortar(q, "iv");
                return En(q, "at", r2) ? Cortar(q, "at") : q;
            }
            foreach (var s in new[] { "os", "ic", "ad" })
                if (En(q, s, r2)) return Cortar(q, s);
            return q;
        }

        if (En(p, "mente", r2))
        {
            var q = Cortar(p, "mente");
            foreach (var s in new[] { "ante", "able", "ible" })
                if (En(q, s, r2)) return Cortar(q, s);
            return q;
        }

        foreach (var s in new[] { "idades", "idad" })
            if (En(p, s, r2))
            {
                var q = Cortar(p, s);
                foreach (var t in new[] { "abil", "ic", "iv" })
                    if (En(q, t, r2)) return Cortar(q, t);
                return q;
            }

        foreach (var s in new[] { "ivas", "ivos", "iva", "ivo" })
            if (En(p, s, r2))
            {
                var q = Cortar(p, s);
                return En(q, "at", r2) ? Cortar(q, "at") : q;
            }

        return p;
    }

    private static string Paso2a(string p, int rv)
    {
        string[] sufijos = { "yeron", "yendo", "yamos", "yais", "yan", "yen", "yas", "yes", "ya", "ye", "yo" };
        foreach (var s in sufijos)
            if (En(p, s, rv) && p.Length > s.Length && p[^(s.Length + 1)] == 'u')
                return Cortar(p, s);
        return p;
    }

    private static string Paso2b(string p, int rv)
    {
        var mejor = Paso2bGrupoGu.Concat(Paso2bGrupo)
            .Where(x => En(p, x, rv))
            .OrderByDescending(x => x.Length)
            .FirstOrDefault();
        if (mejor is null) return p;
        var q = Cortar(p, mejor);
        if (Paso2bGrupoGu.Contains(mejor) && q.EndsWith("gu", StringComparison.Ordinal))
            return q[..^1];
        return q;
    }

    private static string Paso3(string p, int rv)
    {
        foreach (var s in new[] { "os", "a", "o" })
            if (En(p, s, rv)) return Cortar(p, s);
        if (En(p, "e", rv))
        {
            var q = Cortar(p, "e");
            return q.EndsWith("gu", StringComparison.Ordinal) && q.Length - 1 >= rv ? q[..^1] : q;
        }
        return p;
    }
}
