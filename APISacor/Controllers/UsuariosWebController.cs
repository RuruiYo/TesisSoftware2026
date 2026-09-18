using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/usuarios-web")]
public class UsuariosWebController : BaseCrudController<UsuarioWeb>
{
    public UsuariosWebController(SacorDbContext db, ILogger<UsuariosWebController> logger)
        : base(db, logger) { }

    // SEGURIDAD: esta tabla la llena gente anonima desde el sitio publico,
    // por eso es la que necesita la validacion mas estricta.
    protected override string? ValidarReglasPropias(UsuarioWeb entidad)
    {
        if (!InputSanitizer.DuiValido(entidad.Dui))
            return "El DUI debe tener el formato 00000000-0.";

        if (!InputSanitizer.TelefonoValido(entidad.Telefono))
            return "El telefono no tiene un formato valido de El Salvador.";

        if (string.IsNullOrWhiteSpace(entidad.Telefono) && string.IsNullOrWhiteSpace(entidad.Email))
            return "Debe proporcionar al menos un telefono o un correo de contacto.";

        return null;
    }
}
