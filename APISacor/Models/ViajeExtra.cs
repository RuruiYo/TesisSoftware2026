using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("viaje_extra")]
public class ViajeExtra : EntidadBase
{
    [Key]
    [Column("id_viaje_extra")]
    public int IdViajeExtra { get; set; }

    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [Column("id_administrador")]
    public int IdAdministrador { get; set; }

    [Column("id_camion")]
    public int IdCamion { get; set; }

    [MaxLength(60)]
    [Column("tipo")]
    public string? Tipo { get; set; }

    [Range(0, 9999999999.99)]
    [Column("precio", TypeName = "decimal(12,2)")]
    public decimal? Precio { get; set; }

    [Range(0, 9999999999.99)]
    [Column("pago", TypeName = "decimal(12,2)")]
    public decimal? PagoMonto { get; set; }

    [Range(0, 100000)]
    [Column("cantidad")]
    public int? Cantidad { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdCliente))]
    public Cliente? Cliente { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministrador))]
    public Empleado? Administrador { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdCamion))]
    public Camion? Camion { get; set; }

    public override int Id => IdViajeExtra;
}
