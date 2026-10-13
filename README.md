# API SACOR

API REST en .NET 8 con Entity Framework Core y SQL Server. Da servicio a tres aplicaciones:

| App | Plataforma | Estado |
|---|---|---|
| Sitio web público | React + Vite | En desarrollo |
| Módulo administrador | JavaFX | Por hacer |
| Módulo trabajador | React Native / Expo | Integración de activación disponible; otras vistas pendientes |

---

## 1. Requisitos

- .NET 8 SDK
- SQL Server (Express sirve) + SSMS
- Visual Studio 2022 o VS Code

## 2. Puesta en marcha

### 2.1 Crear la base de datos

Abrí `db/sacor_completo.sql` en SSMS y ejecutalo completo (F5). Crea la base `sacor` con sus 25 tablas, 13 procedimientos almacenados y un usuario de cada tipo para probar (códigos `710245`, `820376`, `3104`, `4215`). **Borra la base si ya existe**: no correrlo sobre una base con datos reales.

> El script arranca borrando las tablas si ya existen. No lo vuelvas a correr cuando tengas datos que quieras conservar.

### 2.2 Configurar la cadena de conexión

Copiá `APISacor/appsettings.Development.json.ejemplo` y renombralo a `appsettings.Development.json`. Cambiá `NOMBRE_DE_TU_SERVIDOR` por tu instancia de SQL Server (aparece en la barra de título de SSMS al conectarte).

Ese archivo está en `.gitignore`: cada quien tiene el suyo y no se sube al repo.

### 2.3 Configurar las API Keys

Las llaves no van en ningún archivo del repositorio. Desde la carpeta `APISacor`:

```bash
dotnet user-secrets init
dotnet user-secrets set "Seguridad:ApiKeys:AppWeb" "PEGA_AQUI_UNA_LLAVE"
dotnet user-secrets set "Seguridad:ApiKeys:AppEscritorio" "PEGA_AQUI_OTRA"
dotnet user-secrets set "Seguridad:ApiKeys:AppMovil" "PEGA_AQUI_OTRA"
```

Para generar cada llave, en PowerShell:

```powershell
$b = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
-join ($b | ForEach-Object { $_.ToString('x2') })
```

Cada app usa la suya. Si se filtra una, se revoca solo esa.

### 2.4 Correr

```bash
cd APISacor
dotnet run
```

| URL | Uso |
|---|---|
| `https://localhost:7210/swagger` | Documentación interactiva |
| `https://localhost:7210` | Para el navegador (web) |
| `http://localhost:5084` | Para JavaFX y Android, sin líos de certificado |

---

## 3. Autenticación

**Todas** las rutas `/api/...` exigen el header:

```
X-Api-Key: tu-llave
```

Sin header → `401`. Llave incorrecta → `403`.

Quedan libres `/swagger` y `/health`.

En Swagger usá el botón **Authorize** de arriba a la derecha para pegar la llave.

---

## 4. Endpoints

### 4.1 CRUD genérico

