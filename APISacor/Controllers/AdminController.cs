using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using APISacor.Data;
using APISacor.Models;
using APISacor.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

[ApiController]
[Route("api/movil/admin")]
[Produces("application/json")]
public sealed class AdminController : ControllerBase
{
    private readonly SacorDbContext _db;
    private readonly ServicioAccesoMovil _acceso;

    private static readonly string[] TiposPermitidos =
    {
        "Administrador",
        "Motorista",
        "Tecnico",
        "Ayudante",
        "Mecanico",
        "Coordinador"
    };

    private static readonly string[] EstadosPermitidos =
    {
        "Activo",
        "Inactivo",
        "Suspendido"
    };

    public AdminController(
        SacorDbContext db,
        ServicioAccesoMovil acceso)
    {
        _db = db;
        _acceso = acceso;
    }

    private async Task<Empleado?> ObtenerAdminAsync(
        CancellationToken ct)
    {
        var empleado =
            await _acceso.IdentificarAsync(Request, ct);

        if (empleado is null)
            return null;

        return ServicioAccesoMovil.RolApp(empleado.Tipo)
            == "administrador"
                ? empleado
                : null;
    }

    private async Task<string> GenerarCodigoAsync(
        string tipo,
        CancellationToken ct)
    {
        var esAdmin = tipo.Equals(
            "Administrador",
            StringComparison.OrdinalIgnoreCase
        );

        var minimo = esAdmin ? 100000 : 1000;
        var maximo = esAdmin ? 1000000 : 10000;

        for (var intento = 0; intento < 50; intento++)
        {
            var numero = RandomNumberGenerator.GetInt32(
                minimo,
                maximo
            );

            var codigo = numero.ToString();

            var existe = await _db.Empleados
                .AnyAsync(
                    e => e.CodigoEmpleado == codigo,
                    ct
                );

            if (!existe)
                return codigo;
        }

        throw new InvalidOperationException(
            "No se pudo generar un código de empleado."
        );
    }

    public sealed class CrearEmpleadoPeticion
    {
        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(12)]
        public string Dui { get; set; } = string.Empty;

        [MaxLength(25)]
        public string? Telefono { get; set; }

        [Range(0, 9999999999.99)]
        public decimal Sueldo { get; set; }

        public DateTime FechaIngreso { get; set; }

        [MaxLength(30)]
        public string? CodigoAf { get; set; }

