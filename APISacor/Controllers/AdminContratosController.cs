using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using APISacor.Data;
using APISacor.Models;
using APISacor.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// El administrador tecnico asigna personal y camion a cada viaje de un contrato.
[ApiController]
[Route("api/movil/admin")]
[Produces("application/json")]
public sealed class AdminContratosController : ControllerBase
{
    // SEGURIDAD: tope de personas por viaje para que una peticion no cree cientos de filas.
    private const int MaxPersonasPorViaje = 10;

    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public AdminContratosController(SacorDbContext db, ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    public sealed class AsignacionPeticion
    {
        [MaxLength(MaxPersonasPorViaje)]
        public List<int> IdEmpleados { get; set; } = new();

        public int? IdCamion { get; set; }

        public bool SoloSinPersonal { get; set; }
    }

    public sealed class ViajeExtraPeticion
    {
        public DateTime Fecha { get; set; }

        [Range(1, int.MaxValue)]
        public int IdCamion { get; set; }

        [MaxLength(MaxPersonasPorViaje)]
        public List<int> IdEmpleados { get; set; } = new();

        [Required]
        [StringLength(40, MinimumLength = 3)]
        public string Motivo { get; set; } = string.Empty;

        [Range(0, 9999999999.99)]
        public decimal? Precio { get; set; }
    }

    // SEGURIDAD: solo el administrador tecnico activo usa estos endpoints.
    private async Task<Empleado?> ObtenerAdminAsync(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        return empleado is not null && ServicioAccesoMovil.RolApp(empleado.Tipo) == "administrador" ? empleado : null;
    }

    // GET /api/movil/admin/contratos
    [HttpGet("contratos")]
    public async Task<IActionResult> Contratos(CancellationToken ct)
    {
        if (await ObtenerAdminAsync(ct) is null)
            return Unauthorized(new { mensaje = "Sesión inválida o sin permisos." });

        var contratos = await (
            from s in _db.ServiciosContrato.AsNoTracking()
            join c in _db.Clientes.AsNoTracking() on s.IdCliente equals c.IdCliente
            join p in _db.PreciosLugar.AsNoTracking() on s.IdPrecioLugar equals p.IdPrecioLugar
            where _db.RutasTrabajo.Any(r => r.IdServicioContrato == s.IdServicioContrato)
            orderby s.IdServicioContrato descending
            select new
            {
                s.IdServicioContrato,
                s.IdCotizacion,
                cliente = c.Nombre,
                telefono = c.Telefono,
                servicio = s.TipoTransportaje,
                lugar = p.Lugar,
                s.Fecha,
                s.Observacion,
                totalViajes = _db.RutasTrabajo.Count(r => r.IdServicioContrato == s.IdServicioContrato),
                viajesConPersonal = _db.RutasTrabajo.Count(r =>
                    r.IdServicioContrato == s.IdServicioContrato &&
                    _db.RutasEmpleado.Any(a => a.IdRutaTrabajo == r.IdRutaTrabajo))
            }
        ).Take(100).ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";
        return Ok(contratos);
    }

    // GET /api/movil/admin/contratos/{id}/viajes
    [HttpGet("contratos/{id:int}/viajes")]
    public async Task<IActionResult> ViajesDelContrato(int id, CancellationToken ct)
    {
        if (await ObtenerAdminAsync(ct) is null)
            return Unauthorized(new { mensaje = "Sesión inválida o sin permisos." });

        var ids = await _db.RutasTrabajo.AsNoTracking()
            .Where(r => r.IdServicioContrato == id)
            .Select(r => r.IdRutaTrabajo)
            .Take(500)
            .ToListAsync(ct);

        if (ids.Count == 0)
            return NotFound(new { mensaje = "El contrato no existe o no tiene viajes." });

        Response.Headers.CacheControl = "no-store";
        return Ok(await ConsultasMovil.ResumirViajesAsync(_db, ids, ct));
    }

    // PUT /api/movil/admin/viajes/{id}/asignacion
    [HttpPut("viajes/{id:int}/asignacion")]
    public async Task<IActionResult> AsignarViaje(int id, [FromBody] AsignacionPeticion peticion, CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);
        if (admin is null)
            return Unauthorized(new { mensaje = "Sesión inválida o sin permisos." });

        if (!await _db.RutasTrabajo.AnyAsync(r => r.IdRutaTrabajo == id, ct))
            return NotFound(new { mensaje = "El viaje no existe." });

        var error = await ValidarAsync(peticion, ct);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        await AplicarAsync(new List<int> { id }, peticion, admin.IdEmpleado, ct);

        var viaje = (await ConsultasMovil.ResumirViajesAsync(_db, new List<int> { id }, ct)).Single();
        return Ok(new { viaje, mensaje = "Viaje asignado correctamente." });
    }

