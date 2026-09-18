using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("servicio_contrato")]
public class ServicioContrato : EntidadBase
{
    [Key]
    [Column("id_servicio_contrato")]
    public int IdServicioContrato { get; set; }

    [Column("id_ruta_trabajo")]
    public int IdRutaTrabajo { get; set; }

    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [Column("id_precio_lugar")]
    public int IdPrecioLugar { get; set; }

    [MaxLength(1000)]
    [Column("observacion")]
    public string? Observacion { get; set; }

    [MaxLength(80)]
    [Column("tipo_transportaje")]
    public string? TipoTransportaje { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime? Fecha { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdRutaTrabajo))]
    public RutaTrabajo? RutaTrabajo { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdCliente))]
    public Cliente? Cliente { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdPrecioLugar))]
    public PrecioLugar? PrecioLugar { get; set; }

    public override int Id => IdServicioContrato;
}
