using APISacor.Data;
using APISacor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using APISacor.Models;

namespace APISacor.Controllers;

// Solo datos propios del técnico identificado por el token individual.
[ApiController]
[Route("api/movil/tecnico")]
[Produces("application/json")]
public sealed class TecnicoController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    public TecnicoController(SacorDbContext db, ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    [HttpGet("inicio")]
    public async Task<IActionResult> Inicio(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "La sesión es inválida o ha vencido." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new { mensaje = "No tienes permiso para consultar este módulo." });

        var id = empleado.IdEmpleado;
        Response.Headers.CacheControl = "no-store";

        var horarios = await _db.HorariosTrabajo.AsNoTracking()
            .Where(h => h.IdEmpleadoMarco == id)
            .OrderByDescending(h => h.Fecha)
            .ThenByDescending(h => h.IdHorarioTrabajo)
            .Take(20)
            .Select(h => new { h.IdHorarioTrabajo, h.Fecha, h.Entrada, h.Salida })
            .ToListAsync(ct);

        var rutas = await (
            from asignacion in _db.RutasEmpleado.AsNoTracking()
            join ruta in _db.RutasTrabajo.AsNoTracking()
                on asignacion.IdRutaTrabajo equals ruta.IdRutaTrabajo
            where asignacion.IdEmpleado == id
            orderby asignacion.IdRutaEmpleado descending
            select new
            {
                asignacion.IdRutaEmpleado,
                ruta.IdRutaTrabajo,
                ruta.Destino,
                ruta.Tipo
            }).Take(30).ToListAsync(ct);

        // Limpiezas que el propio técnico registró. No se exponen las de otros.
        var limpiezas = await _db.Limpiezas.AsNoTracking()
            .Where(l => l.IdEmpleadoSubio == id)
            .OrderByDescending(l => l.Fecha)
            .ThenByDescending(l => l.IdLimpieza)
            .Take(20)
            .Select(l => new { l.IdLimpieza, l.IdServicioContrato, l.IdRutaTrabajo, l.Fecha, l.Observacion, l.Etapa })
            .ToListAsync(ct);

        var idsRutas = limpiezas.Where(l => l.IdRutaTrabajo != null).Select(l => l.IdRutaTrabajo!.Value).Distinct().ToList();
        var destinos = await _db.RutasTrabajo.AsNoTracking()
            .Where(r => idsRutas.Contains(r.IdRutaTrabajo))
            .Select(r => new { r.IdRutaTrabajo, r.Destino })
            .ToListAsync(ct);

        var idsLimpieza = limpiezas.Select(l => l.IdLimpieza).ToList();
        var fotos = await _db.EvidenciasFoto.AsNoTracking()
            .Where(f => idsLimpieza.Contains(f.IdLimpieza))
            .OrderBy(f => f.IdEvidenciaFoto)
            .Select(f => new { f.IdLimpieza, f.IdEvidenciaFoto })
            .ToListAsync(ct);

        var limpiezasConFotos = limpiezas.Select(l => new
        {
            l.IdLimpieza,
            l.IdServicioContrato,
            l.IdRutaTrabajo,
            destino = ConsultasMovil.LeerDestino(destinos.FirstOrDefault(d => d.IdRutaTrabajo == l.IdRutaTrabajo)?.Destino ?? string.Empty).Texto,
            fechaViaje = ConsultasMovil.LeerDestino(destinos.FirstOrDefault(d => d.IdRutaTrabajo == l.IdRutaTrabajo)?.Destino ?? string.Empty).Fecha?.ToString("yyyy-MM-dd"),
            l.Fecha,
            l.Observacion,
            l.Etapa,
            fotos = fotos.Where(f => f.IdLimpieza == l.IdLimpieza).Select(f => f.IdEvidenciaFoto).ToList()
        });

        // Un pesaje puede corresponder al técnico como responsable o como autor.
        var pesajes = await _db.Pesajes.AsNoTracking()
            .Where(p => p.IdTecnicoTransporte == id || p.IdEmpleadoSubio == id)
            .OrderByDescending(p => p.Fecha)
            .ThenByDescending(p => p.IdPesaje)
            .Take(20)
            .Select(p => new { p.IdPesaje, p.IdServicioContrato, p.Fecha, p.PesoTotal, p.Etapa })
            .ToListAsync(ct);

        return Ok(new
        {
            empleado = ServicioAccesoMovil.Perfil(empleado),
            rutas,
            horarios,
            limpiezas = limpiezasConFotos,
            pesajes
        });
    }

    // GET: api/movil/tecnico/servicios
    // Devuelve los servicios de las rutas asignadas al técnico.
    [HttpGet("servicios")]
    public async Task<IActionResult> MisServicios(
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);

        if (empleado is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o vencida."
            });

        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new
            {
                mensaje = "No tienes permiso."
            });

        var servicios = await (
            from servicio in ConsultasMovil.ServiciosDelEmpleado(_db, empleado.IdEmpleado).AsNoTracking()
            join ruta in _db.RutasTrabajo.AsNoTracking()
                on servicio.IdRutaTrabajo equals ruta.IdRutaTrabajo
            orderby servicio.IdServicioContrato
            select new
            {
                servicio.IdServicioContrato,
                servicio.IdRutaTrabajo,
                ruta.Destino
            }
        )
        .Take(100)
        .ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(servicios);
    }

    public sealed class CargaPeticion
    {
        [Range(1, int.MaxValue)]
        public int IdPrecioLugar { get; set; }

        [Range(0.01, 45)]
        public decimal Toneladas { get; set; }
    }

    public sealed class GastoPeticion
    {
        [Required]
        [StringLength(40)]
        public string Concepto { get; set; } = string.Empty;

        [Range(0.01, 100000)]
        public decimal Monto { get; set; }

        [StringLength(250)]
        public string? Descripcion { get; set; }
    }

    public sealed class PeticionRecoleccion
    {
        [Range(1, int.MaxValue)]
        public int IdRutaTrabajo { get; set; }

        [StringLength(1000)]
        public string? Observacion { get; set; }

        // SEGURIDAD: listas acotadas para que una sola peticion no inserte cientos de filas.
        [MaxLength(10)]
        public List<CargaPeticion> Cargas { get; set; } = new();

        [MaxLength(15)]
        public List<GastoPeticion> Gastos { get; set; } = new();
    }

    // Materiales que se pueden cobrar en un viaje: los precios por tonelada de la
    // misma plaza del contrato (el del contrato va primero).
    private async Task<List<PrecioLugar>> MaterialesDelViajeAsync(int idRutaTrabajo, CancellationToken ct)
    {
        var idContrato = await ConsultasMovil.ContratoDelViajeAsync(_db, idRutaTrabajo, ct);
        if (idContrato is null)
            return new List<PrecioLugar>();

        var precioContrato = await (
            from s in _db.ServiciosContrato.AsNoTracking()
            join p in _db.PreciosLugar.AsNoTracking() on s.IdPrecioLugar equals p.IdPrecioLugar
            where s.IdServicioContrato == idContrato
            select p
        ).FirstOrDefaultAsync(ct);
        if (precioContrato is null)
            return new List<PrecioLugar>();

        var mismosLugar = await _db.PreciosLugar.AsNoTracking()
            .Where(p => p.Lugar == precioContrato.Lugar)
            .OrderBy(p => p.Tipo)
            .ToListAsync(ct);

        return mismosLugar
            .OrderBy(p => p.IdPrecioLugar == precioContrato.IdPrecioLugar ? 0 : 1)
            .ToList();
    }

    // GET: api/movil/tecnico/viajes/5/materiales
    [HttpGet("viajes/{id:int}/materiales")]
    public async Task<IActionResult> Materiales(int id, CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "Sesión inválida o vencida." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new { mensaje = "No tienes permiso." });
        if (!await ConsultasMovil.EstaAsignadoAsync(_db, id, empleado.IdEmpleado, ct))
            return NotFound(new { mensaje = "No estás asignado a ese viaje." });

        var materiales = await MaterialesDelViajeAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            conceptosGasto = ReglasGastoViaje.Conceptos,
            materiales = materiales.Select(p => new { p.IdPrecioLugar, p.Tipo, p.Lugar, p.Precio })
        });
    }

    // POST: api/movil/tecnico/recoleccion
    // Registra en un solo paso lo ocurrido en el viaje: observacion (con fotos aparte),
    // toneladas movidas por material (pesaje) y gastos del viaje.
    [HttpPost("recoleccion")]
    public async Task<IActionResult> RegistrarRecoleccion(
        [FromBody] PeticionRecoleccion peticion,
        CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "Sesión inválida o vencida." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new { mensaje = "No tienes permiso." });

        // SEGURIDAD: el tecnico solo registra en viajes a los que esta asignado.
        if (!await ConsultasMovil.EstaAsignadoAsync(_db, peticion.IdRutaTrabajo, empleado.IdEmpleado, ct))
            return BadRequest(new { mensaje = "No estás asignado a ese viaje." });

        var observacion = peticion.Observacion?.Trim();
        if (string.IsNullOrWhiteSpace(observacion) && peticion.Cargas.Count == 0 && peticion.Gastos.Count == 0)
            return BadRequest(new { mensaje = "Registra al menos una carga, un gasto o una observación." });

        var idContrato = await ConsultasMovil.ContratoDelViajeAsync(_db, peticion.IdRutaTrabajo, ct);

        // SEGURIDAD: cada material debe ser uno de los precios de la plaza del contrato;
        // el celular no puede inventar precios ni cobrar con los de otra plaza.
        var materiales = await MaterialesDelViajeAsync(peticion.IdRutaTrabajo, ct);
        if (peticion.Cargas.Count > 0 && (idContrato is null || materiales.Count == 0))
            return BadRequest(new { mensaje = "Este viaje no tiene contrato con precios; no se pueden registrar toneladas." });
        foreach (var carga in peticion.Cargas)
        {
            if (!materiales.Any(m => m.IdPrecioLugar == carga.IdPrecioLugar))
                return BadRequest(new { mensaje = "Uno de los materiales no corresponde a este contrato." });
            if (carga.Toneladas <= 0 || carga.Toneladas > ReglasGastoViaje.MaxToneladasPorCarga ||
                decimal.Round(carga.Toneladas, 2) != carga.Toneladas)
                return BadRequest(new { mensaje = "Las toneladas deben estar entre 0.01 y 45, con máximo dos decimales." });
        }

        var gastos = new List<(string Concepto, decimal Monto, string? Descripcion)>();
        foreach (var gasto in peticion.Gastos)
        {
            var concepto = ReglasGastoViaje.Normalizar(gasto.Concepto);
            if (concepto is null)
                return BadRequest(new { mensaje = $"El concepto del gasto debe ser uno de: {string.Join(", ", ReglasGastoViaje.Conceptos)}." });
            if (gasto.Monto <= 0 || gasto.Monto > 100_000 || decimal.Round(gasto.Monto, 2) != gasto.Monto)
                return BadRequest(new { mensaje = "Cada gasto debe estar entre $0.01 y $100,000, con máximo dos decimales." });
            var descripcion = string.IsNullOrWhiteSpace(gasto.Descripcion) ? null : gasto.Descripcion.Trim();
            gastos.Add((concepto, gasto.Monto, descripcion));
        }

        // La fecha contable es la del viaje si ya ocurrio; si no, la de hoy.
        var hoy = ConsultasMovil.HoyLocal();
        var destino = await _db.RutasTrabajo.AsNoTracking()
            .Where(r => r.IdRutaTrabajo == peticion.IdRutaTrabajo)
            .Select(r => r.Destino)
            .FirstAsync(ct);
        var fechaViaje = ConsultasMovil.LeerDestino(destino).Fecha;
        var fecha = fechaViaje is not null && fechaViaje <= hoy ? fechaViaje.Value : hoy;

        var estrategia = _db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync<IActionResult>(async () =>
        {
            // SEGURIDAD: observacion, cargas y gastos se guardan juntos o nada.
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

            var limpieza = new Limpieza
            {
                IdEmpleadoSubio = empleado.IdEmpleado,
                IdRutaTrabajo = peticion.IdRutaTrabajo,
                IdServicioContrato = idContrato,
                Fecha = hoy,
                Observacion = string.IsNullOrWhiteSpace(observacion) ? "Recolección registrada." : observacion,
                Etapa = EtapasEvidencia.Recoleccion
            };
            _db.Limpiezas.Add(limpieza);

            foreach (var carga in peticion.Cargas)
            {
                _db.Pesajes.Add(new Pesaje
                {
                    IdServicioContrato = idContrato!.Value,
                    IdEmpleadoSubio = empleado.IdEmpleado,
                    IdTecnicoTransporte = empleado.IdEmpleado,
                    IdPrecioLugar = carga.IdPrecioLugar,
                    IdRutaTrabajo = peticion.IdRutaTrabajo,
                    Fecha = fecha,
                    PesoTotal = decimal.Round(carga.Toneladas * ReglasGastoViaje.LibrasPorTonelada, 2),
                    PrecioTonelada = materiales.First(m => m.IdPrecioLugar == carga.IdPrecioLugar).Precio,
                    Etapa = "App móvil"
                });
            }

            foreach (var gasto in gastos)
            {
                _db.GastosViaje.Add(new GastoViaje
                {
                    IdRutaTrabajo = peticion.IdRutaTrabajo,
                    IdEmpleado = empleado.IdEmpleado,
                    Fecha = fecha,
                    Concepto = gasto.Concepto,
                    Monto = gasto.Monto,
                    Descripcion = gasto.Descripcion
                });
            }

            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            var total = peticion.Cargas.Sum(c => c.Toneladas * materiales.First(m => m.IdPrecioLugar == c.IdPrecioLugar).Precio);

            Response.Headers.CacheControl = "no-store";
            return Ok(new
            {
                limpieza.IdLimpieza,
                limpieza.IdRutaTrabajo,
                toneladas = peticion.Cargas.Sum(c => c.Toneladas),
                totalCobrado = decimal.Round(total, 2),
                totalGastos = gastos.Sum(g => g.Monto),
                mensaje = "Recolección registrada correctamente."
            });
        });
    }

    // GET: api/movil/tecnico/registros
    // Historial del tecnico agrupado por viaje: cargas, gastos y observaciones.
    [HttpGet("registros")]
    public async Task<IActionResult> Registros(CancellationToken ct)
    {
        var empleado = await _acceso.IdentificarAsync(Request, ct);
        if (empleado is null)
            return Unauthorized(new { mensaje = "Sesión inválida o vencida." });
        if (ServicioAccesoMovil.RolApp(empleado.Tipo) != "tecnico")
            return StatusCode(403, new { mensaje = "No tienes permiso." });

        var id = empleado.IdEmpleado;

        var observaciones = await _db.Limpiezas.AsNoTracking()
            .Where(l => l.IdEmpleadoSubio == id && l.IdRutaTrabajo != null && l.Etapa == EtapasEvidencia.Recoleccion)
            .OrderByDescending(l => l.IdLimpieza)
            .Take(100)
            .Select(l => new { IdRutaTrabajo = l.IdRutaTrabajo!.Value, l.IdLimpieza, l.Fecha, l.Observacion })
            .ToListAsync(ct);

        var cargas = await (
            from p in _db.Pesajes.AsNoTracking()
            join pl in _db.PreciosLugar.AsNoTracking() on p.IdPrecioLugar equals pl.IdPrecioLugar
            where p.IdEmpleadoSubio == id && p.IdRutaTrabajo != null
            orderby p.IdPesaje descending
            select new { IdRutaTrabajo = p.IdRutaTrabajo!.Value, p.PesoTotal, pl.Tipo, Precio = p.PrecioTonelada ?? pl.Precio }
        ).Take(300).ToListAsync(ct);

        var gastos = await _db.GastosViaje.AsNoTracking()
            .Where(g => g.IdEmpleado == id)
            .OrderByDescending(g => g.IdGastoViaje)
            .Take(300)
            .Select(g => new { g.IdRutaTrabajo, g.Concepto, g.Monto, g.Descripcion })
            .ToListAsync(ct);

        var idsLimpieza = observaciones.Select(o => o.IdLimpieza).ToList();
        var fotos = await _db.EvidenciasFoto.AsNoTracking()
            .Where(f => idsLimpieza.Contains(f.IdLimpieza))
            .Select(f => new { f.IdLimpieza, f.IdEvidenciaFoto })
            .ToListAsync(ct);

        var idsViaje = observaciones.Select(o => o.IdRutaTrabajo)
            .Concat(cargas.Select(c => c.IdRutaTrabajo))
            .Concat(gastos.Select(g => g.IdRutaTrabajo))
            .Distinct()
            .ToList();
        var viajes = await ConsultasMovil.ResumirViajesAsync(_db, idsViaje, ct);

        Response.Headers.CacheControl = "no-store";
        return Ok(viajes
            .OrderByDescending(v => v.Fecha ?? "")
            .Select(v => new
            {
                v.IdRutaTrabajo,
                v.Fecha,
                v.Destino,
                v.Cliente,
                cargas = cargas.Where(c => c.IdRutaTrabajo == v.IdRutaTrabajo).Select(c =>
                {
                    var toneladas = decimal.Round((c.PesoTotal ?? 0) / ReglasGastoViaje.LibrasPorTonelada, 2);
                    return new { material = c.Tipo, toneladas, precio = c.Precio, monto = decimal.Round(toneladas * c.Precio, 2) };
                }).ToList(),
                gastos = gastos.Where(g => g.IdRutaTrabajo == v.IdRutaTrabajo)
                    .Select(g => new { g.Concepto, g.Monto, g.Descripcion }).ToList(),
                observaciones = observaciones.Where(o => o.IdRutaTrabajo == v.IdRutaTrabajo).Select(o => new
                {
                    o.IdLimpieza,
                    o.Fecha,
                    o.Observacion,
                    fotos = fotos.Where(f => f.IdLimpieza == o.IdLimpieza).Select(f => f.IdEvidenciaFoto).ToList()
                }).ToList()
            }));
    }
}
