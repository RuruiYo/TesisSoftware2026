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
        if (entidad.Salida <= entidad.Entrada)
            return "La hora de salida debe ser posterior a la de entrada.";

        if (entidad.Fecha > DateTime.Today.AddDays(1))
            return "No se pueden registrar marcas con fecha futura.";

        return null;
    }
}
