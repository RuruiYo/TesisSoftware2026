using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/anticipos-sueldo")]
public class AnticiposSueldoController : BaseCrudController<AnticipoSueldo>
{
    public AnticiposSueldoController(SacorDbContext db, ILogger<AnticiposSueldoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(AnticipoSueldo entidad)
    {
        if (entidad.Cantidad <= 0)
            return "El anticipo debe ser mayor que cero.";

        return null;
    }
}
