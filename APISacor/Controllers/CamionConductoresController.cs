using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/camion-conductores")]
public class CamionConductoresController : BaseCrudController<CamionConductor>
{
    public CamionConductoresController(SacorDbContext db, ILogger<CamionConductoresController> logger)
        : base(db, logger) { }
}