    // POST /api/movil/admin/contratos/{id}/asignar
    [HttpPost("contratos/{id:int}/asignar")]
    public async Task<IActionResult> AsignarContrato(int id, [FromBody] AsignacionPeticion peticion, CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);
        if (admin is null)
            return Unauthorized(new { mensaje = "Sesión inválida o sin permisos." });

        var error = await ValidarAsync(peticion, ct);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var consulta = _db.RutasTrabajo.AsNoTracking().Where(r => r.IdServicioContrato == id);
        if (peticion.SoloSinPersonal)
            consulta = consulta.Where(r => !_db.RutasEmpleado.Any(a => a.IdRutaTrabajo == r.IdRutaTrabajo));

        var ids = await consulta.Select(r => r.IdRutaTrabajo).Take(500).ToListAsync(ct);
        if (ids.Count == 0)
            return Ok(new { actualizados = 0, mensaje = "No había viajes por asignar." });

        await AplicarAsync(ids, peticion, admin.IdEmpleado, ct);
        return Ok(new { actualizados = ids.Count, mensaje = $"Se asignaron {ids.Count} viaje(s)." });
    }

    // GET /api/movil/admin/viajes-extra
    [HttpGet("viajes-extra")]
    public async Task<IActionResult> ViajesExtra(CancellationToken ct)
    {
        if (await ObtenerAdminAsync(ct) is null)
            return Unauthorized(new { mensaje = "Sesión inválida o sin permisos." });

        var extras = await _db.ViajesExtra.AsNoTracking()
            .Where(v => v.IdRutaTrabajo != null)
            .OrderByDescending(v => v.IdViajeExtra)
            .Take(100)
            .Select(v => new { v.IdViajeExtra, IdRutaTrabajo = v.IdRutaTrabajo!.Value, v.Precio })
            .ToListAsync(ct);

        var viajes = await ConsultasMovil.ResumirViajesAsync(_db, extras.Select(e => e.IdRutaTrabajo).ToList(), ct);

        Response.Headers.CacheControl = "no-store";
        return Ok(extras.Select(e => new
        {
            e.IdViajeExtra,
            e.Precio,
            viaje = viajes.FirstOrDefault(v => v.IdRutaTrabajo == e.IdRutaTrabajo)
        }).Where(e => e.viaje is not null));
    }

    // POST /api/movil/admin/contratos/{id}/viajes-extra
    // Un imprevisto del contrato: crea el viaje (calendario, evidencias) y su
    // registro en viaje_extra (lo que se cobra aparte en contabilidad).
    [HttpPost("contratos/{id:int}/viajes-extra")]
    public async Task<IActionResult> CrearViajeExtra(int id, [FromBody] ViajeExtraPeticion peticion, CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);
        if (admin is null)
            return Unauthorized(new { mensaje = "Sesión inválida o sin permisos." });

        var contrato = await _db.ServiciosContrato.AsNoTracking().FirstOrDefaultAsync(s => s.IdServicioContrato == id, ct);
        if (contrato is null)
            return NotFound(new { mensaje = "El contrato no existe." });

        var hoy = ConsultasMovil.HoyLocal();
        var fecha = peticion.Fecha.Date;
        if (fecha < hoy.AddYears(-1) || fecha > hoy.AddYears(1))
            return BadRequest(new { mensaje = "La fecha del viaje no es válida." });

        if (decimal.Round(peticion.Precio ?? 0, 2) != (peticion.Precio ?? 0))
            return BadRequest(new { mensaje = "El precio admite máximo dos decimales." });

        var motivo = peticion.Motivo.Trim();
        if (motivo.Length < 3)
            return BadRequest(new { mensaje = "Escribe el motivo del viaje extra." });

        var error = await ValidarAsync(new AsignacionPeticion { IdEmpleados = peticion.IdEmpleados, IdCamion = peticion.IdCamion }, ct);
        if (error is not null)
            return BadRequest(new { mensaje = error });
        if (peticion.IdEmpleados.Count == 0)
            return BadRequest(new { mensaje = "Selecciona al menos una persona para el viaje." });

        var destinoBase = await _db.RutasTrabajo.AsNoTracking()
            .Where(r => r.IdRutaTrabajo == contrato.IdRutaTrabajo)
            .Select(r => r.Destino)
            .FirstOrDefaultAsync(ct) ?? "Sin destino";
        var texto = ConsultasMovil.LeerDestino(destinoBase).Texto;

        var estrategia = _db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync<IActionResult>(async () =>
        {
            // SEGURIDAD: viaje, personal y cobro se guardan juntos o no se guarda nada.
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

            var ruta = new RutaTrabajo
            {
                IdAdministrador = admin.IdEmpleado,
                IdServicioContrato = contrato.IdServicioContrato,
                IdCamion = peticion.IdCamion,
                Destino = Recortar($"[{fecha:yyyy-MM-dd}] {texto}", 200),
                Tipo = Recortar($"Viaje extra: {motivo}", 60)
            };
            _db.RutasTrabajo.Add(ruta);
            await _db.SaveChangesAsync(ct);

            var extra = new ViajeExtra
            {
                IdCliente = contrato.IdCliente,
                IdAdministrador = admin.IdEmpleado,
                IdCamion = peticion.IdCamion,
                Tipo = Recortar($"[{fecha:yyyy-MM}] Extra: {motivo}", 60),
                Precio = peticion.Precio,
                Cantidad = 1,
                IdRutaTrabajo = ruta.IdRutaTrabajo
            };
            _db.ViajesExtra.Add(extra);
            await _db.SaveChangesAsync(ct);

            foreach (var idEmpleado in peticion.IdEmpleados)
            {
                _db.RutasEmpleado.Add(new RutaEmpleado { IdRutaTrabajo = ruta.IdRutaTrabajo, IdEmpleado = idEmpleado });
                _db.ViajeExtraEmpleados.Add(new ViajeExtraEmpleado { IdViajeExtra = extra.IdViajeExtra, IdEmpleado = idEmpleado });
            }

            _db.Auditorias.Add(new Auditoria
            {
                IdEmpleado = admin.IdEmpleado,
                TablaAfectada = "viaje_extra",
                IdRegistro = $"{extra.IdViajeExtra}",
                Accion = "INSERT",
                FechaHora = DateTime.Now,
                ValorNuevo = JsonSerializer.Serialize(new
                {
                    contrato = contrato.IdServicioContrato,
                    viaje = ruta.IdRutaTrabajo,
                    fecha = fecha.ToString("yyyy-MM-dd"),
                    motivo,
                    peticion.Precio,
                    peticion.IdEmpleados,
                    peticion.IdCamion
                }),
                Detalle = "App móvil: viaje extra de contrato"
            });

            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            var viaje = (await ConsultasMovil.ResumirViajesAsync(_db, new List<int> { ruta.IdRutaTrabajo }, ct)).Single();
            return Ok(new { idViajeExtra = extra.IdViajeExtra, viaje, mensaje = "Viaje extra creado y asignado." });
        });
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];

    // SEGURIDAD: lista blanca. Solo motoristas y tecnicos activos van a un viaje y el
    // camion debe existir; los ids repetidos o invalidos se rechazan.
    private async Task<string?> ValidarAsync(AsignacionPeticion peticion, CancellationToken ct)
    {
        var ids = peticion.IdEmpleados.Where(i => i > 0).Distinct().ToList();
        if (ids.Count != peticion.IdEmpleados.Count)
            return "La lista de empleados tiene valores inválidos o repetidos.";
        if (ids.Count > MaxPersonasPorViaje)
            return $"Un viaje admite máximo {MaxPersonasPorViaje} personas.";

        var validos = await _db.Empleados.AsNoTracking()
            .CountAsync(e => ids.Contains(e.IdEmpleado) && e.Estado == "Activo" &&
                             (e.Tipo == TiposEmpleado.Motorista || e.Tipo == TiposEmpleado.Tecnico), ct);
        if (validos != ids.Count)
            return "Solo se pueden asignar motoristas y técnicos activos.";

        if (peticion.IdCamion is not null && !await _db.Camiones.AnyAsync(c => c.IdCamion == peticion.IdCamion, ct))
            return "El camión seleccionado no existe.";

        return null;
    }

    private async Task AplicarAsync(List<int> idsViaje, AsignacionPeticion peticion, int idAdmin, CancellationToken ct)
    {
        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            // SEGURIDAD: transaccion para que un viaje nunca quede a medio asignar.
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

            var actuales = await _db.RutasEmpleado.Where(a => idsViaje.Contains(a.IdRutaTrabajo)).ToListAsync(ct);
            _db.RutasEmpleado.RemoveRange(actuales.Where(a => !peticion.IdEmpleados.Contains(a.IdEmpleado)));

            foreach (var idViaje in idsViaje)
            {
                foreach (var idEmpleado in peticion.IdEmpleados)
                {
                    if (!actuales.Any(a => a.IdRutaTrabajo == idViaje && a.IdEmpleado == idEmpleado))
                        _db.RutasEmpleado.Add(new RutaEmpleado { IdRutaTrabajo = idViaje, IdEmpleado = idEmpleado });
                }
            }

            await _db.RutasTrabajo
                .Where(r => idsViaje.Contains(r.IdRutaTrabajo))
                .ExecuteUpdateAsync(c => c.SetProperty(r => r.IdCamion, peticion.IdCamion), ct);

            // Los viajes extra llevan su personal tambien en viaje_extra_empleado (lo
            // muestra la app de escritorio); se mantiene igual al del viaje.
            var extras = await _db.ViajesExtra
                .Where(v => v.IdRutaTrabajo != null && idsViaje.Contains(v.IdRutaTrabajo.Value))
                .ToListAsync(ct);
            if (extras.Count > 0)
            {
                var idsExtra = extras.Select(v => v.IdViajeExtra).ToList();
                var personalExtra = await _db.ViajeExtraEmpleados.Where(p => idsExtra.Contains(p.IdViajeExtra)).ToListAsync(ct);
                _db.ViajeExtraEmpleados.RemoveRange(personalExtra.Where(p => !peticion.IdEmpleados.Contains(p.IdEmpleado)));
                foreach (var extra in extras)
                {
                    if (peticion.IdCamion is not null)
                        extra.IdCamion = peticion.IdCamion.Value;
                    foreach (var idEmpleado in peticion.IdEmpleados)
                    {
                        if (!personalExtra.Any(p => p.IdViajeExtra == extra.IdViajeExtra && p.IdEmpleado == idEmpleado))
                            _db.ViajeExtraEmpleados.Add(new ViajeExtraEmpleado { IdViajeExtra = extra.IdViajeExtra, IdEmpleado = idEmpleado });
                    }
                }
            }

            // SEGURIDAD: cada cambio de asignacion queda en la auditoria con quien lo hizo.
            _db.Auditorias.Add(new Auditoria
            {
                IdEmpleado = idAdmin,
                TablaAfectada = "ruta_empleado",
                IdRegistro = idsViaje.Count == 1 ? $"{idsViaje[0]}" : $"{idsViaje.Count} viajes",
                Accion = "UPDATE",
                FechaHora = DateTime.Now,
                ValorNuevo = JsonSerializer.Serialize(new { viajes = idsViaje, peticion.IdEmpleados, peticion.IdCamion }),
                Detalle = "App móvil: asignación de personal a viajes"
            });

            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });
    }
}
