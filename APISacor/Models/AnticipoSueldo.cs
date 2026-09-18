using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
