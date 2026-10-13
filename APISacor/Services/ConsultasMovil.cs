using System.Text.RegularExpressions;

using APISacor.Data;
using APISacor.Models;

using Microsoft.EntityFrameworkCore;

namespace APISacor.Services;

public static class ConsultasMovil
{
    private static readonly Regex PatronFecha = new(
        @"^\s*\[(?<anio>\d{4})-(?<mes>\d{2})(?:-(?<dia>\d{2}))?\]\s*(?<texto>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromMilliseconds(100));

    // La fecha de un viaje vive al inicio del destino: "[2026-10-13] Soyapango".
    public static (DateTime? Fecha, string Texto) LeerDestino(string destino)
    {
        var coincidencia = PatronFecha.Match(destino ?? string.Empty);
        if (!coincidencia.Success)
            return (null, (destino ?? string.Empty).Trim());

        var anio = int.Parse(coincidencia.Groups["anio"].Value);
        var mes = int.Parse(coincidencia.Groups["mes"].Value);
        var dia = coincidencia.Groups["dia"].Success ? int.Parse(coincidencia.Groups["dia"].Value) : 1;
        var texto = coincidencia.Groups["texto"].Value.Trim();

        if (mes is < 1 or > 12 || dia < 1 || dia > DateTime.DaysInMonth(anio, mes))
            return (null, destino!.Trim());

        return (new DateTime(anio, mes, dia), texto);
    }

    public static DateTime HoyLocal() => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;

    // SEGURIDAD: un empleado solo puede registrar algo en un viaje al que esta asignado.
    public static Task<bool> EstaAsignadoAsync(SacorDbContext db, int idRutaTrabajo, int idEmpleado, CancellationToken ct) =>
        db.RutasEmpleado.AsNoTracking().AnyAsync(a => a.IdRutaTrabajo == idRutaTrabajo && a.IdEmpleado == idEmpleado, ct);

    // Contrato de un viaje: el que lo agrupa (viajes de cotizacion) o, en salidas
    // creadas a mano, el contrato que apunta a esa salida.
    public static async Task<int?> ContratoDelViajeAsync(SacorDbContext db, int idRutaTrabajo, CancellationToken ct)
    {
        var propio = await db.RutasTrabajo.AsNoTracking()
            .Where(r => r.IdRutaTrabajo == idRutaTrabajo)
            .Select(r => r.IdServicioContrato)
            .FirstOrDefaultAsync(ct);
        if (propio is not null)
            return propio;

        return await db.ServiciosContrato.AsNoTracking()
            .Where(s => s.IdRutaTrabajo == idRutaTrabajo)
            .OrderBy(s => s.IdServicioContrato)
            .Select(s => (int?)s.IdServicioContrato)
            .FirstOrDefaultAsync(ct);
    }

    // Contratos en los que el empleado tiene al menos un viaje asignado.
    public static IQueryable<ServicioContrato> ServiciosDelEmpleado(SacorDbContext db, int idEmpleado) =>
        from s in db.ServiciosContrato
        where db.RutasEmpleado.Any(a =>
            a.IdEmpleado == idEmpleado &&
            (a.IdRutaTrabajo == s.IdRutaTrabajo ||
             db.RutasTrabajo.Any(r => r.IdRutaTrabajo == a.IdRutaTrabajo && r.IdServicioContrato == s.IdServicioContrato)))
        select s;

    public sealed record ViajeResumen(
        int IdRutaTrabajo,
        string? Fecha,
        string Destino,
        string? Tipo,
        int? IdServicioContrato,
        string? Cliente,
        string? Servicio,
        int? IdCamion,
        string? Camion,
        List<PersonaViaje> Personal,
        int Evidencias);

    public sealed record PersonaViaje(int IdEmpleado, string Nombre, string Tipo);

    // Arma la informacion completa de varios viajes con pocas consultas.
    public static async Task<List<ViajeResumen>> ResumirViajesAsync(SacorDbContext db, List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return new List<ViajeResumen>();

        var rutas = await db.RutasTrabajo.AsNoTracking().Where(r => ids.Contains(r.IdRutaTrabajo)).ToListAsync(ct);

        var idsContratoPropio = rutas.Where(r => r.IdServicioContrato != null).Select(r => r.IdServicioContrato!.Value).ToList();
        var contratos = await (
            from s in db.ServiciosContrato.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on s.IdCliente equals c.IdCliente
            where idsContratoPropio.Contains(s.IdServicioContrato) || ids.Contains(s.IdRutaTrabajo)
            select new { s.IdServicioContrato, s.IdRutaTrabajo, s.TipoTransportaje, Cliente = c.Nombre }
        ).ToListAsync(ct);

        var idsCamion = rutas.Where(r => r.IdCamion != null).Select(r => r.IdCamion!.Value).Distinct().ToList();
        var camiones = await db.Camiones.AsNoTracking()
            .Where(c => idsCamion.Contains(c.IdCamion))
            .Select(c => new { c.IdCamion, c.Placa, c.Nombre })
            .ToListAsync(ct);

        var personal = await (
            from a in db.RutasEmpleado.AsNoTracking()
            join e in db.Empleados.AsNoTracking() on a.IdEmpleado equals e.IdEmpleado
            where ids.Contains(a.IdRutaTrabajo)
            select new { a.IdRutaTrabajo, e.IdEmpleado, e.Nombre, e.Tipo }
        ).ToListAsync(ct);

        var evidencias = await db.Limpiezas.AsNoTracking()
            .Where(l => l.IdRutaTrabajo != null && ids.Contains(l.IdRutaTrabajo.Value))
            .GroupBy(l => l.IdRutaTrabajo!.Value)
            .Select(g => new { IdRutaTrabajo = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);

        return rutas.Select(r =>
        {
            var (fecha, texto) = LeerDestino(r.Destino);
            var contrato = r.IdServicioContrato is not null
                ? contratos.FirstOrDefault(s => s.IdServicioContrato == r.IdServicioContrato)
                : contratos.FirstOrDefault(s => s.IdRutaTrabajo == r.IdRutaTrabajo);
            var camion = camiones.FirstOrDefault(c => c.IdCamion == r.IdCamion);

            return new ViajeResumen(
                r.IdRutaTrabajo,
                fecha?.ToString("yyyy-MM-dd"),
                texto,
                r.Tipo,
                contrato?.IdServicioContrato,
                contrato?.Cliente,
                contrato?.TipoTransportaje,
                r.IdCamion,
                camion is null ? null : (camion.Nombre is null ? camion.Placa : $"{camion.Nombre} ({camion.Placa})"),
                personal.Where(p => p.IdRutaTrabajo == r.IdRutaTrabajo)
                        .Select(p => new PersonaViaje(p.IdEmpleado, p.Nombre, p.Tipo))
                        .ToList(),
                evidencias.FirstOrDefault(e => e.IdRutaTrabajo == r.IdRutaTrabajo)?.Cantidad ?? 0);
        })
        .OrderBy(v => v.Fecha ?? "9999-99-99")
        .ThenBy(v => v.IdRutaTrabajo)
        .ToList();
    }
}
