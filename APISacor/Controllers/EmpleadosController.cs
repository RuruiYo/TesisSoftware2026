using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

[Route("api/empleados")]
public class EmpleadosController : BaseCrudController<Empleado>
{
    public EmpleadosController(SacorDbContext db, ILogger<EmpleadosController> logger)
        : base(db, logger) { }

    // SEGURIDAD: lista blanca de valores permitidos. Si el estado y el tipo llegaran
    // libres, cualquier app consumidora podria inventarse el tipo "superadmin" y
    // la logica de permisos de las otras dos apps quedaria burlada.
    private static readonly string[] EstadosPermitidos = { "Activo", "Inactivo", "Suspendido" };
    private static readonly string[] TiposPermitidos =
        { "Administrador", "Motorista", "Tecnico", "Ayudante", "Mecanico", "Coordinador" };

    protected override string? ValidarReglasPropias(Empleado entidad)
    {
        if (!InputSanitizer.DuiValido(entidad.Dui))
            return "El DUI debe tener el formato 00000000-0.";

        if (!InputSanitizer.TelefonoValido(entidad.Telefono))
            return "El telefono no tiene un formato valido de El Salvador.";

        if (!EstadosPermitidos.Contains(entidad.Estado, StringComparer.OrdinalIgnoreCase))
            return $"El estado debe ser uno de: {string.Join(", ", EstadosPermitidos)}.";

        if (!TiposPermitidos.Contains(entidad.Tipo, StringComparer.OrdinalIgnoreCase))
            return $"El tipo debe ser uno de: {string.Join(", ", TiposPermitidos)}.";

        // SEGURIDAD: se valida el rango de fecha para que no entren registros con
        // fechas imposibles que despues rompan los calculos de antiguedad.
        if (entidad.FechaIngreso < new DateTime(1990, 1, 1) ||
            entidad.FechaIngreso > DateTime.Today.AddDays(1))
            return "La fecha de ingreso esta fuera del rango permitido.";

        return null;
    }
}
