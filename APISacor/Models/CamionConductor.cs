using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
