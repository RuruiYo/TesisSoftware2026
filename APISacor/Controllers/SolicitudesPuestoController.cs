using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/solicitudes-puesto")]
public class SolicitudesPuestoController : BaseCrudController<SolicitudPuestoTrabajo>
{
    public SolicitudesPuestoController(SacorDbContext db, ILogger<SolicitudesPuestoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(SolicitudPuestoTrabajo entidad)
    {
        if (entidad.Fecha == default)
            entidad.Fecha = DateTime.Today;

        return null;
    }
}