Las 19 entidades de clave simple exponen el mismo juego de rutas:

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/api/{recurso}?pagina=1&tamano=50` | `200` con `{ pagina, tamano, total, datos[] }` |
| GET | `/api/{recurso}/{id}` | `200` o `404` |
| POST | `/api/{recurso}` | `201` con el registro creado |
| PUT | `/api/{recurso}/{id}` | `204`, `400` o `404` |
| DELETE | `/api/{recurso}/{id}` | `204`, `404` o `409` |

Recursos disponibles:

```
empleados              clientes              rutas-trabajo
rutas-empleado         camiones              camion-conductores
horarios-trabajo       servicios-externos    pagos
anticipos-sueldo       precios-lugar         servicios-contrato
limpiezas              pesajes               viajes-extra
usuarios-web           cotizaciones          solicitudes-puesto
observaciones-empleado
```

### 4.1.1 Auditoría

`/api/auditoria` es de **solo escritura y lectura**: tiene `GET` y `POST`, pero **no** `PUT` ni `DELETE`. Un historial que se puede editar o borrar no sirve como evidencia.

```
GET  /api/auditoria?tabla=pesaje&idEmpleado=5&desde=2026-09-01&hasta=2026-09-30
GET  /api/auditoria/{id}
POST /api/auditoria
```

```json
{
  "idEmpleado": 5,
  "tablaAfectada": "pesaje",
  "idRegistro": "42",
  "accion": "UPDATE",
  "valorAnterior": "{\"pesoTotal\":12000}",
  "valorNuevo": "{\"pesoTotal\":12500}",
  "detalle": "Correccion por error de digitacion"
}
```

`accion` ∈ `INSERT`, `UPDATE`, `DELETE`. La `fechaHora` la pone el servidor: si viniera del cliente, se podrían fabricar registros con fecha falsa. `idEmpleado` puede ir nulo cuando la acción viene del sitio público.

### 4.1.2 Tabla puente

La tabla puente tiene rutas propias por su clave compuesta:

```
GET    /api/viaje-extra-empleados?idViajeExtra=1
GET    /api/viaje-extra-empleados/{idViajeExtra}/{idEmpleado}
POST   /api/viaje-extra-empleados
DELETE /api/viaje-extra-empleados/{idViajeExtra}/{idEmpleado}
```

### 4.2 Reglas que aplica el servidor

- **Paginación obligatoria.** Máximo 200 registros por página; si pedís más, se recorta.
- **Rate limit** de 120 peticiones por minuto por llave. Al pasarte, `429`.
- **Cuerpo máximo** de 1 MB.
- **`DELETE` devuelve `409`** si el registro tiene hijos. No hay borrado en cascada: eso protege el historial de pesajes y de nómina.
- El **id del cuerpo se ignora** en POST y PUT; manda el de la ruta.
- Los campos de texto se **sanitizan** (se quitan etiquetas HTML y caracteres de control) y se recortan al largo de la columna.
- `empleado.token` **nunca** se devuelve ni se acepta.

### 4.3 Validaciones por entidad

| Entidad | Regla |
|---|---|
| `empleados` | DUI `00000000-0`; `estado` ∈ Activo, Inactivo, Suspendido; `tipo` ∈ Administrador, Motorista, Tecnico, Ayudante, Mecanico, Coordinador |
| `clientes` | `tipo` ∈ Natural, Juridico. Natural exige DUI, Jurídico exige NIT |
| `camiones` | La placa se normaliza a mayúsculas sin espacios |
| `horarios-trabajo` | `salida` puede ir nula (turno en curso). Si viene, debe ser posterior a la entrada. No se aceptan fechas futuras |
| `pagos` | El descuento no puede superar la cantidad |
| `pesajes` | Peso entre 0 y 100000 |
| `cotizaciones` | `estado` ∈ Pendiente, EnRevision, Cotizada, Aprobada, Rechazada, Cerrada |
| `observaciones-empleado` | El autor no puede ser el mismo empleado observado |

### 4.4 Endpoints del sitio público

Solo permiten dar de alta. No leen ni borran.

```http
POST /api/publico/cotizaciones
{
  "nombre": "Steven Ayala",
  "dui": "12345678-9",
  "telefono": "+503 7000 0000",
  "email": "correo@ejemplo.com",
  "tipoServicio": "Acarreo de materiales",
  "descripcion": "Modalidad: servicio puntual | Recoleccion: Mejicanos | Destino: Soyapango"
}
```

```http
POST /api/publico/empleo
{
  "nombre": "Steven Ayala",
  "dui": "12345678-9",
  "telefono": "+503 7000 0000",
  "email": null,
  "tipoPuesto": "Motorista de volqueta"
}
```

Los dos crean o reutilizan el `usuario_web` según el DUI y devuelven `201`.

---

## 5. Formato de errores

Todas las respuestas de error tienen la misma forma:

```json
{
  "estado": 400,
  "mensaje": "Los datos enviados no son validos.",
  "errores": { "Dui": ["El DUI debe tener el formato 00000000-0."] }
}
```

| Código | Significado |
|---|---|
| 400 | Datos inválidos |
| 401 | Falta el header `X-Api-Key` |
| 403 | Llave incorrecta |
| 404 | No existe |
| 409 | Duplicado, o borrado con registros hijos |
| 429 | Pasaste el límite de peticiones |
| 500 | Error del servidor |

---

## 6. Consumir la API

### 6.1 JavaFX (módulo administrador)

CORS no aplica: no es un navegador. Usá el puerto **HTTP** para evitar el certificado autofirmado.

```java
import java.net.URI;
import java.net.http.*;

public class SacorApi {
    private static final String BASE = "http://localhost:5084";
    private static final String API_KEY = System.getenv("SACOR_API_KEY");

    private final HttpClient http = HttpClient.newBuilder()
            .connectTimeout(java.time.Duration.ofSeconds(10))
            .build();

