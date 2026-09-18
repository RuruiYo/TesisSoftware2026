using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APISacor.Models;

public abstract class EntidadBase
{
    // Acceso uniforme a la clave primaria para el controlador base generico.
    // [NotMapped] es obligatorio: es una propiedad calculada, no una columna, y sin
    // el atributo EF Core intentaria crear una columna "Id" que no existe en la tabla.
    // Las consultas contra la base NO usan esta propiedad (no se puede traducir a SQL);
    // el controlador base consulta por la clave real con EF.Property.
    [NotMapped]
    public abstract int Id { get; }
}
