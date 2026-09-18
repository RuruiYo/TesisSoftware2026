using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/camiones")]
public class CamionesController : BaseCrudController<Camion>
{
    public CamionesController(SacorDbContext db, ILogger<CamionesController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(Camion entidad)
    {
        // SEGURIDAD: la placa se normaliza a mayusculas sin espacios para que el
        // indice unico funcione de verdad. Sin esto entrarian "P123-456" y
        // "p123 456" como dos camiones distintos.
        entidad.Placa = entidad.Placa.Replace(" ", string.Empty).ToUpperInvariant();

        if (entidad.Placa.Length < 5)
            return "La placa no es valida.";

        return null;
    }
}
