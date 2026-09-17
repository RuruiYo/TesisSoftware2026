using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

// SEGURIDAD: cada propiedad lleva [MaxLength] con el mismo tamano que la columna
// de SQL Server. Esto valida la longitud ANTES de tocar la base de datos y evita
// que un payload gigante llegue al motor (proteccion contra desbordamiento y DoS).
// SEGURIDAD: las propiedades de navegacion llevan [JsonIgnore] para que el cliente
// no pueda enviar objetos anidados y crear registros en cascada sin autorizacion
// (ataque de over-posting / mass assignment) y para evitar ciclos al serializar.

public abstract class EntidadBase
{
    // Acceso uniforme a la clave primaria para el controlador base generico.
    // [NotMapped] es obligatorio: es una propiedad calculada, no una columna, y sin
    // el atributo EF Core intentaria crear una columna "Id" que no existe en la tabla.
    // Las consultas contra la base NO usan esta propiedad (no se puede traducir a SQL);
    // el controlador base consulta por la clave real con EF.Property.
    [NotMapped]
    public abstract int Id { get; }
}

[Table("empleado")]
public class Empleado : EntidadBase
{
    [Key]
    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Required]
    [MaxLength(30)]
    [Column("codigo_empleado")]
    public string CodigoEmpleado { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(12)]
    [Column("dui")]
    public string Dui { get; set; } = string.Empty;

    [MaxLength(25)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    // SEGURIDAD: [Range] impide montos negativos o absurdos que corrompan la nomina.
    [Range(0, 9999999999.99)]
    [Column("sueldo", TypeName = "decimal(12,2)")]
    public decimal Sueldo { get; set; }

    [Column("fecha_ingreso", TypeName = "date")]
    public DateTime FechaIngreso { get; set; }

    [MaxLength(30)]
    [Column("codigo_af")]
    public string? CodigoAf { get; set; }

    // SEGURIDAD: el token nunca se devuelve al cliente ni se acepta desde el cliente.
    // [JsonIgnore] lo saca por completo del contrato HTTP en las dos direcciones.
    [JsonIgnore]
    [MaxLength(255)]
    [Column("token")]
    public string? Token { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("estado")]
    public string Estado { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Column("id_administrador_registro")]
    public int? IdAdministradorRegistro { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorRegistro))]
    public Empleado? AdministradorRegistro { get; set; }

    public override int Id => IdEmpleado;
}

[Table("cliente")]
public class Cliente : EntidadBase
{
    [Key]
    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [MaxLength(250)]
    [Column("direccion")]
    public string? Direccion { get; set; }

    // SEGURIDAD: [EmailAddress] rechaza cadenas que no sean correos y corta
    // intentos de inyeccion de cabeceras si el correo se usa luego para enviar mail.
    [EmailAddress]
    [MaxLength(254)]
    [Column("correo")]
    public string? Correo { get; set; }

    [MaxLength(25)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    [MaxLength(20)]
    [Column("nit")]
    public string? Nit { get; set; }

    [MaxLength(12)]
    [Column("dui")]
    public string? Dui { get; set; }

    [MaxLength(150)]
    [Column("giro")]
    public string? Giro { get; set; }

    [MaxLength(30)]
    [Column("nrc")]
    public string? Nrc { get; set; }

    [MaxLength(150)]
    [Column("nombre_comercial")]
    public string? NombreComercial { get; set; }

    [Column("id_administrador_registro")]
    public int? IdAdministradorRegistro { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorRegistro))]
    public Empleado? AdministradorRegistro { get; set; }

    public override int Id => IdCliente;
}

[Table("ruta_trabajo")]
public class RutaTrabajo : EntidadBase
{
    [Key]
    [Column("id_ruta_trabajo")]
    public int IdRutaTrabajo { get; set; }

    [Column("id_administrador")]
    public int IdAdministrador { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("destino")]
    public string Destino { get; set; } = string.Empty;

    [Range(0, 9999999999.99)]
    [Column("dinero_gasolina", TypeName = "decimal(12,2)")]
    public decimal? DineroGasolina { get; set; }

    [MaxLength(60)]
    [Column("tipo")]
    public string? Tipo { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministrador))]
    public Empleado? Administrador { get; set; }

    public override int Id => IdRutaTrabajo;
}

[Table("ruta_empleado")]
public class RutaEmpleado : EntidadBase
{
    [Key]
    [Column("id_ruta_empleado")]
    public int IdRutaEmpleado { get; set; }

    [Column("id_ruta_trabajo")]
    public int IdRutaTrabajo { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdRutaTrabajo))]
    public RutaTrabajo? RutaTrabajo { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }

    public override int Id => IdRutaEmpleado;
}

[Table("camion")]
public class Camion : EntidadBase
{
    [Key]
    [Column("id_camion")]
    public int IdCamion { get; set; }

    [Required]
    [MaxLength(15)]
    [Column("placa")]
    public string Placa { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("nombre")]
    public string? Nombre { get; set; }

    [MaxLength(60)]
    [Column("tipo")]
    public string? Tipo { get; set; }

