using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
