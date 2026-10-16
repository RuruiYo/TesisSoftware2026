using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace APISacor.Services.Nlp;

public sealed class EstadoConversacion
{
    public string Paso { get; set; } = "inicio";
    public Dictionary<string, string> Datos { get; set; } = new();
}

public sealed class ResultadoTurno
{
    public List<string> Mensajes { get; } = new();
    public bool Confirmada { get; set; }
    public string Intencion { get; set; } = string.Empty;
    public double Confianza { get; set; }
}

public sealed class MotorConversacion
{
    public static readonly string[] Servicios = { "Acarreo de materiales", "Recoleccion de desechos", "Reciclaje", "Especifico" };
    public static readonly string[] ServiciosVisibles = { "Acarreo de materiales", "Recolección de desechos", "Reciclaje", "Específico" };
    public static readonly string[] Frecuencias = { "Diaria", "Semanal", "Quincenal", "Mensual", "A demanda" };
    private static readonly HashSet<string> ConDestino = new() { "Acarreo de materiales", "Especifico" };
    private static readonly HashSet<string> Opcionales = new() { "materiales", "fecha", "volumen", "correo", "descripcion" };
    private static readonly HashSet<string> TextoLibre = new() { "lugar", "destino", "nombre", "volumen", "descripcion" };
    private static readonly HashSet<string> Informativas = new()
        { "consultar_servicios", "consultar_precio", "consultar_horario", "consultar_contacto", "identidad_bot", "fuera_de_tema", "agradecimiento" };
    private static readonly HashSet<string> PalabrasOmitir = new()
        { "omitir", "no", "ninguno", "ninguna", "nada", "no se", "no tengo", "no aplica", "n/a", "na", "saltar", "siguiente", "paso" };
    private static readonly TimeSpan Limite = TimeSpan.FromMilliseconds(100);

    private static readonly (string Dato, string Patron)[] CamposCorregibles =
    {
        ("fecha", @"\bfecha\b|\bdia\b"), ("nombre", @"\bnombre\b"), ("dui", @"\bdui\b"), ("correo", @"\bcorreo\b|\bemail\b"),
        ("destino", @"\bdestino\b"), ("lugar", @"\blugar\b|\borigen\b|\brecoleccion\b|\bdireccion\b"),
        ("servicio", @"\bservicio\b"), ("materiales", @"\bmateriales?\b"), ("frecuencia", @"\bfrecuencia\b"),
        ("duracion", @"\bduracion\b|\bmeses\b"), ("volumen", @"\bvolumen\b|\bcantidad\b"), ("descripcion", @"\bdetalles?\b|\bdescripcion\b")
    };

    private readonly ModeloNlp _modelo;

    public MotorConversacion(ModeloNlp modelo) => _modelo = modelo;

