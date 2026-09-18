using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/servicios-contrato")]
public class ServiciosContratoController : BaseCrudController<ServicioContrato>
{
    public ServiciosContratoController(SacorDbContext db, ILogger<ServiciosContratoController> logger)
        : base(db, logger) { }
}
