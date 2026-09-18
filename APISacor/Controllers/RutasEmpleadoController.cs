using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/rutas-empleado")]
public class RutasEmpleadoController : BaseCrudController<RutaEmpleado>
{
    public RutasEmpleadoController(SacorDbContext db, ILogger<RutasEmpleadoController> logger)
        : base(db, logger) { }
}