    public ResultadoTurno Procesar(EstadoConversacion estado, string texto, IReadOnlyList<string> materiales, DateTime hoy)
    {
        var r = new ResultadoTurno();
        var n = ProcesadorTexto.Normalizar(texto).Trim('.', '!', '*', ' ');
        var intencion = _modelo.Clasificador.Predecir(texto);
        r.Intencion = intencion.Nombre;
        r.Confianza = intencion.Confianza;
        var segura = intencion.Confianza >= _modelo.Umbral;
        bool Es(string nombre) => segura && intencion.Nombre == nombre;

        if (estado.Paso == "asesor")
        {
            if (n is "asistente" or "bot" or "menu" or "inicio" || Es("solicitar_cotizacion"))
            {
                Reiniciar(estado);
                r.Mensajes.Add(Saludo());
            }
            return r;
        }

        if (n == "cancelar" || (Es("cancelar") && n.Split(' ').Length <= 5))
        {
            Reiniciar(estado);
            r.Mensajes.Add(_modelo.Respuesta("cancelar")!);
            return r;
        }

        if (Es("hablar_asesor"))
        {
            Reiniciar(estado);
            estado.Paso = "asesor";
            r.Mensajes.Add(_modelo.Respuesta("hablar_asesor")!);
            return r;
        }

        var datos = estado.Datos;
        var e = _modelo.Extractor.Extraer(texto, materiales, hoy);

        if (estado.Paso == "confirmar")
        {
            Confirmar(estado, r, n, e, materiales, intencion, segura);
            return r;
        }

        var pendiente = estado.Paso.StartsWith("pregunta:", StringComparison.Ordinal) ? estado.Paso[9..] : null;
        var empezando = pendiente is null;
        if (pendiente is "lugar" or "destino" && (e.Origen is null || e.Destino is null)
            && e.Fecha is null && e.Materiales.Count == 0 && e.Volumen is null)
        {
            e.Origen = null;
            e.Destino = null;
        }
        var anotado = Aplicar(e, datos, pendiente, materiales);

        if (anotado.Count == 0 && pendiente is not null && IntentarCorreccion(estado, n, materiales, r))
            return r;

        if (pendiente is not null && !Tiene(datos, pendiente))
        {
            var quiereOmitir = PalabrasOmitir.Contains(n) || Es("omitir");
            if (Opcionales.Contains(pendiente) && quiereOmitir)
            {
                Omitir(datos, pendiente);
                anotado.Add(string.Empty);
            }
            else if (quiereOmitir && anotado.Count == 0)
            {
                r.Mensajes.Add("Este dato es necesario para preparar tu cotización. " + Pregunta(pendiente, datos, materiales));
                return r;
            }
            else if (TextoLibre.Contains(pendiente) && PuedeSerTextoLibre(pendiente, intencion, segura))
            {
                if (AceptarTextoLibre(pendiente, texto, datos)) anotado.Add(string.Empty);
            }
            else if (n.All(char.IsDigit) && n.Length > 0 && ElegirPorNumero(pendiente, int.Parse(n, CultureInfo.InvariantCulture), datos, materiales))
            {
                anotado.Add(string.Empty);
            }
        }

        if (anotado.Count > 0)
        {
            var visibles = anotado.Where(a => a.Length > 0).ToList();
            var prefijo = visibles.Count > 0 ? "Anotado: " + string.Join(", ", visibles) + ".\n\n" : Acuse(pendiente);
            if (empezando) prefijo = "¡Con gusto te ayudo a planificar tu servicio!\n\n" + prefijo;
            r.Mensajes.Add(prefijo + Avanzar(estado, materiales));
            return r;
        }

        var enCurso = pendiente is not null;
        if (segura && Informativas.Contains(intencion.Nombre))
        {
            var respuesta = _modelo.Respuesta(intencion.Nombre)!;
            r.Mensajes.Add(enCurso
                ? respuesta + "\n\nSigamos con tu solicitud. " + Pregunta(pendiente!, datos, materiales)
                : respuesta + "\n\n¿Te ayudo a planificar un servicio? Cuéntame qué necesitas.");
            return r;
        }

        if (Es("despedida"))
        {
            if (enCurso)
                r.Mensajes.Add("Antes de irte: tu solicitud aún no está enviada. " + Pregunta(pendiente!, datos, materiales) + "\nSi prefieres dejarla, escribe *cancelar*.");
            else
                r.Mensajes.Add(_modelo.Respuesta("despedida")!);
            return r;
        }

        if (!enCurso && (Es("solicitar_cotizacion") || Es("afirmar")))
        {
            r.Mensajes.Add("¡Claro! Vamos a planificar tu servicio paso a paso. Puedes escribir *cancelar* en cualquier momento.\n\n" + Avanzar(estado, materiales));
            return r;
        }

        if (Es("saludo"))
        {
            r.Mensajes.Add(enCurso ? "¡Hola de nuevo! " + Pregunta(pendiente!, datos, materiales) : Saludo());
            if (!enCurso) estado.Paso = "inicio";
            return r;
        }

        if (enCurso)
        {
            r.Mensajes.Add(ErrorDe(pendiente!, e, texto) + Pregunta(pendiente!, datos, materiales));
            return r;
        }

        r.Mensajes.Add(estado.Paso == "inicio" && datos.Count == 0 && string.IsNullOrEmpty(intencion.Nombre) && n.Length <= 12
            ? Saludo()
            : $"Disculpa, no estoy seguro de entender \"{Recortar(texto)}\". Soy el asistente de *SACOR* y puedo ayudarte a planificar y cotizar acarreos de materiales, recolección de desechos y reciclaje.\n\nPor ejemplo, escribe: _necesito llevar ripio de Soyapango a Apopa el lunes_.");
        return r;
    }

