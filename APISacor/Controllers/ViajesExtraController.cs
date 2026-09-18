using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/viajes-extra")]
public class ViajesExtraController : BaseCrudController<ViajeExtra>
{
    public ViajesExtraController(SacorDbContext db, ILogger<ViajesExtraController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(ViajeExtra entidad)
    {
        if (entidad.Cantidad is <= 0)
            return "La cantidad de viajes debe ser mayor que cero.";

        return null;
    }
}
