using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/observaciones-empleado")]
public class ObservacionesEmpleadoController : BaseCrudController<ObservacionEmpleado>
{
    public ObservacionesEmpleadoController(SacorDbContext db, ILogger<ObservacionesEmpleadoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(ObservacionEmpleado entidad)
    {
        // SEGURIDAD: nadie se escribe observaciones a si mismo. Evita que un
        // administrador se limpie su propio expediente.
        if (entidad.IdAdministradorAutor == entidad.IdEmpleado)
            return "El autor de la observacion no puede ser el mismo empleado observado.";

        return null;
    }
}