    private void Confirmar(EstadoConversacion estado, ResultadoTurno r, string n, EntidadesExtraidas e,
        IReadOnlyList<string> materiales, Intencion intencion, bool segura)
    {
        var datos = estado.Datos;
        var cambios = Aplicar(e, datos, null, materiales);
        if (cambios.Count > 0)
        {
            var siguiente = Siguiente(datos, materiales);
            r.Mensajes.Add("Listo, actualicé: " + string.Join(", ", cambios) + ".\n\n" +
                (siguiente is null ? Resumen(datos) + "\n\n" + PreguntaConfirmar() : Avanzar(estado, materiales)));
            return;
        }

        if (IntentarCorreccion(estado, n, materiales, r))
            return;

        if (n is "1" or "si" or "s" || (segura && intencion.Nombre == "afirmar"))
        {
            r.Confirmada = true;
            return;
        }

        if (n is "2" || (segura && intencion.Nombre == "negar"))
        {
            r.Mensajes.Add("Sin problema. ¿Qué dato quieres cambiar? Escríbeme el dato correcto, por ejemplo _la fecha es 30/11/2026_ o _quiero cambiar el destino_. Si prefieres descartar la solicitud, escribe *cancelar*.");
            return;
        }

        if (segura && Informativas.Contains(intencion.Nombre))
        {
            r.Mensajes.Add(_modelo.Respuesta(intencion.Nombre) + "\n\n" + PreguntaConfirmar());
            return;
        }

        r.Mensajes.Add("Para enviar tu solicitud responde *sí*. Si algo está mal, dime qué quieres cambiar o escribe *cancelar*.");
    }

    private static bool IntentarCorreccion(EstadoConversacion estado, string n, IReadOnlyList<string> materiales, ResultadoTurno r)
    {
        if (!Regex.IsMatch(n, @"\b(?:cambi|corregi|modific|equivoc|mal\b|error|otr[oa]\b)", RegexOptions.CultureInvariant, Limite))
            return false;
        var campo = CamposCorregibles.FirstOrDefault(c => Regex.IsMatch(n, c.Patron, RegexOptions.CultureInvariant, Limite));
        if (campo.Dato is null)
            return false;
        estado.Datos.Remove(campo.Dato);
        QuitarOmitido(estado.Datos, campo.Dato);
        estado.Paso = "pregunta:" + campo.Dato;
        r.Mensajes.Add("De acuerdo, corrijamos ese dato. " + Pregunta(campo.Dato, estado.Datos, materiales));
        return true;
    }

    private static bool PuedeSerTextoLibre(string pendiente, Intencion intencion, bool segura)
    {
        if (!segura) return true;
        if (pendiente == "descripcion")
            return !Informativas.Contains(intencion.Nombre) && intencion.Nombre is not ("saludo" or "despedida");
        return false;
    }

