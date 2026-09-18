using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

[Table("cliente")]
public class Cliente : EntidadBase
{
    [Key]
    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [MaxLength(250)]
    [Column("direccion")]
    public string? Direccion { get; set; }

    // SEGURIDAD: [EmailAddress] rechaza cadenas que no sean correos y corta
    // intentos de inyeccion de cabeceras si el correo se usa luego para enviar mail.
    [EmailAddress]
    [MaxLength(254)]
    [Column("correo")]
    public string? Correo { get; set; }

    [MaxLength(25)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    [MaxLength(20)]
    [Column("nit")]
    public string? Nit { get; set; }

    [MaxLength(12)]
    [Column("dui")]
    public string? Dui { get; set; }

    [MaxLength(150)]
    [Column("giro")]
    public string? Giro { get; set; }

    [MaxLength(30)]
    [Column("nrc")]
    public string? Nrc { get; set; }

    [MaxLength(150)]
    [Column("nombre_comercial")]
    public string? NombreComercial { get; set; }

    [Column("id_administrador_registro")]
    public int? IdAdministradorRegistro { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(IdAdministradorRegistro))]
    public Empleado? AdministradorRegistro { get; set; }

    public override int Id => IdCliente;
}
