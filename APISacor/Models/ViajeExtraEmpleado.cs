using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
