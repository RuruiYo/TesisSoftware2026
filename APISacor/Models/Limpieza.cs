using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