    [MaxLength(40)]
    [Column("estado")]
    public string? Estado { get; set; }

    [MaxLength(150)]
    [Column("dueño")]
    public string? Dueno { get; set; }

    [MaxLength(1000)]
    [Column("observacion")]
    public string? Observacion { get; set; }

    public override int Id => IdCamion;
}

[Table("camion_conductor")]
public class CamionConductor : EntidadBase
{
    [Key]
    [Column("id_camion_conductor")]
    public int IdCamionConductor { get; set; }

    [Column("id_camion")]
    public int IdCamion { get; set; }

    [Column("id_conductor")]
    public int IdConductor { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdCamion))]
    public Camion? Camion { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdConductor))]
    public Empleado? Conductor { get; set; }

    public override int Id => IdCamionConductor;
}

[Table("horario_trabajo")]
public class HorarioTrabajo : EntidadBase
{
    [Key]
    [Column("id_horario_trabajo")]
    public int IdHorarioTrabajo { get; set; }

    [Column("id_empleado_marco")]
    public int IdEmpleadoMarco { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [Column("entrada", TypeName = "time(0)")]
    public TimeSpan Entrada { get; set; }

    [Column("salida", TypeName = "time(0)")]
    public TimeSpan Salida { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleadoMarco))]
    public Empleado? EmpleadoMarco { get; set; }

    public override int Id => IdHorarioTrabajo;
}

[Table("servicio_externo")]
public class ServicioExterno : EntidadBase
{
    [Key]
    [Column("id_servicio_ext")]
    public int IdServicioExt { get; set; }

    [Column("id_administrador")]
    public int IdAdministrador { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 9999999999.99)]
    [Column("cantidad_pago", TypeName = "decimal(12,2)")]
    public decimal CantidadPago { get; set; }

    [MaxLength(250)]
    [Column("direccion")]
    public string? Direccion { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministrador))]
    public Empleado? Administrador { get; set; }

    public override int Id => IdServicioExt;
}

[Table("pago")]
public class Pago : EntidadBase
{
    [Key]
    [Column("id_pago")]
    public int IdPago { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [Range(0, 9999999999.99)]
    [Column("cantidad", TypeName = "decimal(12,2)")]
    public decimal Cantidad { get; set; }

    [Range(0, 9999999999.99)]
    [Column("descuento", TypeName = "decimal(12,2)")]
    public decimal Descuento { get; set; }

    [MaxLength(1000)]
    [Column("observacion")]
    public string? Observacion { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }

    public override int Id => IdPago;
}

[Table("anticipo_sueldo")]
public class AnticipoSueldo : EntidadBase
{
    [Key]
    [Column("id_anticipo")]
    public int IdAnticipo { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Range(0, 9999999999.99)]
    [Column("cantidad", TypeName = "decimal(12,2)")]
    public decimal Cantidad { get; set; }

    [MaxLength(1000)]
    [Column("observacion")]
    public string? Observacion { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }

    public override int Id => IdAnticipo;
}

[Table("precio_lugar")]
public class PrecioLugar : EntidadBase
{
    [Key]
    [Column("id_precio_lugar")]
    public int IdPrecioLugar { get; set; }

    [Required]
    [MaxLength(60)]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("lugar")]
    public string Lugar { get; set; } = string.Empty;

    [Range(0, 9999999999.99)]
    [Column("precio", TypeName = "decimal(12,2)")]
    public decimal Precio { get; set; }

    [Column("fecha_modificacion", TypeName = "date")]
    public DateTime? FechaModificacion { get; set; }

    public override int Id => IdPrecioLugar;
}

[Table("servicio_contrato")]
public class ServicioContrato : EntidadBase
{
    [Key]
    [Column("id_servicio_contrato")]
    public int IdServicioContrato { get; set; }

    [Column("id_ruta_trabajo")]
    public int IdRutaTrabajo { get; set; }

    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [Column("id_precio_lugar")]
    public int IdPrecioLugar { get; set; }

    [MaxLength(1000)]
    [Column("observacion")]
    public string? Observacion { get; set; }

    [MaxLength(80)]
    [Column("tipo_transportaje")]
    public string? TipoTransportaje { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime? Fecha { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdRutaTrabajo))]
    public RutaTrabajo? RutaTrabajo { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdCliente))]
    public Cliente? Cliente { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdPrecioLugar))]
    public PrecioLugar? PrecioLugar { get; set; }

    public override int Id => IdServicioContrato;
}

[Table("limpieza")]
public class Limpieza : EntidadBase
{
    [Key]
    [Column("id_limpieza")]
    public int IdLimpieza { get; set; }

    [Column("id_empleado_subio")]
    public int IdEmpleadoSubio { get; set; }

    [Column("id_servicio_contrato")]
    public int IdServicioContrato { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [MaxLength(1000)]
    [Column("observacion")]
    public string? Observacion { get; set; }

    [MaxLength(60)]
    [Column("etapa")]
    public string? Etapa { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleadoSubio))]
    public Empleado? EmpleadoSubio { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdServicioContrato))]
    public ServicioContrato? ServicioContrato { get; set; }

    public override int Id => IdLimpieza;
}

