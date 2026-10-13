using APISacor.Data;
using APISacor.Models;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/gastos-viaje")]
public class GastosViajeController : BaseCrudController<GastoViaje>
{
    public GastosViajeController(SacorDbContext db, ILogger<GastosViajeController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(GastoViaje entidad)
    {
        // SEGURIDAD: lista blanca de conceptos y tope al monto; el gasto entra
        // directo a la contabilidad de la empresa.
        var concepto = ReglasGastoViaje.Normalizar(entidad.Concepto);
        if (concepto is null)
            return $"El concepto debe ser uno de: {string.Join(", ", ReglasGastoViaje.Conceptos)}.";
        entidad.Concepto = concepto;

        if (entidad.Monto <= 0 || entidad.Monto > 100_000)
            return "El monto debe estar entre 0.01 y 100000.";

        return null;
    }
}
