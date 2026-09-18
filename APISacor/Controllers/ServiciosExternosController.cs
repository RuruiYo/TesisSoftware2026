using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/servicios-externos")]
public class ServiciosExternosController : BaseCrudController<ServicioExterno>
{
    public ServiciosExternosController(SacorDbContext db, ILogger<ServiciosExternosController> logger)
        : base(db, logger) { }
}
