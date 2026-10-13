using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // Si el cliente no manda el precio, se congela el precio por tonelada vigente
        // en este momento. Asi la venta del pesaje no cambia si el precio se edita despues.
        if (entidad.PrecioTonelada is null)
        {
            entidad.PrecioTonelada = Db.PreciosLugar.AsNoTracking()
                .Where(p => p.IdPrecioLugar == entidad.IdPrecioLugar)
                .Select(p => (decimal?)p.Precio)
                .FirstOrDefault();
            if (entidad.PrecioTonelada is null)
                return "El precio indicado no existe.";
        }

        return null;
    }
}
