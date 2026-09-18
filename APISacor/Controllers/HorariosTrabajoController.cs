using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/horarios-trabajo")]
public class HorariosTrabajoController : BaseCrudController<HorarioTrabajo>
{
    public HorariosTrabajoController(SacorDbContext db, ILogger<HorariosTrabajoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(HorarioTrabajo entidad)
    {
        
        if (entidad.Fecha > DateTime.Today.AddDays(1))
            return "No se pueden registrar marcas con fecha futura.";

        return null;
    }
}