    private static List<string> Aplicar(EntidadesExtraidas e, Dictionary<string, string> d, string? pendiente, IReadOnlyList<string> materiales)
    {
        var anotado = new List<string>();
        var todo = pendiente is null || !TextoLibre.Contains(pendiente);
        var esUbicacion = pendiente is "lugar" or "destino" or "volumen";
        var esNombre = pendiente is "nombre";

        if (todo && e.Servicio is not null && Valor(d, "servicio") != e.Servicio)
        {
            d["servicio"] = e.Servicio;
            anotado.Add($"servicio de *{Visible(e.Servicio)}*");
        }

        if ((todo || esUbicacion) && e.DuracionMeses is not null)
        {
            d["duracion"] = e.DuracionMeses.Value.ToString(CultureInfo.InvariantCulture);
            anotado.Add($"duración de *{e.DuracionMeses} {(e.DuracionMeses == 1 ? "mes" : "meses")}*");
        }

        if ((todo || esUbicacion) && e.Frecuencia is not null)
        {
            d["frecuencia"] = e.Frecuencia;
            anotado.Add($"frecuencia *{e.Frecuencia.ToLowerInvariant()}*");
        }

        if ((todo || esUbicacion) && e.Modalidad is not null && Valor(d, "modalidad") != e.Modalidad)
        {
            d["modalidad"] = e.Modalidad;
            anotado.Add(e.Modalidad == "recurrente" ? "*contrato recurrente*" : "*servicio de una sola vez*");
        }

        if (todo || esUbicacion)
        {
            var origen = e.Origen;
            var destino = e.Destino;
            if (pendiente == "destino" && origen is not null && destino is null)
                (origen, destino) = (null, origen);

            if (origen is not null && Valor(d, "lugar") != origen)
            {
                if (!d.ContainsKey("lugar") || pendiente == "lugar" || pendiente is null)
                {
                    d["lugar"] = origen;
                    anotado.Add($"recolección en *{origen}*");
                }
                else if (!d.ContainsKey("destino"))
                {
                    destino ??= origen;
                }
            }

            if (destino is not null && Valor(d, "destino") != destino)
            {
                d["destino"] = destino;
                anotado.Add($"destino *{destino}*");
            }

            if (!d.ContainsKey("servicio") && d.ContainsKey("lugar") && d.ContainsKey("destino"))
            {
                d["servicio"] = "Acarreo de materiales";
                anotado.Add("servicio de *Acarreo de materiales*");
            }

            if (e.Materiales.Count > 0)
            {
                var lista = string.Join(", ", e.Materiales);
                if (Valor(d, "materiales") != lista)
                {
                    d["materiales"] = lista;
                    QuitarOmitido(d, "materiales");
                    anotado.Add($"materiales: *{lista}*");
                }
            }

            if (e.Fecha is not null)
            {
                var fecha = e.Fecha.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (Valor(d, "fecha") != fecha)
                {
                    d["fecha"] = fecha;
                    QuitarOmitido(d, "fecha");
                    anotado.Add($"fecha *{e.Fecha.Value:dd/MM/yyyy}*");
                }
            }

            if (e.Volumen is not null && Valor(d, "modalidad") != "recurrente" && Valor(d, "volumen") != e.Volumen)
            {
                d["volumen"] = e.Volumen;
                QuitarOmitido(d, "volumen");
                anotado.Add($"cantidad *{e.Volumen}*");
            }
        }

        if (pendiente == "volumen" && e.Volumen is not null && Valor(d, "volumen") != e.Volumen)
        {
            d["volumen"] = e.Volumen;
            anotado.Add($"cantidad *{e.Volumen}*");
        }

        if ((todo || esNombre) && e.Nombre is not null && Valor(d, "nombre") != e.Nombre)
        {
            d["nombre"] = e.Nombre;
            anotado.Add($"a nombre de *{e.Nombre}*");
        }

        if ((todo || esNombre) && e.Dui is not null && Valor(d, "dui") != e.Dui)
        {
            d["dui"] = e.Dui;
            anotado.Add($"DUI *{e.Dui}*");
        }

        if ((todo || esNombre) && e.Correo is not null && Valor(d, "correo") != e.Correo)
        {
            d["correo"] = e.Correo;
            QuitarOmitido(d, "correo");
            anotado.Add($"correo *{e.Correo}*");
        }

        return anotado;
    }

    private static bool AceptarTextoLibre(string pendiente, string texto, Dictionary<string, string> d)
    {
        var t = texto.Trim();
        switch (pendiente)
        {
            case "nombre":
                if (t.Length is < 3 or > 150 || t.Contains('?') || t.Any(char.IsDigit) || t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10)
                    return false;
                d["nombre"] = t;
                return true;
            case "lugar":
            case "destino":
                if (t.Length is < 3 or > 120 || t.Contains('?')) return false;
                d[pendiente] = t;
                return true;
            case "volumen":
                if (t.Length > 60 || t.Contains('?')) return false;
                if (!Regex.IsMatch(ProcesadorTexto.Normalizar(t), @"\d|\b(?:un|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|media|medio|camion\w*|viajes?|toneladas?|m3|metros?|bolsas?|sacos?|quintal\w*|libras?|volquet\w*|poco|mucho|grande|pequen\w*)\b", RegexOptions.CultureInvariant, Limite))
                    return false;
                d["volumen"] = t;
                return true;
            case "descripcion":
                if (t.Length > 500) return false;
                d["descripcion"] = t;
                return true;
        }
        return false;
    }

    private static bool ElegirPorNumero(string pendiente, int n, Dictionary<string, string> d, IReadOnlyList<string> materiales)
    {
        switch (pendiente)
        {
            case "servicio" when n >= 1 && n <= Servicios.Length:
                d["servicio"] = Servicios[n - 1];
                return true;
            case "modalidad" when n is 1 or 2:
                d["modalidad"] = n == 1 ? "puntual" : "recurrente";
                return true;
            case "frecuencia" when n >= 1 && n <= Frecuencias.Length:
                d["frecuencia"] = Frecuencias[n - 1];
                return true;
            case "materiales" when n >= 1 && n <= Math.Min(materiales.Count, 15):
                d["materiales"] = materiales[n - 1];
                return true;
            case "duracion" when n is >= 1 and <= 36:
                d["duracion"] = n.ToString(CultureInfo.InvariantCulture);
                return true;
        }
        return false;
    }

