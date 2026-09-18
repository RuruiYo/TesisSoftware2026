using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("servicio_externo")]
public class ServicioExterno : EntidadBase
{
    [Key]
    [Column("id_servicio_ext")]
    public int IdServicioExt { get; set; }

    [Column("id_administrador")]
    public int IdAdministrador { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 9999999999.99)]
    [Column("cantidad_pago", TypeName = "decimal(12,2)")]
    public decimal CantidadPago { get; set; }

    [MaxLength(250)]
    [Column("direccion")]
    public string? Direccion { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministrador))]
    public Empleado? Administrador { get; set; }

    public override int Id => IdServicioExt;
}
