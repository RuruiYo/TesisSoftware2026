namespace APISacor.Services;

// Los cuatro tipos de usuario de SACOR y la app que usa cada uno.
public static class TiposEmpleado
{
    public const string AdministradorOperativo = "AdministradorOperativo";
    public const string AdministradorTecnico = "AdministradorTecnico";
    public const string Tecnico = "Tecnico";
    public const string Motorista = "Motorista";

    public static readonly string[] Vigentes =
        { AdministradorOperativo, AdministradorTecnico, Tecnico, Motorista };

    // Tipos de antes de la reorganizacion. Solo se conservan para no perder el
    // historial de esos empleados; nunca pueden quedar activos.
    public static readonly string[] Anteriores = { "Ayudante", "Mecanico", "Coordinador" };

    public static bool EsAdministrador(string tipo) =>
        tipo.Equals(AdministradorOperativo, StringComparison.OrdinalIgnoreCase) ||
        tipo.Equals(AdministradorTecnico, StringComparison.OrdinalIgnoreCase);

    public static string? Normalizar(string? tipo) =>
        Vigentes.Concat(Anteriores).FirstOrDefault(t => t.Equals(tipo?.Trim(), StringComparison.OrdinalIgnoreCase));

    // SEGURIDAD: valida tipo y estado juntos. Un tipo anterior solo se acepta
    // inactivo o suspendido, asi nadie reactiva un rol que ya no existe.
    public static string? Validar(string tipo, string estado)
    {
        var normalizado = Normalizar(tipo);
        if (normalizado is null)
            return $"El tipo debe ser uno de: {string.Join(", ", Vigentes)}.";

        if (Anteriores.Contains(normalizado) && estado.Equals("Activo", StringComparison.OrdinalIgnoreCase))
            return $"El tipo {normalizado} ya no existe. Cambie el tipo a uno de: {string.Join(", ", Vigentes)}, o deje al empleado inactivo.";

        return null;
    }
}