    private static string Avanzar(EstadoConversacion estado, IReadOnlyList<string> materiales)
    {
        var siguiente = Siguiente(estado.Datos, materiales);
        if (siguiente is null)
        {
            estado.Paso = "confirmar";
            return Resumen(estado.Datos) + "\n\n" + PreguntaConfirmar();
        }
        estado.Paso = "pregunta:" + siguiente;
        return Pregunta(siguiente, estado.Datos, materiales);
    }

    public static string? Siguiente(Dictionary<string, string> d, IReadOnlyList<string> materiales)
    {
        var recurrente = Valor(d, "modalidad") == "recurrente";
        if (!Tiene(d, "servicio")) return "servicio";
        if (!Tiene(d, "modalidad")) return "modalidad";
        if (!Tiene(d, "lugar")) return "lugar";
        if (ConDestino.Contains(d["servicio"]) && !Tiene(d, "destino")) return "destino";
        if (materiales.Count > 0 && !Tiene(d, "materiales") && !Omitido(d, "materiales")) return "materiales";
        if (recurrente && !Tiene(d, "duracion")) return "duracion";
        if (recurrente && !Tiene(d, "frecuencia")) return "frecuencia";
        if (!Tiene(d, "fecha") && !Omitido(d, "fecha")) return "fecha";
        if (!recurrente && !Tiene(d, "volumen") && !Omitido(d, "volumen")) return "volumen";
        if (!Tiene(d, "nombre")) return "nombre";
        if (!Tiene(d, "dui")) return "dui";
        if (!Tiene(d, "correo") && !Omitido(d, "correo")) return "correo";
        if (!Tiene(d, "descripcion") && !Omitido(d, "descripcion")) return "descripcion";
        return null;
    }

    private static string Pregunta(string dato, Dictionary<string, string> d, IReadOnlyList<string> materiales) => dato switch
    {
        "servicio" => "¿Qué servicio necesitas?\n\n" + Lista(ServiciosVisibles),
        "modalidad" => "¿Será un servicio de *una sola vez* o un *contrato recurrente* (por ejemplo, cada semana durante varios meses)?",
        "lugar" => "¿Desde dónde recogemos el material? Puedes escribir la dirección o el municipio.",
        "destino" => "¿Y hacia dónde lo llevamos?",
        "materiales" => "¿Qué materiales se moverán? Puedes escribir los nombres o los números:\n\n" + Lista(materiales.Take(15)) + "\n\nSi aún no lo sabes, escribe *omitir*.",
        "duracion" => "¿Por cuántos meses necesitas el contrato? (de 1 a 36)",
        "frecuencia" => "¿Cada cuánto necesitas los viajes? Diaria, semanal, quincenal, mensual o a demanda.",
        "fecha" => Valor(d, "modalidad") == "recurrente"
            ? "¿Desde qué fecha quieres iniciar? Puedes decirme _el lunes_, _la próxima semana_ o una fecha como _25/11/2026_. Si aún no la tienes, escribe *omitir*."
            : "¿Para qué fecha lo necesitas? Puedes decirme _mañana_, _el viernes_ o una fecha como _25/11/2026_. Si aún no la tienes, escribe *omitir*.",
        "volumen" => "¿Qué cantidad aproximada es? Por ejemplo: _2 camiones_, _10 toneladas_ o _5 m3_. Si no lo sabes, escribe *omitir*.",
        "nombre" => "Ya casi terminamos. ¿A nombre de quién registro la solicitud? Puede ser tu nombre o el de tu empresa.",
        "dui" => $"Gracias{(Tiene(d, "nombre") ? ", " + d["nombre"].Split(' ')[0] : string.Empty)}. ¿Me compartes tu *DUI*? (formato 00000000-0)",
        "correo" => "¿Tienes un correo donde podamos enviarte la cotización? Si no, escribe *omitir*.",
        "descripcion" => "Por último, ¿algún detalle adicional? Por ejemplo horarios de acceso o si se necesita carga y descarga. Si no, escribe *omitir*.",
        _ => string.Empty
    };

    private static string Acuse(string? pendiente) => pendiente switch
    {
        "nombre" => string.Empty,
        "descripcion" => "Perfecto. ",
        null => string.Empty,
        _ => "Perfecto. "
    };

