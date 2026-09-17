using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// Esta tabla tiene clave primaria compuesta, asi que no encaja en el controlador
// base generico y se escribe aparte. Mantiene las mismas protecciones: paginacion
// con tope, validacion de enteros, try-catch y codigos HTTP correctos.
[ApiController]
[Route("api/viaje-extra-empleados")]
[Produces("application/json")]
public class ViajeExtraEmpleadosController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ILogger<ViajeExtraEmpleadosController> _logger;

    public ViajeExtraEmpleadosController(
        SacorDbContext db,
        ILogger<ViajeExtraEmpleadosController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        [FromQuery] int? idViajeExtra,
        CancellationToken cancellationToken)
    {
        var paginaSegura = InputSanitizer.NormalizarPagina(pagina);
        var tamanoSeguro = InputSanitizer.NormalizarTamanoPagina(tamano);

        var consulta = _db.ViajeExtraEmpleados.AsNoTracking();

        // SEGURIDAD: el filtro opcional entra como entero ya parseado por el binder
        // de ASP.NET y se aplica con LINQ, que EF traduce a una consulta
        // parametrizada. En ningun punto se concatena texto para armar SQL.
        if (idViajeExtra is > 0)
            consulta = consulta.Where(v => v.IdViajeExtra == idViajeExtra);

        var total = await consulta.CountAsync(cancellationToken);

        var datos = await consulta
            .OrderBy(v => v.IdViajeExtra).ThenBy(v => v.IdEmpleado)
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToListAsync(cancellationToken);

        return Ok(new { pagina = paginaSegura, tamano = tamanoSeguro, total, datos });
    }

    [HttpGet("{idViajeExtra:int}/{idEmpleado:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(
        int idViajeExtra,
        int idEmpleado,
        CancellationToken cancellationToken)
    {
        if (idViajeExtra <= 0 || idEmpleado <= 0)
            return BadRequest(new { estado = 400, mensaje = "Los identificadores deben ser mayores que cero." });

        var registro = await _db.ViajeExtraEmpleados.AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.IdViajeExtra == idViajeExtra && v.IdEmpleado == idEmpleado,
                cancellationToken);

        return registro is null
            ? NotFound(new { estado = 404, mensaje = "La asignacion no existe." })
            : Ok(registro);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] ViajeExtraEmpleado registro,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (registro.IdViajeExtra <= 0 || registro.IdEmpleado <= 0)
            return BadRequest(new { estado = 400, mensaje = "Los identificadores deben ser mayores que cero." });

        // SEGURIDAD: se verifica que las dos llaves foraneas existan antes de
        // intentar el INSERT. Asi el cliente recibe un 400 explicativo en vez de
        // un 500 con el error crudo de SQL Server, que revelaria nombres de
        // restricciones y estructura de la base.
        var existeViaje = await _db.ViajesExtra
            .AnyAsync(v => v.IdViajeExtra == registro.IdViajeExtra, cancellationToken);
        if (!existeViaje)
            return BadRequest(new { estado = 400, mensaje = "El viaje extra indicado no existe." });

        var existeEmpleado = await _db.Empleados
            .AnyAsync(e => e.IdEmpleado == registro.IdEmpleado, cancellationToken);
        if (!existeEmpleado)
            return BadRequest(new { estado = 400, mensaje = "El empleado indicado no existe." });

        var yaAsignado = await _db.ViajeExtraEmpleados.AnyAsync(
            v => v.IdViajeExtra == registro.IdViajeExtra && v.IdEmpleado == registro.IdEmpleado,
            cancellationToken);
        if (yaAsignado)
            return Conflict(new { estado = 409, mensaje = "El empleado ya esta asignado a ese viaje." });

        // Se descartan las propiedades de navegacion que pudieran venir en el cuerpo.
        registro.ViajeExtra = null;
        registro.Empleado = null;

        _db.ViajeExtraEmpleados.Add(registro);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Asignacion de empleado {Empleado} al viaje {Viaje} por app {App}",
            registro.IdEmpleado, registro.IdViajeExtra, HttpContext.Items["AppConsumidora"]);

        return CreatedAtAction(nameof(Obtener),
            new { idViajeExtra = registro.IdViajeExtra, idEmpleado = registro.IdEmpleado },
            registro);
    }

    [HttpDelete("{idViajeExtra:int}/{idEmpleado:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(
        int idViajeExtra,
        int idEmpleado,
        CancellationToken cancellationToken)
    {
        if (idViajeExtra <= 0 || idEmpleado <= 0)
            return BadRequest(new { estado = 400, mensaje = "Los identificadores deben ser mayores que cero." });

        var registro = await _db.ViajeExtraEmpleados.FirstOrDefaultAsync(
            v => v.IdViajeExtra == idViajeExtra && v.IdEmpleado == idEmpleado,
            cancellationToken);

        if (registro is null)
            return NotFound(new { estado = 404, mensaje = "La asignacion no existe." });

        _db.ViajeExtraEmpleados.Remove(registro);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Baja de asignacion empleado {Empleado} viaje {Viaje} por app {App}",
            idEmpleado, idViajeExtra, HttpContext.Items["AppConsumidora"]);

        return NoContent();
    }
}
