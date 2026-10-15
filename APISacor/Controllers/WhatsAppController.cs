using System.Security;
using System.Security.Cryptography;
using System.Text;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

public sealed class OpcionesTwilio
{
    public const string Seccion = "Twilio";

    public string AuthToken { get; set; } = string.Empty;
    public string UrlWebhook { get; set; } = string.Empty;

    public bool Configurado => !string.IsNullOrWhiteSpace(AuthToken) && !string.IsNullOrWhiteSpace(UrlWebhook);
}

[ApiController]
[Route("api/whatsapp")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class WhatsAppController : ControllerBase
{
    private readonly OpcionesTwilio _opciones;
    private readonly ChatbotCotizacion _chatbot;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(OpcionesTwilio opciones, ChatbotCotizacion chatbot, ILogger<WhatsAppController> logger)
    {
        _opciones = opciones;
        _chatbot = chatbot;
        _logger = logger;
    }

    // POST /api/whatsapp/webhook
    // Twilio envia aqui cada mensaje que llega al numero de WhatsApp y la respuesta
    // se devuelve en formato TwiML, sin llamadas salientes.
    [HttpPost("webhook")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Recibir(CancellationToken ct)
    {
        if (!_opciones.Configurado)
            return StatusCode(503);

        var formulario = await Request.ReadFormAsync(ct);

        // SEGURIDAD: se valida la firma X-Twilio-Signature. Sin ella cualquiera podria
        // llamar esta ruta publica y crear cotizaciones haciendose pasar por un cliente.
        if (!FirmaValida(formulario, Request.Headers["X-Twilio-Signature"].ToString()))
        {
            _logger.LogWarning("Webhook de WhatsApp con firma invalida desde {Ip}", HttpContext.Connection.RemoteIpAddress);
            return StatusCode(403);
        }

        var de = new string(formulario["From"].ToString().Where(char.IsDigit).ToArray());
        var id = formulario["MessageSid"].ToString();
        if (de.Length is < 8 or > 20 || string.IsNullOrWhiteSpace(id) || id.Length > 128)
            return Twiml(Array.Empty<string>());

        var cuerpo = formulario["Body"].ToString();
        var texto = string.IsNullOrWhiteSpace(cuerpo) ? null : cuerpo;

        try
        {
            var respuestas = await _chatbot.ProcesarAsync(de, id, texto, ct);
            return Twiml(respuestas);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error al procesar un mensaje de WhatsApp");
            return Twiml(new[] { "Tuvimos un problema al procesar tu mensaje. Intenta de nuevo en unos minutos." });
        }
    }

    private ContentResult Twiml(IEnumerable<string> mensajes)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response>");
        foreach (var mensaje in mensajes)
            xml.Append("<Message>").Append(SecurityElement.Escape(mensaje)).Append("</Message>");
        xml.Append("</Response>");
        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    private bool FirmaValida(IFormCollection formulario, string firma)
    {
        if (string.IsNullOrWhiteSpace(firma))
            return false;

        var datos = new StringBuilder(_opciones.UrlWebhook);
        foreach (var clave in formulario.Keys.OrderBy(k => k, StringComparer.Ordinal))
            datos.Append(clave).Append(formulario[clave].ToString());

        var calculada = HMACSHA1.HashData(Encoding.UTF8.GetBytes(_opciones.AuthToken), Encoding.UTF8.GetBytes(datos.ToString()));
        byte[] recibida;
        try
        {
            recibida = Convert.FromBase64String(firma);
        }
        catch (FormatException)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(calculada, recibida);
    }
}
