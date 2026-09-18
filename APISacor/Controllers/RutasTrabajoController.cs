using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/rutas-trabajo")]
public class RutasTrabajoController : BaseCrudController<RutaTrabajo>
{
    public RutasTrabajoController(SacorDbContext db, ILogger<RutasTrabajoController> logger)
        : base(db, logger) { }
}
