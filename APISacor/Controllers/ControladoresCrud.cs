using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;

namespace APISacor.Controllers;

// Los 19 controladores de clave simple heredan del controlador base, asi que ya
// traen paginacion con tope, sanitizacion de texto, forzado de la clave primaria,
// manejo de 404 y de 409 sin repetir una sola linea.
// Los que reciben datos escritos por personas (empleado, cliente, usuario web)
// agregan reglas propias sobre DUI y telefono.

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

[Route("api/clientes")]
public class ClientesController : BaseCrudController<Cliente>
{
    public ClientesController(SacorDbContext db, ILogger<ClientesController> logger)
        : base(db, logger) { }

    private static readonly string[] TiposPermitidos = { "Natural", "Juridico" };

    protected override string? ValidarReglasPropias(Cliente entidad)
    {
        if (!TiposPermitidos.Contains(entidad.Tipo, StringComparer.OrdinalIgnoreCase))
            return $"El tipo debe ser uno de: {string.Join(", ", TiposPermitidos)}.";

        if (!InputSanitizer.TelefonoValido(entidad.Telefono))
            return "El telefono no tiene un formato valido de El Salvador.";

        // Persona natural se identifica con DUI, persona juridica con NIT y NRC.
        if (entidad.Tipo.Equals("Natural", StringComparison.OrdinalIgnoreCase))
        {
            if (!InputSanitizer.DuiValido(entidad.Dui))
                return "Un cliente natural requiere DUI con formato 00000000-0.";
        }
        else if (string.IsNullOrWhiteSpace(entidad.Nit))
        {
            return "Un cliente juridico requiere NIT.";
        }

        return null;
    }
}

[Route("api/rutas-trabajo")]
public class RutasTrabajoController : BaseCrudController<RutaTrabajo>
{
    public RutasTrabajoController(SacorDbContext db, ILogger<RutasTrabajoController> logger)
        : base(db, logger) { }
}

[Route("api/rutas-empleado")]
public class RutasEmpleadoController : BaseCrudController<RutaEmpleado>
{
    public RutasEmpleadoController(SacorDbContext db, ILogger<RutasEmpleadoController> logger)
        : base(db, logger) { }
}

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

[Route("api/camion-conductores")]
public class CamionConductoresController : BaseCrudController<CamionConductor>
{
    public CamionConductoresController(SacorDbContext db, ILogger<CamionConductoresController> logger)
        : base(db, logger) { }
}

[Route("api/horarios-trabajo")]
public class HorariosTrabajoController : BaseCrudController<HorarioTrabajo>
{
    public HorariosTrabajoController(SacorDbContext db, ILogger<HorariosTrabajoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(HorarioTrabajo entidad)
    {
        if (entidad.Salida <= entidad.Entrada)
            return "La hora de salida debe ser posterior a la de entrada.";

        if (entidad.Fecha > DateTime.Today.AddDays(1))
            return "No se pueden registrar marcas con fecha futura.";

        return null;
    }
}

[Route("api/servicios-externos")]
public class ServiciosExternosController : BaseCrudController<ServicioExterno>
{
    public ServiciosExternosController(SacorDbContext db, ILogger<ServiciosExternosController> logger)
        : base(db, logger) { }
}

[Route("api/pagos")]
public class PagosController : BaseCrudController<Pago>
{
    public PagosController(SacorDbContext db, ILogger<PagosController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(Pago entidad)
    {
        // SEGURIDAD: regla de negocio con impacto economico. Sin esta validacion,
        // un descuento mayor al pago generaria un monto neto negativo.
        if (entidad.Descuento > entidad.Cantidad)
            return "El descuento no puede ser mayor que la cantidad pagada.";

        return null;
    }
}

[Route("api/anticipos-sueldo")]
public class AnticiposSueldoController : BaseCrudController<AnticipoSueldo>
{
    public AnticiposSueldoController(SacorDbContext db, ILogger<AnticiposSueldoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(AnticipoSueldo entidad)
    {
        if (entidad.Cantidad <= 0)
            return "El anticipo debe ser mayor que cero.";

        return null;
    }
}

[Route("api/precios-lugar")]
public class PreciosLugarController : BaseCrudController<PrecioLugar>
{
    public PreciosLugarController(SacorDbContext db, ILogger<PreciosLugarController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(PrecioLugar entidad)
    {
        if (entidad.Precio <= 0)
            return "El precio debe ser mayor que cero.";

        entidad.FechaModificacion = DateTime.Today;
        return null;
    }
}

[Route("api/servicios-contrato")]
public class ServiciosContratoController : BaseCrudController<ServicioContrato>
{
    public ServiciosContratoController(SacorDbContext db, ILogger<ServiciosContratoController> logger)
        : base(db, logger) { }
}

[Route("api/limpiezas")]
public class LimpiezasController : BaseCrudController<Limpieza>
{
    public LimpiezasController(SacorDbContext db, ILogger<LimpiezasController> logger)
        : base(db, logger) { }
}

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

[Route("api/viajes-extra")]
public class ViajesExtraController : BaseCrudController<ViajeExtra>
{
    public ViajesExtraController(SacorDbContext db, ILogger<ViajesExtraController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(ViajeExtra entidad)
    {
        if (entidad.Cantidad is <= 0)
            return "La cantidad de viajes debe ser mayor que cero.";

        return null;
    }
}

[Route("api/usuarios-web")]
public class UsuariosWebController : BaseCrudController<UsuarioWeb>
{
    public UsuariosWebController(SacorDbContext db, ILogger<UsuariosWebController> logger)
        : base(db, logger) { }

