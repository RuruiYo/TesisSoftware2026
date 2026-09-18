using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APISacor.Models;

// Tablas nuevas, independientes del CRUD legado. Solo contienen hashes, nunca codigos ni tokens claros.
[Table("codigo_activacion_movil")]
public class CodigoActivacionMovil
{
    [Key, Column("id_codigo_activacion")]
    public int IdCodigoActivacion { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Required, StringLength(64), Column("codigo_hash", TypeName = "char(64)")]
    public string CodigoHash { get; set; } = string.Empty;

    [Column("id_administrador_generador")]
    public int? IdAdministradorGenerador { get; set; }

    [Column("creado_utc", TypeName = "datetime2(0)")]
    public DateTime CreadoUtc { get; set; }

    [Column("expira_utc", TypeName = "datetime2(0)")]
    public DateTime ExpiraUtc { get; set; }

    [Column("usado_utc", TypeName = "datetime2(0)")]
    public DateTime? UsadoUtc { get; set; }

    [Column("revocado_utc", TypeName = "datetime2(0)")]
    public DateTime? RevocadoUtc { get; set; }
}

[Table("sesion_movil")]
public class SesionMovil
{
    [Key, Column("id_sesion_movil")]
    public int IdSesionMovil { get; set; }

    [Column("id_empleado")]
    public int IdEmpleado { get; set; }

    [Required, StringLength(64), Column("token_hash", TypeName = "char(64)")]
    public string TokenHash { get; set; } = string.Empty;

    [Column("creado_utc", TypeName = "datetime2(0)")]
    public DateTime CreadoUtc { get; set; }

    [Column("expira_utc", TypeName = "datetime2(0)")]
    public DateTime ExpiraUtc { get; set; }

    [Column("revocado_utc", TypeName = "datetime2(0)")]
    public DateTime? RevocadoUtc { get; set; }
}
