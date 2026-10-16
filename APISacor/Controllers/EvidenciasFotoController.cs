using System.ComponentModel.DataAnnotations;

using APISacor.Data;
using APISacor.Models;
using APISacor.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// Fotos de las evidencias que suben transportistas y tecnicos desde la app movil.
// SEGURIDAD: vive bajo /api/movil, asi que no usa API Key; cada peticion se
// identifica con el token Bearer individual del empleado.
[ApiController]
[Route("api/movil")]
public sealed class EvidenciasFotoController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public EvidenciasFotoController(SacorDbContext db, ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    public sealed class SubirFotoPeticion
    {
        [Required]
        public string ContenidoBase64 { get; set; } = string.Empty;
    }

    // POST /api/movil/evidencias/{idLimpieza}/fotos
    [HttpPost("evidencias/{idLimpieza:int}/fotos")]
    // SEGURIDAD: se sube el limite global de 1 MB solo para este endpoint, con un
    // tope fijo, en lugar de abrirlo para toda la API.
    [RequestSizeLimit(ValidadorFoto.MaxCuerpoPeticion)]
    [Produces("application/json")]
    public async Task<IActionResult> Subir(int idLimpieza, [FromBody] SubirFotoPeticion peticion, CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });

        // SEGURIDAD: solo el autor de la evidencia puede adjuntarle fotos.
        var evidencia = await _db.Limpiezas.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdLimpieza == idLimpieza && l.IdEmpleadoSubio == empleado.IdEmpleado, ct);
        if (evidencia is null)
            return NotFound(new { mensaje = "La evidencia no existe o no te pertenece." });

        if (evidencia.Fecha.Date != ConsultasMovil.HoyLocal())
            return BadRequest(new { mensaje = "Solo puedes agregar fotos a evidencias de hoy." });
        if (!await ConsultasMovil.EnJornadaAsync(_db, empleado.IdEmpleado, ct))
            return BadRequest(new { mensaje = "Marca tu entrada antes de subir fotos." });

        var cantidad = await _db.EvidenciasFoto.CountAsync(f => f.IdLimpieza == idLimpieza, ct);
        if (cantidad >= ValidadorFoto.MaxFotosPorEvidencia)
            return Conflict(new { mensaje = $"Cada evidencia admite máximo {ValidadorFoto.MaxFotosPorEvidencia} fotos." });

        var datos = ValidadorFoto.Decodificar(peticion.ContenidoBase64);
        if (datos is null)
            return BadRequest(new { mensaje = "La foto no es válida o supera los 3 MB." });
        if (datos.Length > ValidadorFoto.MaxBytes)
            return BadRequest(new { mensaje = "La foto supera los 3 MB." });

        var tipo = ValidadorFoto.DetectarTipo(datos);
        if (tipo is null)
            return BadRequest(new { mensaje = "Solo se aceptan fotos JPG o PNG." });

        var foto = new EvidenciaFoto
        {
            IdLimpieza = idLimpieza,
            TipoContenido = tipo,
            Contenido = datos,
            TamanoBytes = datos.Length,
            CreadoUtc = DateTime.UtcNow
        };

        _db.EvidenciasFoto.Add(foto);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            idFoto = foto.IdEvidenciaFoto,
            foto.IdLimpieza,
            foto.TipoContenido,
            foto.TamanoBytes,
            mensaje = "Foto guardada correctamente."
        });
    }

    // GET /api/movil/fotos/{idFoto}
    [HttpGet("fotos/{idFoto:int}")]
    public async Task<IActionResult> Ver(int idFoto, CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });

        var esAdministrador = ServicioAccesoMovil.RolApp(empleado.Tipo) == "administrador";

        // SEGURIDAD: un empleado solo ve sus propias fotos; el administrador ve todas.
        var foto = await (
            from f in _db.EvidenciasFoto.AsNoTracking()
            join l in _db.Limpiezas.AsNoTracking() on f.IdLimpieza equals l.IdLimpieza
            where f.IdEvidenciaFoto == idFoto && (esAdministrador || l.IdEmpleadoSubio == empleado.IdEmpleado)
            select new { f.Contenido, f.TipoContenido }
        ).FirstOrDefaultAsync(ct);

        if (foto is null)
            return NotFound(new { mensaje = "La foto no existe." });

        // SEGURIDAD: nosniff evita que el navegador interprete los bytes como otro
        // tipo de archivo, y private/no-store evita que proxies guarden la foto.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";

        return File(foto.Contenido, foto.TipoContenido);
    }
}
