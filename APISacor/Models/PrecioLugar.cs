using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

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
