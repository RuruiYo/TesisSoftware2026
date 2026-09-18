using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/clientes")]
public class ClientesController : BaseCrudController<Cliente>
{
    public ClientesController(SacorDbContext db, ILogger<ClientesController> logger)
        : base(db, logger) { }

    private static readonly string[] TiposPermitidos = { "Natural", "Juridico" };

    protected override string? ValidarReglasPropias(Cliente entidad)
    {
        if (!TiposPermitidos.Contains(entidad.Tipo, StringComparer.OrdinalIgnoreCase))
            return $"El tipo debe ser uno de: {string.Join(", ", TiposPermitidos)}.";

        if (!InputSanitizer.TelefonoValido(entidad.Telefono))
            return "El telefono no tiene un formato valido de El Salvador.";

        // Persona natural se identifica con DUI, persona juridica con NIT y NRC.
        if (entidad.Tipo.Equals("Natural", StringComparison.OrdinalIgnoreCase))
        {
            if (!InputSanitizer.DuiValido(entidad.Dui))
                return "Un cliente natural requiere DUI con formato 00000000-0.";
        }
        else if (string.IsNullOrWhiteSpace(entidad.Nit))
        {
            return "Un cliente juridico requiere NIT.";
        }

        return null;
    }
}
