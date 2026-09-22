using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// SEGURIDAD: la auditoria NO hereda del controlador base generico a proposito.
// El base trae PUT y DELETE, y un registro de auditoria que se puede editar o
// borrar pierde todo su valor: alguien podria hacer un cambio indebido y despues
// limpiar su rastro. Aqui solo existen GET y POST.
[ApiController]
[Route("api/auditoria")]
[Produces("application/json")]
public class AuditoriaController : ControllerBase
{
    private static readonly string[] AccionesPermitidas = { "INSERT", "UPDATE", "DELETE" };

    private readonly SacorDbContext _db;
    private readonly ILogger<AuditoriaController> _logger;

    public AuditoriaController(SacorDbContext db, ILogger<AuditoriaController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        [FromQuery] string? tabla,
        [FromQuery] int? idEmpleado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        CancellationToken cancellationToken)
    {
        var paginaSegura = InputSanitizer.NormalizarPagina(pagina);
        var tamanoSeguro = InputSanitizer.NormalizarTamanoPagina(tamano);

        var consulta = _db.Auditorias.AsNoTracking();

        // SEGURIDAD: los filtros entran como parametros tipados y se aplican con
        // LINQ, que EF traduce a una consulta parametrizada. Nunca se concatena
        // texto para armar el SQL.
        var tablaLimpia = InputSanitizer.Limpiar(tabla, 128);
        if (!string.IsNullOrWhiteSpace(tablaLimpia))
            consulta = consulta.Where(a => a.TablaAfectada == tablaLimpia);

        if (idEmpleado is > 0)
            consulta = consulta.Where(a => a.IdEmpleado == idEmpleado);

        if (desde is not null)
            consulta = consulta.Where(a => a.FechaHora >= desde);

        if (hasta is not null)
            consulta = consulta.Where(a => a.FechaHora <= hasta);

        var total = await consulta.CountAsync(cancellationToken);

        // Del mas reciente al mas viejo: es el orden en que se consulta un historial.
        var datos = await consulta
            .OrderByDescending(a => a.IdAuditoria)
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToListAsync(cancellationToken);

        return Ok(new { pagina = paginaSegura, tamano = tamanoSeguro, total, datos });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest(new { estado = 400, mensaje = "El id debe ser mayor que cero." });

        var registro = await _db.Auditorias.AsNoTracking()
            .FirstOrDefaultAsync(a => a.IdAuditoria == id, cancellationToken);

        return registro is null
            ? NotFound(new { estado = 404, mensaje = "El registro no existe." })
            : Ok(registro);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear(
        [FromBody] Auditoria entidad,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        entidad.TablaAfectada = InputSanitizer.Limpiar(entidad.TablaAfectada, 128) ?? string.Empty;
        entidad.IdRegistro = InputSanitizer.Limpiar(entidad.IdRegistro, 100) ?? string.Empty;
        entidad.Detalle = InputSanitizer.Limpiar(entidad.Detalle, 1000);

        if (string.IsNullOrWhiteSpace(entidad.TablaAfectada))
            return BadRequest(new { estado = 400, mensaje = "Indica la tabla afectada." });

        if (string.IsNullOrWhiteSpace(entidad.IdRegistro))
            return BadRequest(new { estado = 400, mensaje = "Indica el identificador del registro." });

        // SEGURIDAD: lista blanca que replica el CHECK de la base. Validar aqui
        // devuelve un 400 explicativo en lugar de un error crudo del motor.
        entidad.Accion = (entidad.Accion ?? string.Empty).Trim().ToUpperInvariant();
        if (!AccionesPermitidas.Contains(entidad.Accion))
            return BadRequest(new
            {
                estado = 400,
                mensaje = $"La accion debe ser una de: {string.Join(", ", AccionesPermitidas)}."
            });

        if (entidad.IdEmpleado is > 0)
        {
            var existe = await _db.Empleados
                .AnyAsync(e => e.IdEmpleado == entidad.IdEmpleado, cancellationToken);
            if (!existe)
                return BadRequest(new { estado = 400, mensaje = "El empleado indicado no existe." });
        }
        else
        {
            entidad.IdEmpleado = null;
        }

        // SEGURIDAD: la fecha la pone el servidor, nunca el cliente. Si viniera del
        // cuerpo, se podrian fabricar registros con fecha falsa.
        entidad.FechaHora = DateTime.Now;
        entidad.IdAuditoria = 0;

        _db.Auditorias.Add(entidad);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Auditoria {Id}: {Accion} sobre {Tabla} registro {Registro}",
            entidad.IdAuditoria, entidad.Accion, entidad.TablaAfectada, entidad.IdRegistro);

        return CreatedAtAction(nameof(Obtener), new { id = entidad.IdAuditoria }, entidad);
    }
}
