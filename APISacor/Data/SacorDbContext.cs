using APISacor.Models;
using Microsoft.EntityFrameworkCore;

namespace APISacor.Data;

public class SacorDbContext : DbContext
{
    public SacorDbContext(DbContextOptions<SacorDbContext> options) : base(options) { }

    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<RutaTrabajo> RutasTrabajo => Set<RutaTrabajo>();
    public DbSet<RutaEmpleado> RutasEmpleado => Set<RutaEmpleado>();
    public DbSet<Camion> Camiones => Set<Camion>();
    public DbSet<CamionConductor> CamionConductores => Set<CamionConductor>();
    public DbSet<HorarioTrabajo> HorariosTrabajo => Set<HorarioTrabajo>();
    public DbSet<ServicioExterno> ServiciosExternos => Set<ServicioExterno>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<AnticipoSueldo> AnticiposSueldo => Set<AnticipoSueldo>();
    public DbSet<PrecioLugar> PreciosLugar => Set<PrecioLugar>();
    public DbSet<ServicioContrato> ServiciosContrato => Set<ServicioContrato>();
    public DbSet<Limpieza> Limpiezas => Set<Limpieza>();
    public DbSet<Pesaje> Pesajes => Set<Pesaje>();
    public DbSet<ViajeExtra> ViajesExtra => Set<ViajeExtra>();
    public DbSet<UsuarioWeb> UsuariosWeb => Set<UsuarioWeb>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<SolicitudPuestoTrabajo> SolicitudesPuestoTrabajo => Set<SolicitudPuestoTrabajo>();
    public DbSet<ObservacionEmpleado> ObservacionesEmpleado => Set<ObservacionEmpleado>();
    public DbSet<ViajeExtraEmpleado> ViajeExtraEmpleados => Set<ViajeExtraEmpleado>();
    public DbSet<CodigoActivacionMovil> CodigosActivacionMovil => Set<CodigoActivacionMovil>();
    public DbSet<SesionMovil> SesionesMoviles => Set<SesionMovil>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Clave primaria compuesta de la tabla puente.
        modelBuilder.Entity<ViajeExtraEmpleado>()
            .HasKey(v => new { v.IdViajeExtra, v.IdEmpleado });

        // Se agregan sin alterar las entidades/columnas ya existentes.
        modelBuilder.Entity<CodigoActivacionMovil>()
            .HasIndex(c => c.CodigoHash).IsUnique();
        modelBuilder.Entity<SesionMovil>()
            .HasIndex(s => s.TokenHash).IsUnique();
        modelBuilder.Entity<CodigoActivacionMovil>()
            .HasOne<Empleado>().WithMany().HasForeignKey(c => c.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CodigoActivacionMovil>()
            .HasOne<Empleado>().WithMany().HasForeignKey(c => c.IdAdministradorGenerador)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SesionMovil>()
            .HasOne<Empleado>().WithMany().HasForeignKey(s => s.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);

        // La propiedad calculada Id de EntidadBase existe solo para el controlador
        // generico. Se quita del modelo de forma explicita para que EF Core no
        // intente buscar una columna "Id" que no existe en ninguna tabla.
        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            if (tipo.FindProperty("Id") is not null)
                tipo.RemoveProperty("Id");
        }

        // SEGURIDAD: indices unicos replicados desde la base de datos. Aunque SQL Server
        // ya los tiene, declararlos aqui permite que EF Core devuelva un error limpio
        // en lugar de una excepcion cruda del motor que podria filtrar detalles internos.
        modelBuilder.Entity<Empleado>().HasIndex(e => e.CodigoEmpleado).IsUnique();
        modelBuilder.Entity<Empleado>().HasIndex(e => e.Dui).IsUnique();
        modelBuilder.Entity<Camion>().HasIndex(c => c.Placa).IsUnique();
        modelBuilder.Entity<UsuarioWeb>().HasIndex(u => u.Dui).IsUnique();
        modelBuilder.Entity<PrecioLugar>().HasIndex(p => new { p.Tipo, p.Lugar }).IsUnique();

        // SEGURIDAD: se desactiva el borrado en cascada en TODAS las relaciones.
        // Con las multiples llaves foraneas que apuntan a empleado, una cascada
        // permitiria que un solo DELETE arrastre nomina, pesajes y contratos.
        // Con Restrict el borrado falla y devolvemos 409 Conflict en lugar de
        // destruir datos historicos de forma silenciosa.
        foreach (var relacion in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetForeignKeys()))
        {
            relacion.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
