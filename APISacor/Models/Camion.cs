using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
