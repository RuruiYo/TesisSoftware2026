using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APISacor.Models;

// Foto adjunta a un registro de limpieza (evidencia de limpieza, de ruta o de recoleccion).
// SEGURIDAD: no hereda de EntidadBase ni tiene controlador CRUD generico, asi que las
// imagenes solo se leen por los endpoints moviles que validan al dueno de la evidencia.
[Table("evidencia_foto")]
public class EvidenciaFoto
{
    [Key, Column("id_evidencia_foto")]
    public int IdEvidenciaFoto { get; set; }

    [Column("id_limpieza")]
    public int IdLimpieza { get; set; }

    [Required, StringLength(30), Column("tipo_contenido")]
    public string TipoContenido { get; set; } = string.Empty;

    [Required, Column("contenido", TypeName = "varbinary(max)")]
    public byte[] Contenido { get; set; } = Array.Empty<byte>();

    [Column("tamano_bytes")]
    public int TamanoBytes { get; set; }

    [Column("creado_utc", TypeName = "datetime2(0)")]
    public DateTime CreadoUtc { get; set; }
}