        [Required]
        [MaxLength(30)]
        public string Tipo { get; set; } = string.Empty;
    }

    public sealed class CambiarEstadoPeticion
    {
        [Required]
        [MaxLength(20)]
        public string Estado { get; set; } = string.Empty;
    }

    public sealed class CrearViajePeticion
    {
        [Range(1, int.MaxValue)]
        public int IdCliente { get; set; }

        [Range(1, int.MaxValue)]
        public int IdCamion { get; set; }

        [Required]
        [MaxLength(60)]
        public string Tipo { get; set; } = string.Empty;

        [Range(0, 9999999999.99)]
        public decimal? Precio { get; set; }

        [Range(0, 9999999999.99)]
        public decimal? PagoMonto { get; set; }

        [Range(1, 100000)]
        public int Cantidad { get; set; }

        [MinLength(1)]
        public List<int> IdEmpleados { get; set; } = new();
    }

    public sealed class CrearServicioExternoPeticion
    {
        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Range(0, 9999999999.99)]
        public decimal CantidadPago { get; set; }

        [MaxLength(250)]
        public string? Direccion { get; set; }
    }

    // -------------------------------------------------
    // INICIO
    // -------------------------------------------------

    [HttpGet("inicio")]
    public async Task<IActionResult> Inicio(
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var empleados = await _db.Empleados
            .CountAsync(ct);

        var viajes = await _db.ViajesExtra
            .CountAsync(
                v => v.IdAdministrador == admin.IdEmpleado,
                ct
            );

        var servicios = await _db.ServiciosExternos
            .CountAsync(
                s => s.IdAdministrador == admin.IdEmpleado,
                ct
            );

        Response.Headers.CacheControl = "no-store";

        return Ok(new
        {
            empleado = ServicioAccesoMovil.Perfil(admin),
            empleados,
            viajes,
            servicios
        });
    }

    // -------------------------------------------------
    // EMPLEADOS
    // -------------------------------------------------

    [HttpGet("empleados")]
    public async Task<IActionResult> Empleados(
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var empleados = await _db.Empleados
            .AsNoTracking()
            .OrderByDescending(e => e.IdEmpleado)
            .Take(200)
            .Select(e => new
            {
                e.IdEmpleado,
                e.CodigoEmpleado,
                e.Nombre,
                e.Dui,
                e.Telefono,
                e.Sueldo,
                e.FechaIngreso,
                e.CodigoAf,
                e.Estado,
                e.Tipo
            })
            .ToListAsync(ct);

        return Ok(empleados);
    }

    [HttpPost("empleados")]
    public async Task<IActionResult> CrearEmpleado(
        [FromBody] CrearEmpleadoPeticion peticion,
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var nombre = peticion.Nombre.Trim();
        var dui = peticion.Dui.Trim();
        var tipo = peticion.Tipo.Trim();

        if (!Regex.IsMatch(dui, @"^\d{8}-\d$"))
            return BadRequest(new
            {
                mensaje = "El DUI debe tener formato 00000000-0."
            });

        if (!TiposPermitidos.Contains(
            tipo,
            StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                mensaje = "El tipo de empleado no es válido."
            });
        }

        var tipoNormalizado =
            TiposPermitidos.First(t =>
                t.Equals(
                    tipo,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (
            peticion.FechaIngreso < new DateTime(1990, 1, 1) ||
            peticion.FechaIngreso > DateTime.Today.AddDays(1)
        )
        {
            return BadRequest(new
            {
                mensaje = "La fecha de ingreso no es válida."
            });
        }

        var duiExiste = await _db.Empleados
            .AnyAsync(e => e.Dui == dui, ct);

        if (duiExiste)
            return Conflict(new
            {
                mensaje = "Ya existe un empleado con ese DUI."
            });

        var codigo = await GenerarCodigoAsync(
            tipoNormalizado,
            ct
        );

        var empleado = new Empleado
        {
            CodigoEmpleado = codigo,
            Nombre = nombre,
            Dui = dui,
            Telefono = string.IsNullOrWhiteSpace(
                peticion.Telefono
            )
                ? null
                : peticion.Telefono.Trim(),

            Sueldo = peticion.Sueldo,
            FechaIngreso = peticion.FechaIngreso.Date,

            CodigoAf = string.IsNullOrWhiteSpace(
                peticion.CodigoAf
            )
                ? null
                : peticion.CodigoAf.Trim(),

            Estado = "Activo",
            Tipo = tipoNormalizado,

            IdAdministradorRegistro =
                admin.IdEmpleado
        };

        _db.Empleados.Add(empleado);

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            empleado.IdEmpleado,
            empleado.CodigoEmpleado,
            empleado.Nombre,
            empleado.Tipo,
            empleado.Estado,
            mensaje = "Empleado registrado correctamente."
        });
    }

    [HttpPatch("empleados/{id:int}/estado")]
    public async Task<IActionResult> CambiarEstadoEmpleado(
        int id,
        [FromBody] CambiarEstadoPeticion peticion,
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        if (id <= 0)
            return BadRequest(new
            {
                mensaje = "Empleado inválido."
            });

        var estado = peticion.Estado.Trim();

        if (!EstadosPermitidos.Contains(
            estado,
            StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                mensaje = "Estado inválido."
            });
        }

        var estadoNormalizado =
            EstadosPermitidos.First(e =>
                e.Equals(
                    estado,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (
            id == admin.IdEmpleado &&
            estadoNormalizado != "Activo"
        )
        {
            return BadRequest(new
            {
                mensaje = "No puedes desactivar tu propia cuenta."
            });
        }

        var actualizados = await _db.Empleados
            .Where(e => e.IdEmpleado == id)
            .ExecuteUpdateAsync(
                cambios => cambios.SetProperty(
                    e => e.Estado,
                    estadoNormalizado
                ),
                ct
            );

        if (actualizados == 0)
            return NotFound(new
            {
                mensaje = "El empleado no existe."
            });

        return Ok(new
        {
            mensaje = "Estado actualizado correctamente."
        });
    }

    // -------------------------------------------------
    // VIAJES
    // -------------------------------------------------

    [HttpGet("viajes/catalogos")]
    public async Task<IActionResult> CatalogosViajes(
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var clientes = await _db.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Take(100)
            .Select(c => new
            {
                c.IdCliente,
                c.Nombre,
                c.Tipo
            })
            .ToListAsync(ct);

        var camiones = await _db.Camiones
            .AsNoTracking()
            .OrderBy(c => c.Placa)
            .Take(100)
            .Select(c => new
            {
                c.IdCamion,
                c.Placa,
                c.Nombre,
                c.Tipo,
                c.Estado
            })
            .ToListAsync(ct);

        var empleados = await _db.Empleados
            .AsNoTracking()
            .Where(e =>
                e.Estado == "Activo" &&
                (
                    e.Tipo == "Motorista" ||
                    e.Tipo == "Tecnico"
                )
            )
            .OrderBy(e => e.Nombre)
            .Select(e => new
            {
                e.IdEmpleado,
                e.Nombre,
                e.CodigoEmpleado,
                e.Tipo
            })
            .ToListAsync(ct);

        return Ok(new
        {
            clientes,
            camiones,
            empleados
        });
    }

    [HttpGet("viajes")]
    public async Task<IActionResult> Viajes(
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var viajes = await (
            from viaje in _db.ViajesExtra.AsNoTracking()

            join cliente in _db.Clientes.AsNoTracking()
                on viaje.IdCliente equals cliente.IdCliente

            join camion in _db.Camiones.AsNoTracking()
                on viaje.IdCamion equals camion.IdCamion

            where viaje.IdAdministrador == admin.IdEmpleado

            orderby viaje.IdViajeExtra descending

            select new
            {
                viaje.IdViajeExtra,
                viaje.IdCliente,
                cliente = cliente.Nombre,

                viaje.IdCamion,
                camion = camion.Nombre ?? camion.Placa,
                camion.Placa,

                viaje.Tipo,
                viaje.Precio,
                viaje.PagoMonto,
                viaje.Cantidad
            }
        )
        .Take(50)
        .ToListAsync(ct);

        var ids = viajes
            .Select(v => v.IdViajeExtra)
            .ToList();

        var asignaciones = await (
            from asignacion
                in _db.ViajeExtraEmpleados.AsNoTracking()

            join empleado in _db.Empleados.AsNoTracking()
                on asignacion.IdEmpleado
                equals empleado.IdEmpleado

            where ids.Contains(
                asignacion.IdViajeExtra
            )

            select new
            {
                asignacion.IdViajeExtra,
                empleado.IdEmpleado,
                empleado.Nombre,
                empleado.Tipo
            }
        )
        .ToListAsync(ct);

        var respuesta = viajes.Select(v => new
        {
            v.IdViajeExtra,
            v.IdCliente,
            v.cliente,
            v.IdCamion,
            v.camion,
            v.Placa,
            v.Tipo,
            v.Precio,
            v.PagoMonto,
            v.Cantidad,

            empleados = asignaciones
                .Where(a =>
                    a.IdViajeExtra ==
                    v.IdViajeExtra
                )
                .Select(a => new
                {
                    a.IdEmpleado,
                    a.Nombre,
                    a.Tipo
                })
                .ToList()
        });

        return Ok(respuesta);
    }

    // EL ADMIN CREA EL VIAJE Y LO ASIGNA
    // EN LA MISMA OPERACIÓN.
    [HttpPost("viajes")]
    public async Task<IActionResult> CrearViaje(
        [FromBody] CrearViajePeticion peticion,
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var tipo = peticion.Tipo.Trim();

        if (string.IsNullOrWhiteSpace(tipo))
            return BadRequest(new
            {
                mensaje = "Debes indicar el tipo de viaje."
            });

        var clienteExiste = await _db.Clientes
            .AnyAsync(
                c => c.IdCliente == peticion.IdCliente,
                ct
            );

        if (!clienteExiste)
            return BadRequest(new
            {
                mensaje = "El cliente seleccionado no existe."
            });

        var camionExiste = await _db.Camiones
            .AnyAsync(
                c => c.IdCamion == peticion.IdCamion,
                ct
            );

        if (!camionExiste)
            return BadRequest(new
            {
                mensaje = "El camión seleccionado no existe."
            });

        var idsEmpleados = peticion.IdEmpleados
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (idsEmpleados.Count == 0)
            return BadRequest(new
            {
                mensaje = "Selecciona al menos un empleado."
            });

        var empleadosValidos = await _db.Empleados
            .Where(e =>
                idsEmpleados.Contains(e.IdEmpleado) &&
                e.Estado == "Activo" &&
                (
                    e.Tipo == "Motorista" ||
                    e.Tipo == "Tecnico"
                )
            )
            .Select(e => e.IdEmpleado)
            .ToListAsync(ct);

        if (empleadosValidos.Count != idsEmpleados.Count)
            return BadRequest(new
            {
                mensaje =
                    "Uno o más empleados no son válidos para el viaje."
            });

        await using var transaccion =
            await _db.Database.BeginTransactionAsync(ct);

        var viaje = new ViajeExtra
        {
            IdCliente = peticion.IdCliente,
            IdAdministrador = admin.IdEmpleado,
            IdCamion = peticion.IdCamion,
            Tipo = tipo,
            Precio = peticion.Precio,
            PagoMonto = peticion.PagoMonto,
            Cantidad = peticion.Cantidad
        };

        _db.ViajesExtra.Add(viaje);

        await _db.SaveChangesAsync(ct);

        foreach (var idEmpleado in idsEmpleados)
        {
            _db.ViajeExtraEmpleados.Add(
                new ViajeExtraEmpleado
                {
                    IdViajeExtra =
                        viaje.IdViajeExtra,

                    IdEmpleado =
                        idEmpleado
                }
            );
        }

        await _db.SaveChangesAsync(ct);

        await transaccion.CommitAsync(ct);

        return Ok(new
        {
            viaje.IdViajeExtra,
            mensaje =
                "Viaje creado y asignado correctamente."
        });
    }

    // -------------------------------------------------
    // SERVICIOS EXTERNOS
    // -------------------------------------------------

    [HttpGet("servicios-externos")]
    public async Task<IActionResult> ServiciosExternos(
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var servicios = await _db.ServiciosExternos
            .AsNoTracking()
            .Where(s =>
                s.IdAdministrador ==
                admin.IdEmpleado
            )
            .OrderByDescending(s => s.IdServicioExt)
            .Take(100)
            .Select(s => new
            {
                s.IdServicioExt,
                s.Nombre,
                s.CantidadPago,
                s.Direccion
            })
            .ToListAsync(ct);

        return Ok(servicios);
    }

    [HttpPost("servicios-externos")]
    public async Task<IActionResult> CrearServicioExterno(
        [FromBody] CrearServicioExternoPeticion peticion,
        CancellationToken ct)
    {
        var admin = await ObtenerAdminAsync(ct);

        if (admin is null)
            return Unauthorized(new
            {
                mensaje = "Sesión inválida o sin permisos."
            });

        var nombre = peticion.Nombre.Trim();

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new
            {
                mensaje = "El nombre es obligatorio."
            });

        var servicio = new ServicioExterno
        {
            IdAdministrador = admin.IdEmpleado,
            Nombre = nombre,
            CantidadPago = peticion.CantidadPago,

            Direccion = string.IsNullOrWhiteSpace(
                peticion.Direccion
            )
                ? null
                : peticion.Direccion.Trim()
        };

        _db.ServiciosExternos.Add(servicio);

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            servicio.IdServicioExt,
            servicio.Nombre,
            servicio.CantidadPago,
            servicio.Direccion,

            mensaje =
                "Servicio externo registrado correctamente."
        });
    }
}