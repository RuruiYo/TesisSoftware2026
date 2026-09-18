using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("ruta_trabajo")]
public class RutaTrabajo : EntidadBase
{
    [Key]
    [Column("id_ruta_trabajo")]
    public int IdRutaTrabajo { get; set; }

    [Column("id_administrador")]
    public int IdAdministrador { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("destino")]
    public string Destino { get; set; } = string.Empty;

    [Range(0, 9999999999.99)]
    [Column("dinero_gasolina", TypeName = "decimal(12,2)")]
    public decimal? DineroGasolina { get; set; }

    [MaxLength(60)]
    [Column("tipo")]
    public string? Tipo { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministrador))]
    public Empleado? Administrador { get; set; }

    public override int Id => IdRutaTrabajo;
}
