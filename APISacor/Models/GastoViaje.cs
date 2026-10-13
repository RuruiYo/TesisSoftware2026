using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

// Gasto hecho durante un viaje (combustible, peaje, comida...). Lo registran
// motoristas y tecnicos desde el movil y la app de escritorio lo suma a los gastos.
[Table("gasto_viaje")]
public class GastoViaje : EntidadBase
{
    [Key]
    [Column("id_gasto_viaje")]
    public int IdGastoViaje { get; set; }

    [Column("id_ruta_trabajo")]
    public int IdRutaTrabajo { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [Required]
    [MaxLength(40)]
    [Column("concepto")]
    public string Concepto { get; set; } = string.Empty;

    [Range(0.01, 100000)]
    [Column("monto", TypeName = "decimal(12,2)")]
    public decimal Monto { get; set; }

    [MaxLength(250)]
    [Column("descripcion")]
    public string? Descripcion { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdRutaTrabajo))]
    public RutaTrabajo? RutaTrabajo { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }

    public override int Id => IdGastoViaje;
}
