using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/cotizaciones")]
public class CotizacionesController : BaseCrudController<Cotizacion>
{
    public CotizacionesController(SacorDbContext db, ILogger<CotizacionesController> logger)
        : base(db, logger) { }

    private static readonly string[] EstadosPermitidos =
        { "Pendiente", "EnRevision", "Cotizada", "Aprobada", "Rechazada", "Cerrada" };

    protected override string? ValidarReglasPropias(Cotizacion entidad)
    {
        if (!EstadosPermitidos.Contains(entidad.Estado, StringComparer.OrdinalIgnoreCase))
            return $"El estado debe ser uno de: {string.Join(", ", EstadosPermitidos)}.";

        if (entidad.Fecha == default)
            entidad.Fecha = DateTime.Today;

        return null;
    }
}
