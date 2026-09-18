using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/pagos")]
public class PagosController : BaseCrudController<Pago>
{
    public PagosController(SacorDbContext db, ILogger<PagosController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(Pago entidad)
    {
        // SEGURIDAD: regla de negocio con impacto economico. Sin esta validacion,
        // un descuento mayor al pago generaria un monto neto negativo.
        if (entidad.Descuento > entidad.Cantidad)
            return "El descuento no puede ser mayor que la cantidad pagada.";

        return null;
    }
}
