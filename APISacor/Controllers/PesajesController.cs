using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/pesajes")]
public class PesajesController : BaseCrudController<Pesaje>
{
    public PesajesController(SacorDbContext db, ILogger<PesajesController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(Pesaje entidad)
    {
        // SEGURIDAD: tope superior al peso. El peso alimenta el cobro al cliente,
        // asi que un valor absurdo por error de digitacion o por manipulacion
        // tiene consecuencia economica directa.
        if (entidad.PesoTotal is <= 0 or > 100_000)
            return "El peso total debe estar entre 0 y 100000.";

        return null;
    }
}
