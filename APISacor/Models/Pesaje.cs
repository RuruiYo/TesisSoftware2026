using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("pesaje")]
public class Pesaje : EntidadBase
{
    [Key]
    [Column("id_pesaje")]
    public int IdPesaje { get; set; }

    [Column("id_servicio_contrato")]
    public int IdServicioContrato { get; set; }

    [Column("id_empleado_subio")]
    public int IdEmpleadoSubio { get; set; }

    [Column("id_tecnico_transporte")]
    public int IdTecnicoTransporte { get; set; }

    [Column("id_precio_lugar")]
    public int IdPrecioLugar { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateTime? Fecha { get; set; }

    [Range(0, 9999999999.99)]
    [Column("peso_total", TypeName = "decimal(12,2)")]
    public decimal? PesoTotal { get; set; }

    [MaxLength(60)]
    [Column("etapa")]
    public string? Etapa { get; set; }

    // Viaje en el que se movio esta carga (registrada por el tecnico desde el movil).
    [Column("id_ruta_trabajo")]
    public int? IdRutaTrabajo { get; set; }

    // Precio por tonelada vigente cuando se registro el pesaje. Se congela aqui para
    // que cambiar el precio despues no altere ventas que ya ocurrieron.
    [Range(0, 9999999999.99)]
    [Column("precio_tonelada", TypeName = "decimal(12,2)")]
    public decimal? PrecioTonelada { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdServicioContrato))]
    public ServicioContrato? ServicioContrato { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdEmpleadoSubio))]
    public Empleado? EmpleadoSubio { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdTecnicoTransporte))]
    public Empleado? TecnicoTransporte { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdPrecioLugar))]
    public PrecioLugar? PrecioLugar { get; set; }

    public override int Id => IdPesaje;
}
