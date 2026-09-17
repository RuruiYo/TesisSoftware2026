using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using APISacor.Data;
using APISacor.Models;
using APISacor.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Controllers;

// SEGURIDAD: controlador base generico. Toda la logica de validacion, sanitizacion,
// paginacion y manejo de errores vive en UN solo lugar. Si estuviera copiada en los
// 20 controladores, tarde o temprano uno se quedaria sin una de las protecciones:
// centralizarla es lo que garantiza que las 20 tablas esten cubiertas igual.
[ApiController]
[Produces("application/json")]
public abstract class BaseCrudController<TEntidad> : ControllerBase
    where TEntidad : EntidadBase, new()
{
    protected readonly SacorDbContext Db;
    protected readonly ILogger Logger;

    // Cache de las propiedades string con [MaxLength] y del nombre de la clave
    // primaria. Se calcula una sola vez por tipo: sin el cache, hacer reflexion
    // en cada peticion seria un cuello de botella aprovechable como DoS.
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Prop, int Max, bool Nullable)[]> CacheTexto = new();
    private static readonly ConcurrentDictionary<Type, string> CacheNombreClave = new();

    protected BaseCrudController(SacorDbContext db, ILogger logger)
    {
        Db = db;
        Logger = logger;
    }

    protected DbSet<TEntidad> Conjunto => Db.Set<TEntidad>();

    // Punto de extension para reglas propias de cada entidad (validar DUI, etc.).
    // Devuelve null si todo esta bien, o el mensaje de error.
    protected virtual string? ValidarReglasPropias(TEntidad entidad) => null;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> Listar(
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken cancellationToken)
    {
        // SEGURIDAD: la paginacion es obligatoria y con tope duro. Un GET sin limite
        // sobre una tabla grande es la forma mas facil de tumbar el API y la base.
        var paginaSegura = InputSanitizer.NormalizarPagina(pagina);
        var tamanoSeguro = InputSanitizer.NormalizarTamanoPagina(tamano);
        var nombreClave = ObtenerNombreClave();

        // SEGURIDAD: AsNoTracking no carga el change tracker de EF, lo que reduce
        // la memoria por peticion de forma importante en listados.
        var consulta = Conjunto.AsNoTracking();

        var total = await consulta.CountAsync(cancellationToken);

        // El OrderBy es obligatorio: SQL Server exige ORDER BY para poder usar
        // OFFSET/FETCH, y sin orden fijo la paginacion devolveria filas repetidas.
        var datos = await consulta
            .OrderBy(e => EF.Property<int>(e, nombreClave))
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            pagina = paginaSegura,
            tamano = tamanoSeguro,
            total,
            datos
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Obtener(int id, CancellationToken cancellationToken)
    {
        // SEGURIDAD: la restriccion de ruta {id:int} rechaza cualquier cosa que no sea
        // un entero ANTES de entrar al metodo. Nunca llega texto a la consulta.
        if (id <= 0)
            return BadRequest(new { estado = 400, mensaje = "El id debe ser mayor que cero." });

        var nombreClave = ObtenerNombreClave();

        // SEGURIDAD: EF.Property arma la comparacion contra la columna real de la
        // clave primaria y EF la traduce a una consulta PARAMETRIZADA. Nunca se
        // concatena el id dentro de una cadena SQL, por eso no hay inyeccion posible.
        var entidad = await Conjunto.AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<int>(e, nombreClave) == id, cancellationToken);

        return entidad is null
            ? NotFound(new { estado = 404, mensaje = "El registro no existe." })
            : Ok(entidad);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public virtual async Task<IActionResult> Crear(
        [FromBody] TEntidad entidad,
        CancellationToken cancellationToken)
    {
        // SEGURIDAD: [ApiController] ya valida las anotaciones del modelo y responde
        // 400 automaticamente, pero se revisa de nuevo por si alguien desactiva
        // esa convencion en el futuro.
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // SEGURIDAD: se limpia todo el texto libre antes de que toque la base.
        SanitizarTexto(entidad);

        var error = ValidarReglasPropias(entidad);
        if (error is not null)
            return BadRequest(new { estado = 400, mensaje = error });

        // SEGURIDAD: se fuerza la clave primaria a 0 para que el cliente no pueda
        // elegir el id ni sobreescribir un registro ajeno enviandolo en el POST
        // (ataque de over-posting sobre la llave).
        ObtenerPropiedadClave().SetValue(entidad, 0);

        Conjunto.Add(entidad);
        await Db.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Alta de {Entidad} id {Id} por app {App}",
            typeof(TEntidad).Name, entidad.Id, HttpContext.Items["AppConsumidora"]);

        return CreatedAtAction(nameof(Obtener), new { id = entidad.Id }, entidad);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Actualizar(
        int id,
        [FromBody] TEntidad entidad,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest(new { estado = 400, mensaje = "El id debe ser mayor que cero." });

        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // FindAsync busca por la clave primaria real de la entidad.
        var existente = await Conjunto.FindAsync(new object?[] { id }, cancellationToken);
        if (existente is null)
            return NotFound(new { estado = 404, mensaje = "El registro no existe." });

        SanitizarTexto(entidad);

        var error = ValidarReglasPropias(entidad);
        if (error is not null)
            return BadRequest(new { estado = 400, mensaje = error });

        // SEGURIDAD: el id valido es SIEMPRE el de la ruta, nunca el del cuerpo.
        // Asi se evita que un PUT a /empleados/5 modifique al empleado 9.
        ObtenerPropiedadClave().SetValue(entidad, id);

        // SEGURIDAD: SetValues copia unicamente las propiedades escalares mapeadas.
        // Las de navegacion quedan fuera, asi que un cuerpo con objetos anidados
        // no puede crear ni modificar registros de otras tablas.
        Db.Entry(existente).CurrentValues.SetValues(entidad);

        await Db.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Modificacion de {Entidad} id {Id} por app {App}",
            typeof(TEntidad).Name, id, HttpContext.Items["AppConsumidora"]);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public virtual async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest(new { estado = 400, mensaje = "El id debe ser mayor que cero." });

        var entidad = await Conjunto.FindAsync(new object?[] { id }, cancellationToken);
        if (entidad is null)
            return NotFound(new { estado = 404, mensaje = "El registro no existe." });

        Conjunto.Remove(entidad);

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // SEGURIDAD: con DeleteBehavior.Restrict el motor rechaza el borrado si
            // hay registros hijos. Se responde 409 en lugar de dejar que la cascada
            // elimine nomina o historial de pesajes de forma silenciosa.
            // Se limpia el estado del contexto para no dejarlo inconsistente.
            Db.Entry(entidad).State = EntityState.Unchanged;

            return Conflict(new
            {
                estado = 409,
                mensaje = "No se puede eliminar: el registro tiene datos relacionados."
            });
        }

        Logger.LogWarning(
            "Baja de {Entidad} id {Id} por app {App}",
            typeof(TEntidad).Name, id, HttpContext.Items["AppConsumidora"]);

        return NoContent();
    }

    // SEGURIDAD: recorre por reflexion todas las propiedades string con [MaxLength]
    // y les aplica el sanitizador. Al ser generico, ninguna tabla se queda afuera
    // aunque despues se agreguen columnas nuevas.
    private static void SanitizarTexto(TEntidad entidad)
    {
        var propiedades = CacheTexto.GetOrAdd(typeof(TEntidad), tipo =>
        {
            var contextoNulabilidad = new NullabilityInfoContext();

            return tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string) && p.CanWrite && p.CanRead)
                .Select(p => (
                    Prop: p,
                    Max: p.GetCustomAttribute<MaxLengthAttribute>()?.Length ?? 0,
                    Nullable: contextoNulabilidad.Create(p).WriteState == NullabilityState.Nullable))
                .Where(x => x.Max > 0)
                .ToArray();
        });

        foreach (var (prop, max, esNullable) in propiedades)
        {
            var valor = prop.GetValue(entidad) as string;
            var limpio = InputSanitizer.Limpiar(valor, max);

            // Si la propiedad no acepta null se deja cadena vacia para que
            // [Required] sea quien produzca el 400, no una excepcion.
            prop.SetValue(entidad, limpio ?? (esNullable ? null : string.Empty));
        }
    }

    private string ObtenerNombreClave() =>
        CacheNombreClave.GetOrAdd(typeof(TEntidad), tipo =>
            Db.Model.FindEntityType(tipo)!.FindPrimaryKey()!.Properties[0].Name);

    private PropertyInfo ObtenerPropiedadClave() =>
        typeof(TEntidad).GetProperty(ObtenerNombreClave())!;
}
