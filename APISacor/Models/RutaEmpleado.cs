using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