    public String listarEmpleados(int pagina) throws Exception {
        HttpRequest req = HttpRequest.newBuilder()
                .uri(URI.create(BASE + "/api/empleados?pagina=" + pagina + "&tamano=50"))
                .header("X-Api-Key", API_KEY)
                .header("Accept", "application/json")
                .timeout(java.time.Duration.ofSeconds(20))
                .GET()
                .build();

        HttpResponse<String> res = http.send(req, HttpResponse.BodyHandlers.ofString());
        if (res.statusCode() != 200) throw new RuntimeException("Error " + res.statusCode() + ": " + res.body());
        return res.body();
    }
}
```

La llave se lee de una variable de entorno, no del código. En IntelliJ se configura en *Run > Edit Configurations > Environment variables*.

### 6.2 Android / Kotlin (módulo trabajador)

Dos detalles del emulador:

1. `localhost` apunta al propio emulador. El host es **`10.0.2.2`**.
2. Android bloquea HTTP sin cifrar por defecto. Para desarrollo se habilita solo a esa IP.

`res/xml/network_security_config.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
    <domain-config cleartextTrafficPermitted="true">
        <domain includeSubdomains="false">10.0.2.2</domain>
    </domain-config>
</network-security-config>
```

En `AndroidManifest.xml`:

```xml
<application
    android:networkSecurityConfig="@xml/network_security_config"
    ... >
```

Con Retrofit:

```kotlin
private const val BASE_URL = "http://10.0.2.2:5084/"

private val cliente = OkHttpClient.Builder()
    .connectTimeout(10, TimeUnit.SECONDS)
    .readTimeout(20, TimeUnit.SECONDS)
    .addInterceptor { cadena ->
        val peticion = cadena.request().newBuilder()
            .addHeader("X-Api-Key", BuildConfig.SACOR_API_KEY)
            .build()
        cadena.proceed(peticion)
    }
    .build()

val retrofit: Retrofit = Retrofit.Builder()
    .baseUrl(BASE_URL)
    .client(cliente)
    .addConverterFactory(GsonConverterFactory.create())
    .build()
```

`BuildConfig.SACOR_API_KEY` sale de `local.properties`, que no se sube al repo:

```kotlin
// build.gradle.kts del módulo app
val props = Properties().apply {
    load(rootProject.file("local.properties").inputStream())
}

