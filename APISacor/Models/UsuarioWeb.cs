using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("usuario_web")]
public class UsuarioWeb : EntidadBase
{
    [Key]
    [Column("id_usuario_web")]
    public int IdUsuarioWeb { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(12)]
    [Column("dui")]
    public string Dui { get; set; } = string.Empty;

    [MaxLength(25)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    [EmailAddress]
    [MaxLength(254)]
    [Column("email")]
    public string? Email { get; set; }

    public override int Id => IdUsuarioWeb;
}
