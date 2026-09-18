using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/precios-lugar")]
public class PreciosLugarController : BaseCrudController<PrecioLugar>
{
    public PreciosLugarController(SacorDbContext db, ILogger<PreciosLugarController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(PrecioLugar entidad)
    {
        if (entidad.Precio <= 0)
            return "El precio debe ser mayor que cero.";

        entidad.FechaModificacion = DateTime.Today;
        return null;
    }
}