[Table("pesaje")]
public class Pesaje : EntidadBase
{
    [Key]
    [Column("id_pesaje")]
    public int IdPesaje { get; set; }

    [Column("id_servicio_contrato")]
    public int IdServicioContrato { get; set; }

    [Column("id_empleado_subio")]
    public int IdEmpleadoSubio { get; set; }

    [Column("id_tecnico_transporte")]
    public int IdTecnicoTransporte { get; set; }

    [Column("id_precio_lugar")]
    public int IdPrecioLugar { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime? Fecha { get; set; }

    [Range(0, 9999999999.99)]
    [Column("peso_total", TypeName = "decimal(12,2)")]
    public decimal? PesoTotal { get; set; }

    [MaxLength(60)]
    [Column("etapa")]
    public string? Etapa { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdServicioContrato))]
    public ServicioContrato? ServicioContrato { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleadoSubio))]
    public Empleado? EmpleadoSubio { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdTecnicoTransporte))]
    public Empleado? TecnicoTransporte { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdPrecioLugar))]
    public PrecioLugar? PrecioLugar { get; set; }

    public override int Id => IdPesaje;
}

[Table("viaje_extra")]
public class ViajeExtra : EntidadBase
{
    [Key]
    [Column("id_viaje_extra")]
    public int IdViajeExtra { get; set; }

    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [Column("id_administrador")]
    public int IdAdministrador { get; set; }

    [Column("id_camion")]
    public int IdCamion { get; set; }

    [MaxLength(60)]
    [Column("tipo")]
    public string? Tipo { get; set; }

    [Range(0, 9999999999.99)]
    [Column("precio", TypeName = "decimal(12,2)")]
    public decimal? Precio { get; set; }

    [Range(0, 9999999999.99)]
    [Column("pago", TypeName = "decimal(12,2)")]
    public decimal? PagoMonto { get; set; }

    [Range(0, 100000)]
    [Column("cantidad")]
    public int? Cantidad { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdCliente))]
    public Cliente? Cliente { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministrador))]
    public Empleado? Administrador { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdCamion))]
    public Camion? Camion { get; set; }

    public override int Id => IdViajeExtra;
}

[Table("usuario_web")]
public class UsuarioWeb : EntidadBase
{
    [Key]
    [Column("id_usuario_web")]
    public int IdUsuarioWeb { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(12)]
    [Column("dui")]
    public string Dui { get; set; } = string.Empty;

    [MaxLength(25)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    [EmailAddress]
    [MaxLength(254)]
    [Column("email")]
    public string? Email { get; set; }

    public override int Id => IdUsuarioWeb;
}

[Table("cotizacion")]
public class Cotizacion : EntidadBase
{
    [Key]
    [Column("id_cotizacion")]
    public int IdCotizacion { get; set; }

    [Column("id_usuario_web")]
    public int IdUsuarioWeb { get; set; }

    [Column("id_administrador_analista")]
    public int? IdAdministradorAnalista { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("estado")]
    public string Estado { get; set; } = string.Empty;

    [MaxLength(80)]
    [Column("tipo_servicio")]
    public string? TipoServicio { get; set; }

    // SEGURIDAD: este campo lo escribe cualquier visitante del sitio publico,
    // por eso pasa por el sanitizador antes de guardarse.
    [MaxLength(1000)]
    [Column("descripcion")]
    public string? Descripcion { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdUsuarioWeb))]
    public UsuarioWeb? UsuarioWeb { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorAnalista))]
    public Empleado? AdministradorAnalista { get; set; }

    public override int Id => IdCotizacion;
}

[Table("solicitud_puesto_trabajo")]
public class SolicitudPuestoTrabajo : EntidadBase
{
    [Key]
    [Column("id_solicitud")]
    public int IdSolicitud { get; set; }

    [Column("id_usuario_web")]
    public int IdUsuarioWeb { get; set; }

    [Column("id_administrador_analista")]
    public int? IdAdministradorAnalista { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("tipo_puesto")]
    public string TipoPuesto { get; set; } = string.Empty;

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdUsuarioWeb))]
    public UsuarioWeb? UsuarioWeb { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorAnalista))]
    public Empleado? AdministradorAnalista { get; set; }

    public override int Id => IdSolicitud;
}

[Table("observacion_empleado")]
public class ObservacionEmpleado : EntidadBase
{
    [Key]
    [Column("id_observacion")]
    public int IdObservacion { get; set; }

    [Column("id_administrador_autor")]
    public int IdAdministradorAutor { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [MaxLength(60)]
    [Column("tipo")]
    public string? Tipo { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorAutor))]
    public Empleado? AdministradorAutor { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }

    public override int Id => IdObservacion;
}

[Table("viaje_extra_empleado")]
public class ViajeExtraEmpleado
{
    [Column("id_viaje_extra")]
    public int IdViajeExtra { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdViajeExtra))]
    public ViajeExtra? ViajeExtra { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }
}
