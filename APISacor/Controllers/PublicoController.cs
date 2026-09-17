using System.ComponentModel.DataAnnotations;
using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// SEGURIDAD: controlador dedicado al sitio publico. Existe para NO exponerle a la
// web los controladores CRUD completos: desde aqui solo se puede dar de alta una
// cotizacion o una solicitud de empleo, nunca leer, modificar ni borrar nada.
// Es el principio de menor privilegio aplicado a la superficie del API.
[ApiController]
[Route("api/publico")]
[Produces("application/json")]
public class PublicoController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ILogger<PublicoController> _logger;

    public PublicoController(SacorDbContext db, ILogger<PublicoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // SEGURIDAD: DTOs propios en lugar de las entidades. El visitante solo puede
    // enviar estos campos; no existe forma de que toque estado, analista asignado
    // ni ninguna columna interna aunque los mande en el JSON.
    public class CotizacionWebDto
    {
        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required, MaxLength(12)]
        public string Dui { get; set; } = string.Empty;

        [MaxLength(25)]
        public string? Telefono { get; set; }

        [EmailAddress, MaxLength(254)]
        public string? Email { get; set; }

        [MaxLength(80)]
        public string? TipoServicio { get; set; }

        [MaxLength(1000)]
        public string? Descripcion { get; set; }
    }

    public class EmpleoWebDto
    {
        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required, MaxLength(12)]
        public string Dui { get; set; } = string.Empty;

        [MaxLength(25)]
        public string? Telefono { get; set; }

        [EmailAddress, MaxLength(254)]
        public string? Email { get; set; }

        [Required, MaxLength(100)]
        public string TipoPuesto { get; set; } = string.Empty;
    }

    [HttpPost("cotizaciones")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearCotizacion(
        [FromBody] CotizacionWebDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var nombre = InputSanitizer.Limpiar(dto.Nombre, 150);
        var dui = InputSanitizer.Limpiar(dto.Dui, 12);
        var telefono = InputSanitizer.Limpiar(dto.Telefono, 25);
        var email = InputSanitizer.Limpiar(dto.Email, 254);
        var tipoServicio = InputSanitizer.Limpiar(dto.TipoServicio, 80);
        var descripcion = InputSanitizer.Limpiar(dto.Descripcion, 1000);

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new { estado = 400, mensaje = "El nombre es obligatorio." });

        if (!InputSanitizer.DuiValido(dui))
            return BadRequest(new { estado = 400, mensaje = "El DUI debe tener el formato 00000000-0." });

        if (!InputSanitizer.TelefonoValido(telefono))
            return BadRequest(new { estado = 400, mensaje = "El telefono no tiene un formato valido de El Salvador." });

        if (string.IsNullOrWhiteSpace(telefono) && string.IsNullOrWhiteSpace(email))
            return BadRequest(new { estado = 400, mensaje = "Necesitamos un telefono o un correo para contactarte." });

        // SEGURIDAD: la estrategia de ejecucion es obligatoria porque el DbContext
        // tiene EnableRetryOnFailure. Sin esto, abrir una transaccion manual lanza
        // excepcion en tiempo de ejecucion.
        var estrategia = _db.Database.CreateExecutionStrategy();

        var idCotizacion = await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            var usuario = await ObtenerOCrearUsuarioAsync(nombre, dui!, telefono, email, cancellationToken);

            var cotizacion = new Cotizacion
            {
                IdUsuarioWeb = usuario.IdUsuarioWeb,
                Fecha = DateTime.Today,
                // El estado lo fija el servidor, NUNCA el cliente. Si viniera del
                // formulario, cualquiera podria crear cotizaciones ya "Aprobadas".
                Estado = "Pendiente",
                TipoServicio = tipoServicio,
                Descripcion = descripcion
            };

            _db.Cotizaciones.Add(cotizacion);
            await _db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return cotizacion.IdCotizacion;
        });

        _logger.LogInformation("Cotizacion web {Id} registrada", idCotizacion);

        return StatusCode(StatusCodes.Status201Created, new
        {
            estado = 201,
            mensaje = "Solicitud registrada. Te contactamos en menos de 24 horas habiles.",
            idCotizacion
        });
    }

    [HttpPost("empleo")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearSolicitudEmpleo(
        [FromBody] EmpleoWebDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var nombre = InputSanitizer.Limpiar(dto.Nombre, 150);
        var dui = InputSanitizer.Limpiar(dto.Dui, 12);
        var telefono = InputSanitizer.Limpiar(dto.Telefono, 25);
        var email = InputSanitizer.Limpiar(dto.Email, 254);
        var puesto = InputSanitizer.Limpiar(dto.TipoPuesto, 100);

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new { estado = 400, mensaje = "El nombre es obligatorio." });

        if (!InputSanitizer.DuiValido(dui))
            return BadRequest(new { estado = 400, mensaje = "El DUI debe tener el formato 00000000-0." });

        if (!InputSanitizer.TelefonoValido(telefono))
            return BadRequest(new { estado = 400, mensaje = "El telefono no tiene un formato valido de El Salvador." });

        if (string.IsNullOrWhiteSpace(puesto))
            return BadRequest(new { estado = 400, mensaje = "Indicanos el puesto que te interesa." });

        var estrategia = _db.Database.CreateExecutionStrategy();

        var idSolicitud = await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            var usuario = await ObtenerOCrearUsuarioAsync(nombre, dui!, telefono, email, cancellationToken);

            var solicitud = new SolicitudPuestoTrabajo
            {
                IdUsuarioWeb = usuario.IdUsuarioWeb,
                TipoPuesto = puesto!,
                Fecha = DateTime.Today
            };

            _db.SolicitudesPuestoTrabajo.Add(solicitud);
            await _db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return solicitud.IdSolicitud;
        });

        _logger.LogInformation("Solicitud de empleo {Id} registrada", idSolicitud);

        return StatusCode(StatusCodes.Status201Created, new
        {
            estado = 201,
            mensaje = "Solicitud registrada. Te contactamos en un maximo de cinco dias habiles.",
            idSolicitud
        });
    }

    // El DUI identifica a la persona. Si ya escribio antes se reutiliza su registro
    // en lugar de chocar con el indice unico y devolver un 409 que el visitante
    // no sabria como resolver.
    private async Task<UsuarioWeb> ObtenerOCrearUsuarioAsync(
        string nombre,
        string dui,
        string? telefono,
        string? email,
        CancellationToken cancellationToken)
    {
        var usuario = await _db.UsuariosWeb.FirstOrDefaultAsync(u => u.Dui == dui, cancellationToken);

        if (usuario is null)
        {
            usuario = new UsuarioWeb
            {
                Nombre = nombre,
                Dui = dui,
                Telefono = telefono,
                Email = email
            };
            _db.UsuariosWeb.Add(usuario);
        }
        else
        {
            // Se refrescan los datos de contacto por si cambiaron desde la ultima vez.
            usuario.Nombre = nombre;
            if (!string.IsNullOrWhiteSpace(telefono)) usuario.Telefono = telefono;
            if (!string.IsNullOrWhiteSpace(email)) usuario.Email = email;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return usuario;
    }
}
