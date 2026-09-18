using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("observacion_empleado")]
public class ObservacionEmpleado : EntidadBase
{
    [Key]
    [Column("id_observacion")]
    public int IdObservacion { get; set; }

    [Column("id_administrador_autor")]
    public int IdAdministradorAutor { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime Fecha { get; set; }

    [MaxLength(60)]
    [Column("tipo")]
    public string? Tipo { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorAutor))]
    public Empleado? AdministradorAutor { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }

    public override int Id => IdObservacion;
}