    // SEGURIDAD: esta tabla la llena gente anonima desde el sitio publico,
    // por eso es la que necesita la validacion mas estricta.
    protected override string? ValidarReglasPropias(UsuarioWeb entidad)
    {
        if (!InputSanitizer.DuiValido(entidad.Dui))
            return "El DUI debe tener el formato 00000000-0.";

        if (!InputSanitizer.TelefonoValido(entidad.Telefono))
            return "El telefono no tiene un formato valido de El Salvador.";

        if (string.IsNullOrWhiteSpace(entidad.Telefono) && string.IsNullOrWhiteSpace(entidad.Email))
            return "Debe proporcionar al menos un telefono o un correo de contacto.";

        return null;
    }
}

[Route("api/cotizaciones")]
public class CotizacionesController : BaseCrudController<Cotizacion>
{
    public CotizacionesController(SacorDbContext db, ILogger<CotizacionesController> logger)
        : base(db, logger) { }

    private static readonly string[] EstadosPermitidos =
        { "Pendiente", "EnRevision", "Cotizada", "Aprobada", "Rechazada", "Cerrada" };

    protected override string? ValidarReglasPropias(Cotizacion entidad)
    {
        if (!EstadosPermitidos.Contains(entidad.Estado, StringComparer.OrdinalIgnoreCase))
            return $"El estado debe ser uno de: {string.Join(", ", EstadosPermitidos)}.";

        if (entidad.Fecha == default)
            entidad.Fecha = DateTime.Today;

        return null;
    }
}

[Route("api/solicitudes-puesto")]
public class SolicitudesPuestoController : BaseCrudController<SolicitudPuestoTrabajo>
{
    public SolicitudesPuestoController(SacorDbContext db, ILogger<SolicitudesPuestoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(SolicitudPuestoTrabajo entidad)
    {
        if (entidad.Fecha == default)
            entidad.Fecha = DateTime.Today;

        return null;
    }
}

[Route("api/observaciones-empleado")]
public class ObservacionesEmpleadoController : BaseCrudController<ObservacionEmpleado>
{
    public ObservacionesEmpleadoController(SacorDbContext db, ILogger<ObservacionesEmpleadoController> logger)
        : base(db, logger) { }

    protected override string? ValidarReglasPropias(ObservacionEmpleado entidad)
    {
        // SEGURIDAD: nadie se escribe observaciones a si mismo. Evita que un
        // administrador se limpie su propio expediente.
        if (entidad.IdAdministradorAutor == entidad.IdEmpleado)
            return "El autor de la observacion no puede ser el mismo empleado observado.";

        return null;
    }
}
