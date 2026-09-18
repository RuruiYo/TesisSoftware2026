using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("cotizacion")]
public class Cotizacion : EntidadBase
{
    [Key]
    [Column("id_cotizacion")]
    public int IdCotizacion { get; set; }

    [Column("id_usuario_web")]
    public int IdUsuarioWeb { get; set; }

    [Column("id_administrador_analista")]
    public int? IdAdministradorAnalista { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("estado")]
    public string Estado { get; set; } = string.Empty;

    [MaxLength(80)]
    [Column("tipo_servicio")]
    public string? TipoServicio { get; set; }

    // SEGURIDAD: este campo lo escribe cualquier visitante del sitio publico,
    // por eso pasa por el sanitizador antes de guardarse.
    [MaxLength(1000)]
    [Column("descripcion")]
    public string? Descripcion { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdUsuarioWeb))]
    public UsuarioWeb? UsuarioWeb { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorAnalista))]
    public Empleado? AdministradorAnalista { get; set; }

    public override int Id => IdCotizacion;
}
