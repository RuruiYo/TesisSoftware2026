using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

// SEGURIDAD: registro de auditoria. Guarda quien cambio que y cuando.
// Es la unica tabla del sistema que se escribe pero NUNCA se edita ni se borra:
// un historial que se puede modificar no sirve como evidencia de nada.
// Por eso su controlador solo expone GET y POST.
[Table("auditoria")]
public class Auditoria : EntidadBase
{
    [Key]
    [Column("id_auditoria")]
    public int IdAuditoria { get; set; }

    // Nulable porque una accion puede venir del sitio publico, donde no hay
    // empleado identificado.
    [Column("id_empleado")]
    public int? IdEmpleado { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("tabla_afectada")]
    public string TablaAfectada { get; set; } = string.Empty;

    // Texto y no entero: hay tablas con clave compuesta, como viaje_extra_empleado,
    // donde el identificador del registro son dos valores.
    [Required]
    [MaxLength(100)]
    [Column("id_registro")]
    public string IdRegistro { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    [Column("accion")]
    public string Accion { get; set; } = string.Empty;

    [Column("fecha_hora", TypeName = "datetime2(0)")]
    public DateTime FechaHora { get; set; }

    [Column("valor_anterior")]
    public string? ValorAnterior { get; set; }

    [Column("valor_nuevo")]
    public string? ValorNuevo { get; set; }

    [MaxLength(1000)]
    [Column("detalle")]
    public string? Detalle { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleado))]
    public Empleado? Empleado { get; set; }

    public override int Id => IdAuditoria;
}