android {
    defaultConfig {
        buildConfigField("String", "SACOR_API_KEY", "\"${props["sacorApiKey"]}\"")
    }
    buildFeatures { buildConfig = true }
}
```

> Si corrés la app en un celular físico en vez del emulador, `10.0.2.2` no sirve: usá la IP de tu PC en la red (`ipconfig`) y habilitá el puerto 5084 en el firewall.

### 6.3 Otra app web

Agregá su origen a `Seguridad:OrigenesPermitidos` en tu `appsettings.Development.json`, o el navegador bloquea las peticiones por CORS.

---

## 7. Subir el repositorio

Desde la carpeta que contiene `APISacor.sln`:

```bash
git init
git add .
git status
```

Antes del primer commit, verificá que **no** aparezcan en la lista:

- `appsettings.Development.json`
- `bin/`, `obj/`, `.vs/`

Si aparece alguno, revisá el `.gitignore`.

```bash
git commit -m "API SACOR con CRUD y capa de seguridad"
git branch -M main
git remote add origin https://github.com/USUARIO/REPO.git
git push -u origin main
```

Tus compañeros después:

```bash
git clone https://github.com/USUARIO/REPO.git
cd REPO/APISacor
# copiar appsettings.Development.json.ejemplo y ajustar el servidor
# correr db/sacor_completo.sql en SSMS
dotnet user-secrets init
dotnet user-secrets set "Seguridad:ApiKeys:AppEscritorio" "su-llave"
dotnet run
```

---

## 8. Pendientes

- Autenticación individual: **implementada únicamente en `/api/movil`** mediante activación y tokens personales revocables. El CRUD legado sigue usando API Keys que identifican a la app, NO al empleado; no conectar el CRUD directamente a la APK.
- Contabilidad distinta de pago y anticipo, pendiente de reunión.
- Los detalles del trabajo de una cotización van todos dentro de `descripcion`. Si se necesitan consultar por separado, hay que agregar columnas a la tabla.

---

## 9. Integración móvil (ampliación)

Leer primero `README_CAMBIOS_MOVIL.md`, luego `docs/INTEGRACION_MOVIL.md`.
Toda la base está en un solo archivo: `db/sacor_completo.sql` (tablas, relaciones, índices, procedimientos y datos iniciales). No ejecutarlo sobre una base con datos porque la recrea desde cero.

Los 13 procedimientos almacenados del script no los usa la API (trabaja con Entity Framework); quedan como respaldo para operaciones directas en SQL Server y aplican las mismas reglas.

### 9.1 Evidencias y fotos

Todas las evidencias de la app móvil se guardan como filas de `limpieza`, diferenciadas por `etapa`:

| etapa | Quién | Endpoint |
|---|---|---|
| Evidencia de limpieza | Transportista (UC-06) | `POST /api/movil/transportista/evidencias` con `tipo: "limpieza"` |
| Evidencia de ruta | Transportista (UC-07) | `POST /api/movil/transportista/evidencias` con `tipo: "ruta"` |
| Información adicional | Transportista | `POST /api/movil/transportista/informacion-adicional` (km, litros, gasto) |
| Recolección finalizada | Técnico (UC-10/11) | `POST /api/movil/tecnico/recoleccion` |

Fotos: `POST /api/movil/evidencias/{idLimpieza}/fotos` con `{ "contenidoBase64": "..." }` (JPG o PNG, máx. 3 MB, 3 por evidencia, solo el autor). `GET /api/movil/fotos/{idFoto}` devuelve la imagen al autor o a un administrador.

Los viajes extra y servicios externos creados desde el móvil se guardan con el prefijo `[aaaa-mm]` para que la app de escritorio los cuente en su mes.

### 9.2 Tipos de usuario

| Tipo (valor en `empleado.tipo`) | App | Qué hace |
|---|---|---|
| `AdministradorOperativo` | Escritorio | Planilla, pesaje, contabilidad, clientes, cotizaciones, salidas |
| `AdministradorTecnico` | Móvil (rol `administrador`) | Registra personal de campo, crea y asigna viajes, servicios externos |
| `Tecnico` | Móvil (rol `tecnico`) | Horarios, recolección y sus fotos |
| `Motorista` | Móvil (rol `transportista`) | Horarios, evidencias de limpieza y ruta, información adicional, historial |

`Ayudante`, `Mecanico` y `Coordinador` ya no existen: se conservan solo para empleados inactivos (la API y un CHECK de la base impiden activarlos). Desde el móvil el administrador técnico solo crea y cambia el estado de motoristas y técnicos.

### 9.3 Contratos, viajes y personal

Un contrato (`servicio_contrato`) tiene varios viajes (`ruta_trabajo.id_servicio_contrato`) y cada viaje tiene su personal (`ruta_empleado`) y su camión (`ruta_trabajo.id_camion`). Las evidencias, la información adicional y las recolecciones se guardan por viaje (`limpieza.id_ruta_trabajo`).

| Endpoint | Quién | Qué hace |
|---|---|---|
| `POST /api/cotizaciones/{id}/generar-viajes` | Escritorio (API Key) | Al aprobar una cotización: crea cliente, precio, contrato (`id_cotizacion`) y sus viajes con fecha según la frecuencia. Una sola vez por cotización. |
| `GET /api/movil/admin/contratos` | Administrador técnico | Contratos con viajes y cuántos tienen personal |
| `GET /api/movil/admin/contratos/{id}/viajes` | Administrador técnico | Viajes del contrato con fecha, personal, camión y evidencias |
| `PUT /api/movil/admin/viajes/{id}/asignacion` | Administrador técnico | `{ idEmpleados, idCamion }` reemplaza el personal de un viaje |
| `POST /api/movil/admin/contratos/{id}/asignar` | Administrador técnico | Igual, para todos los viajes del contrato (`soloSinPersonal: true` para solo los vacíos) |
| `GET /api/movil/viajes` | Motorista y técnico | Calendario: sus viajes con fecha, cliente, camión, equipo y evidencias |
| `POST /api/movil/transportista/evidencias` | Motorista | `{ idRutaTrabajo, tipo, texto }` |
| `POST /api/movil/transportista/informacion-adicional` | Motorista | `{ idRutaTrabajo, kilometros, combustibleLitros, gastoUsd }` |
| `GET /api/movil/tecnico/viajes/{id}/materiales` | Técnico | Materiales (precios por tonelada de la plaza del contrato) y conceptos de gasto |
| `POST /api/movil/tecnico/recoleccion` | Técnico | `{ idRutaTrabajo, observacion, cargas: [{ idPrecioLugar, toneladas }], gastos: [{ concepto, monto, descripcion }] }`. Cada carga crea un `pesaje` (toneladas × 2200 libras) y cada gasto un `gasto_viaje` |
| `GET /api/movil/tecnico/registros` | Técnico | Historial por viaje: cargas con su cobro, gastos y observaciones |

Solo se puede registrar algo en un viaje al que el empleado está asignado. Los gastos de `gasto_viaje` (también el gasto de la información adicional del motorista) suman en la app de escritorio como "Gastos de viajes"; el CRUD es `/api/gastos-viaje`.
