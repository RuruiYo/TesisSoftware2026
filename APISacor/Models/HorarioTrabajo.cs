using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
    public TimeSpan? Salida { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleadoMarco))]
    public Empleado? EmpleadoMarco { get; set; }

    public override int Id => IdHorarioTrabajo;
}
