using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APISacor.Models;

[Table("conversacion_whatsapp")]
public class ConversacionWhatsApp
{
    [Key]
    [Column("id_conversacion")]
    public int IdConversacion { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("telefono")]
    public string Telefono { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [Column("paso")]
    public string Paso { get; set; } = "inicio";

    [MaxLength(2000)]
    [Column("datos")]
    public string? Datos { get; set; }

    [MaxLength(128)]
    [Column("ultimo_mensaje_id")]
    public string? UltimoMensajeId { get; set; }

    [Column("actualizado_utc")]
    public DateTime ActualizadoUtc { get; set; }

    [Column("id_cotizacion")]
    public int? IdCotizacion { get; set; }
}