    private static string ErrorDe(string pendiente, EntidadesExtraidas e, string texto) => pendiente switch
    {
        "dui" when e.DuiInvalido || Regex.IsMatch(texto, @"\d", RegexOptions.None, Limite) => "Ese DUI no parece válido. ",
        "fecha" when e.FechaFueraDeRango => "La fecha debe ser de hoy en adelante y dentro de los próximos dos años. ",
        "duracion" when e.DuracionInvalida || Regex.IsMatch(texto, @"\d", RegexOptions.None, Limite) => "La duración debe estar entre 1 y 36 meses. ",
        "correo" when texto.Contains('@') => "Ese correo no parece válido. ",
        "nombre" => "Necesito el nombre de la persona o empresa, sin números ni signos de pregunta. ",
        "volumen" => "Indícame una cantidad, por ejemplo _2 camiones_ o _10 toneladas_. ",
        "modalidad" => "Respóndeme *una vez* o *contrato*. ",
        _ => $"Disculpa, no estoy seguro de entender \"{Recortar(texto)}\". "
    };

    public static string Resumen(Dictionary<string, string> d)
    {
        var sb = new StringBuilder("*Resumen de tu solicitud*\n\n");
        sb.Append($"Servicio: {Visible(d["servicio"])}\n");
        sb.Append($"Modalidad: {(d["modalidad"] == "recurrente" ? "Contrato recurrente" : "Servicio puntual")}\n");
        sb.Append($"Recolección: {d["lugar"]}\n");
        if (d.TryGetValue("destino", out var destino)) sb.Append($"Destino: {destino}\n");
        if (d.TryGetValue("materiales", out var materiales)) sb.Append($"Materiales: {materiales}\n");
        if (d.TryGetValue("duracion", out var duracion)) sb.Append($"Duración: {duracion} mes(es)\n");
        if (d.TryGetValue("frecuencia", out var frecuencia)) sb.Append($"Frecuencia: {frecuencia}\n");
        if (d.TryGetValue("fecha", out var fecha))
            sb.Append($"Fecha: {DateTime.ParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture):dd/MM/yyyy}\n");
        if (d.TryGetValue("volumen", out var volumen)) sb.Append($"Cantidad: {volumen}\n");
        sb.Append($"Nombre: {d["nombre"]}\nDUI: {d["dui"]}\n");
        if (d.TryGetValue("correo", out var correo)) sb.Append($"Correo: {correo}\n");
        if (d.TryGetValue("descripcion", out var detalle)) sb.Append($"Detalles: {detalle}\n");
        return sb.ToString().TrimEnd();
    }

    private static string PreguntaConfirmar() =>
        "¿Está todo correcto? Responde *sí* para enviar la solicitud, o dime qué quieres cambiar (por ejemplo: _la fecha es 30/11/2026_).";

    private static string Saludo() =>
        "¡Hola! Soy el asistente virtual de *SACOR, Soluciones Ambientales Corporativas*. Puedo ayudarte a planificar y cotizar acarreos de materiales, recolección de desechos y reciclaje.\n\nCuéntame qué necesitas con tus propias palabras, por ejemplo: _necesito llevar 2 camiones de ripio de Soyapango a Apopa el lunes_.";

    public static void Reiniciar(EstadoConversacion estado)
    {
        estado.Paso = "inicio";
        estado.Datos = new Dictionary<string, string>();
    }

    private static string Visible(string servicio)
    {
        var i = Array.IndexOf(Servicios, servicio);
        return i >= 0 ? ServiciosVisibles[i] : servicio;
    }

    private static string Lista(IEnumerable<string> opciones) =>
        string.Join("\n", opciones.Select((o, i) => $"{i + 1}. {o}"));

    private static string Recortar(string texto)
    {
        var t = texto.Trim().Replace("\n", " ");
        return t.Length <= 40 ? t : t[..40] + "...";
    }

    private static string? Valor(Dictionary<string, string> d, string clave) => d.GetValueOrDefault(clave);

    private static bool Tiene(Dictionary<string, string> d, string clave) =>
        d.TryGetValue(clave, out var v) && !string.IsNullOrWhiteSpace(v);

    private static bool Omitido(Dictionary<string, string> d, string clave) =>
        (Valor(d, "_omitidos") ?? string.Empty).Split(',').Contains(clave);

    private static void Omitir(Dictionary<string, string> d, string clave)
    {
        var lista = (Valor(d, "_omitidos") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!lista.Contains(clave)) lista.Add(clave);
        d["_omitidos"] = string.Join(',', lista);
    }

    private static void QuitarOmitido(Dictionary<string, string> d, string clave)
    {
        var lista = (Valor(d, "_omitidos") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Where(c => c != clave).ToList();
        if (lista.Count == 0) d.Remove("_omitidos");
        else d["_omitidos"] = string.Join(',', lista);
    }
}
