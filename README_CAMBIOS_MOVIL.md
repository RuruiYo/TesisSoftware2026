# SACOR — entrega de integración móvil por código

## Qué recibiste

Se partió del ZIP original de `TesisSoftware2026`. Se conservaron la solución `APISacor.sln`, los 19 CRUD, los modelos originales y `db/sacor_db.sql` **sin tocar el esquema original**. Se agregaron archivos nuevos junto a las carpetas existentes y se modificaron únicamente `Program.cs`, `Security/ApiKeyMiddleware.cs`, `Data/SacorDbContext.cs` y la documentación README.

| Ruta | Cambio |
|---|---|
| `APISacor/Models/AccesoMovil.cs` | Entidades para código y sesión con hashes. |
| `APISacor/Services/ServicioAccesoMovil.cs` | Generación aleatoria, hash, validación de token, identificación y mapeo de roles. |
| `APISacor/Controllers/AccesoMovilController.cs` | Activar, perfil, salir y generar código (solo admin). |
| `APISacor/Program.cs` | Registro del servicio, límite de intentos de activación, CORS Authorization y esquema Swagger bearer. |
| `APISacor/Security/ApiKeyMiddleware.cs` | Exclusión exacta `/api/movil` de llave de app: los endpoints de la ruta usan código o Bearer personal. El CRUD previo continúa igual. |
| `APISacor/Data/SacorDbContext.cs` | DbSets y relaciones de las dos tablas nuevas. |
| `db/migraciones/001_acceso_movil.sql` | Migración aditiva (NO DROP de tablas existentes). |
| `db/migraciones/002_primer_administrador.sql` | Emite una sola vez el código inicial de un empleado Administrador existente y activo. Editar ID manualmente. |
| `ejemplos-react-native/servicios/accesoMovil.ts` | Ejemplo listo para copiar a tu Expo; no es parte del backend. |
| `docs/INTEGRACION_MOVIL.md` | Guía de integración y alcance. |

## Orden de instalación en Visual Studio y SSMS

1. **RESPALDA TU BD** antes de aplicar cambios. Compara el esquema real con `db/sacor_db.sql`; esta migración presupone que la tabla `dbo.empleado` existe con `id_empleado`, `estado`, `tipo` y demás columnas del modelo original.
2. Abrir `APISacor.sln` en Visual Studio; verificar SDK .NET 8 y restaurar NuGet. No se agregaron paquetes nuevos.
3. Configurar `APISacor/appsettings.Development.json` COPIANDO el `.ejemplo` y ajustando la conexión a tu SQL Server. No subir ese archivo a Git.
4. En SSMS, abrir `db/migraciones/001_acceso_movil.sql`, revisar el nombre de BD (`USE SACOR;`) y ejecutar el archivo ENTERO. **No ejecutar nuevamente `db/sacor_db.sql` sobre una BD con datos; su inicio elimina tablas.**
5. Configurar API Keys originales en *Administrar secretos de usuario* para el CRUD/Swagger (no las copies a React Native). Estas llaves siguen siendo necesarias para los endpoints antiguos.
6. Ejecutar el backend con perfil `http` en desarrollo. Abrir `http://localhost:5084/health` y `http://localhost:5084/swagger`.
7. Comprobar que existe un empleado con `tipo='Administrador'` y `estado='Activo'`; de no existir, crearlo en Swagger vía el CRUD clásico protegido, introduciendo los datos obligatorios. No usar datos personales reales en una BD de pruebas.
8. Para primer acceso, abrir `db/migraciones/002_primer_administrador.sql`, sustituir `@IdAdministrador = 0` por el ID real obtenido de `SELECT id_empleado, codigo_empleado, tipo, estado FROM dbo.empleado WHERE tipo='Administrador';`, ejecutar y copiar el código mostrado en SSMS. **No guardar el código en el repositorio.**
9. En Swagger, ejecutar `POST /api/movil/activacion` con `{ "codigo": "..." }`: recibirás `token` y perfil. Autorizar como Bearer en Swagger (opción `BearerMovil`), o añadir header `Authorization: Bearer TOKEN` a `GET /api/movil/perfil`. El esquema Swagger original también muestra ApiKey; NO es necesaria para `/api/movil`.
10. Con el token del administrador, `POST /api/movil/administracion/codigos` cuerpo `{ "idEmpleado": 2 }` emite códigos para otros empleados existentes y activos. Entregar de manera segura; no registrar los códigos en logs.
11. El trabajador activa con su código, consulta `GET /api/movil/perfil` y revoca con `POST /api/movil/salir`.

## API nueva

| Método y ruta | Credencial | Acción |
|---|---|---|
| `POST /api/movil/activacion` | Código de 32 dígitos hexadecimales en JSON | Consumo único → token personal + perfil |
| `GET /api/movil/perfil` | `Authorization: Bearer TOKEN` | Perfil mínimo y rol |
| `POST /api/movil/salir` | `Authorization: Bearer TOKEN` | Revoca sesión |
| `POST /api/movil/administracion/codigos` | Bearer de administrador activo | Genera y muestra una sola vez código para empleado existente |

Código de activación: 16 bytes aleatorios, 32 caracteres hexadecimales, vigencia 24 h, solo un uso. Token: 32 bytes aleatorios, 64 hexadecimales, vigencia 30 días. En BD solo se almacena SHA-256; reactivar revoca sesiones anteriores. El estado del empleado se comprueba en cada petición. Rol admitido: `Motorista` → transportista; `Tecnico` → tecnico; `Administrador` → administrador.

## Limitaciones explícitas / seguridad

- **NO se ejecutó `dotnet build` ni SQL Server aquí**: el entorno de preparación no tiene SDK .NET ni SQL Server. Se requiere compilación y pruebas de integración en tu PC antes de afirmar que funciona.
- Las nuevas tablas se agregan con un SQL **independiente**. Ningún script se ejecutó en tu base de datos, y los datos existentes no se editaron.
- Es necesario HTTPS real antes de usarlo fuera de una red local de desarrollo. El perfil `http` no es adecuado para producción: el token es una credencial.
- Los endpoints genéricos antiguos siguen dependiendo de llaves de app: NO incrustes llaves de servidor dentro de una APK ni consumas CRUD genérico desde el móvil. Las operaciones de trabajo/administración requieren endpoints individuales con token y validación de propiedad y rol.
- No se agregó gestión de fotos, flujos completos de horarios, asignación de viajes ni endpoints de CRUD móvil. Tampoco se modificó la app Expo porque su ZIP no fue entregado.
- Revisa esquema real, recuperación de dispositivo, permisos y logs antes de despliegue. Esta entrega es base funcional de backend móvil **pendiente de prueba local**.
