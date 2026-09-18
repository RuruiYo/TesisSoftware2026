using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("solicitud_puesto_trabajo")]
public class SolicitudPuestoTrabajo : EntidadBase
{
    [Key]
    [Column("id_solicitud")]
    public int IdSolicitud { get; set; }

    [Column("id_usuario_web")]
    public int IdUsuarioWeb { get; set; }

    [Column("id_administrador_analista")]
    public int? IdAdministradorAnalista { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("tipo_puesto")]
    public string TipoPuesto { get; set; } = string.Empty;

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdUsuarioWeb))]
    public UsuarioWeb? UsuarioWeb { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorAnalista))]
    public Empleado? AdministradorAnalista { get; set; }

    public override int Id => IdSolicitud;
}
