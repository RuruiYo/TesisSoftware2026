using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/limpiezas")]
public class LimpiezasController : BaseCrudController<Limpieza>
{
    public LimpiezasController(SacorDbContext db, ILogger<LimpiezasController> logger)
        : base(db, logger) { }
}
