# Guía para el equipo — Módulos administrador y trabajador

Esto es lo que necesitás para conectar tu app a la API de SACOR. Si algo no está acá, está en el `README.md`.

---

## 1. Antes de escribir código

```bash
git clone https://github.com/RuruiYo/TesisSoftware2026.git
cd TesisSoftware2026/APISacor
```

1. Abrí `db/sacor_db.sql` en SSMS y ejecutalo (F5). Crea la base `sacor` con sus 20 tablas.
2. Copiá `appsettings.Development.json.ejemplo` → `appsettings.Development.json` y cambiá `NOMBRE_DE_TU_SERVIDOR` por tu instancia (la ves en la barra de título de SSMS).
3. Generá tu llave y guardala:

```powershell
$b = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
-join ($b | ForEach-Object { $_.ToString('x2') })
```

```bash
dotnet user-secrets init
dotnet user-secrets set "Seguridad:ApiKeys:AppEscritorio" "tu-llave"   # JavaFX
dotnet user-secrets set "Seguridad:ApiKeys:AppMovil" "tu-llave"        # Android
```

4. `dotnet run`
5. Abrí `https://localhost:7210/swagger`, botón **Authorize**, pegá tu llave. Desde ahí probás cualquier endpoint sin escribir una línea de código.

> **Probá todo en Swagger primero.** Si un endpoint funciona ahí y no en tu app, el problema está en tu app, no en la API. Ahorra horas.

### Si te saltás el paso 3

La API arranca igual y Swagger abre, pero **todo `/api/...` devuelve `403`**. El mensaje dice "Credencial no autorizada", que parece llave equivocada cuando en realidad no hay ninguna.

Mirá la consola donde corriste `dotnet run`: si ves este aviso, ese es el problema.

```
=========================================================================
 NO HAY NINGUNA API KEY CONFIGURADA.
 La API va a responder 403 a todas las rutas /api/...
=========================================================================
```

Si en cambio ves `1 API Key(s) configurada(s).`, la llave está bien y el `403` viene de otro lado: revisá que la que manda tu app sea idéntica, sin espacios ni saltos de línea al copiarla.

**Cada uno corre su propia base y su propia API en su máquina.** No dependés de que Steven tenga la compu encendida.

---

## 2. Reglas que nadie puede saltarse

| Regla | Qué significa |
|---|---|
| **Header obligatorio** | `X-Api-Key: tu-llave` en *toda* petición a `/api/...`. Sin él, `401`. |
| **Paginación** | `GET` devuelve máximo 200 registros. Usá `?pagina=1&tamano=50`. |
| **Rate limit** | 120 peticiones por minuto. Si tu app hace polling agresivo, vas a comer `429`. |
| **Cuerpo máximo** | 1 MB por petición. |
| **El `id` del body se ignora** | En `POST` y `PUT` manda el id de la URL. |
| **`DELETE` puede fallar con `409`** | Si el registro tiene hijos, no se borra. Mostrá el mensaje al usuario, no lo escondas. |

---

## 3. Qué endpoints usa cada módulo

### Módulo administrador (JavaFX)

| Pantalla | Endpoints |
|---|---|
| Empleados | `/api/empleados` (CRUD completo) |
| Clientes | `/api/clientes` (CRUD completo) |
| Camiones y asignación | `/api/camiones`, `/api/camion-conductores` |
| Rutas | `/api/rutas-trabajo`, `/api/rutas-empleado` |
| Contratos de servicio | `/api/servicios-contrato`, `/api/precios-lugar` |
| Nómina | `/api/pagos`, `/api/anticipos-sueldo` |
| Observaciones de personal | `/api/observaciones-empleado` |
| Bandeja de cotizaciones | `GET/PUT /api/cotizaciones` |
| Bandeja de postulantes | `GET/PUT /api/solicitudes-puesto`, `GET /api/usuarios-web` |
| Viajes extra | `/api/viajes-extra`, `/api/viaje-extra-empleados` |

### Módulo trabajador (Android)

| Pantalla | Endpoints |
|---|---|
| Mi horario / marcar entrada y salida | `/api/horarios-trabajo` |
| Mis rutas del día | `GET /api/rutas-empleado`, `GET /api/rutas-trabajo` |
| Registrar pesaje | `POST /api/pesajes` |
| Registrar limpieza | `POST /api/limpiezas` |
| Camión asignado | `GET /api/camion-conductores` |
| Mis pagos y anticipos | `GET /api/pagos`, `GET /api/anticipos-sueldo` |

> Hoy **cualquier llave puede llamar cualquier endpoint**. La separación de arriba es un acuerdo del equipo, no algo que la API imponga. Cuando agreguemos login con JWT, sí se va a poder restringir de verdad.

---

## 4. Contrato JSON

El JSON usa **camelCase**. La clave primaria conserva el nombre de la tabla: `idEmpleado`, `idCliente`, `idPesaje`, etc.

### Formatos de tipos

| Tipo en la base | Cómo viaja en JSON | Ejemplo |
|---|---|---|
| `DATE` | Texto ISO con hora en cero | `"2026-09-17T00:00:00"` |
| `TIME(0)` | Texto `HH:mm:ss` | `"07:00:00"` |
| `DECIMAL(12,2)` | Número, sin comillas | `425.50` |
| Columna `NULL` | Se **omite** del JSON de salida | — |

> Esa última fila importa: la API no manda `"telefono": null`, directamente no incluye la propiedad. En Kotlin y Java declará esos campos como nullable con valor por defecto.

### Respuesta de un listado

```json
{
  "pagina": 1,
  "tamano": 50,
  "total": 137,
  "datos": [ { ... }, { ... } ]
}
```

### Empleado

```json
{
  "idEmpleado": 1,
  "codigoEmpleado": "EMP-001",
  "nombre": "Juan Perez",
  "dui": "12345678-9",
  "telefono": "+503 7000 0000",
  "sueldo": 400.00,
  "fechaIngreso": "2026-01-15T00:00:00",
  "codigoAf": null,
  "estado": "Activo",
  "tipo": "Motorista",
  "idAdministradorRegistro": 1
}
```

`estado` ∈ `Activo`, `Inactivo`, `Suspendido`
`tipo` ∈ `Administrador`, `Motorista`, `Tecnico`, `Ayudante`, `Mecanico`, `Coordinador`

> El campo `token` existe en la tabla pero **nunca** sale ni entra por la API.

### Cliente

```json
{
  "idCliente": 1,
  "nombre": "Constructora XYZ",
  "tipo": "Juridico",
  "direccion": "San Salvador",
  "correo": "contacto@xyz.com",
  "telefono": "+503 2222 3333",
  "nit": "0614-010101-101-1",
  "dui": null,
  "giro": "Construccion",
  "nrc": "123456-7",
  "nombreComercial": "XYZ",
  "idAdministradorRegistro": 1
}
```

`tipo` ∈ `Natural` (exige `dui`), `Juridico` (exige `nit`)

### Horario de trabajo

```json
{
  "idHorarioTrabajo": 1,
  "idEmpleadoMarco": 5,
  "fecha": "2026-09-17T00:00:00",
  "entrada": "07:00:00",
  "salida": "16:30:00"
}
```

La salida debe ser posterior a la entrada y la fecha no puede ser futura.

### Pesaje

```json
{
  "idPesaje": 1,
  "idServicioContrato": 3,
  "idEmpleadoSubio": 5,
  "idTecnicoTransporte": 7,
  "idPrecioLugar": 2,
  "fecha": "2026-09-17T00:00:00",
  "pesoTotal": 12500.00,
  "etapa": "Salida"
}
```

`pesoTotal` entre 0 y 100000.

### Cotización

```json
{
  "idCotizacion": 1,
  "idUsuarioWeb": 4,
  "idAdministradorAnalista": null,
  "fecha": "2026-09-17T00:00:00",
  "estado": "Pendiente",
  "tipoServicio": "Acarreo de materiales",
  "descripcion": "Modalidad: servicio puntual | Recoleccion: Mejicanos | Destino: Soyapango"
}
```

`estado` ∈ `Pendiente`, `EnRevision`, `Cotizada`, `Aprobada`, `Rechazada`, `Cerrada`

> Los detalles del trabajo vienen concatenados dentro de `descripcion` porque la tabla no tiene columnas para lugar, destino ni volumen. Si el módulo administrador los necesita separados, pedí que se agreguen columnas.

---

## 5. Manejo de errores

Todos los errores tienen la misma forma:

```json
{
  "estado": 400,
  "mensaje": "El DUI debe tener el formato 00000000-0.",
  "errores": { "Dui": ["El campo Dui es obligatorio."] }
}
```

`errores` solo aparece cuando falla la validación del modelo.

| Código | Causa típica | Qué hacer en la app |
|---|---|---|
| 400 | Dato inválido | Mostrar `mensaje` en el formulario |
| 401 | Falta el header | Revisar el interceptor |
| 403 | Llave incorrecta | Revisar la configuración |
| 404 | No existe | Refrescar la lista |
| 409 | Duplicado o tiene hijos | Mostrar `mensaje`, no reintentar |
| 429 | Demasiadas peticiones | Esperar 60s (viene el header `Retry-After`) |
| 500 | Error del servidor | Mensaje genérico y avisar al equipo |

**Mostrá siempre el campo `mensaje`.** Está redactado para que lo lea un usuario final, en español.

---

## 6. JavaFX — módulo administrador

Usá el puerto **HTTP** (`http://localhost:5084`). El HTTPS de desarrollo usa un certificado autofirmado que Java rechaza, y pelearse con el keystore no vale la pena para esto.

```java
public class SacorApi {
    private static final String BASE = "http://localhost:5084";
    private static final String API_KEY = System.getenv("SACOR_API_KEY");

    private final HttpClient http = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(10))
            .build();

    private HttpRequest.Builder base(String ruta) {
        return HttpRequest.newBuilder()
                .uri(URI.create(BASE + ruta))
                .header("X-Api-Key", API_KEY)
                .header("Accept", "application/json")
                .timeout(Duration.ofSeconds(20));
    }

    public String get(String ruta) throws Exception {
        HttpResponse<String> r = http.send(base(ruta).GET().build(),
                HttpResponse.BodyHandlers.ofString());
        if (r.statusCode() >= 400) throw new ApiException(r.statusCode(), r.body());
        return r.body();
    }

    public String post(String ruta, String json) throws Exception {
        HttpResponse<String> r = http.send(
                base(ruta).header("Content-Type", "application/json")
                          .POST(HttpRequest.BodyPublishers.ofString(json)).build(),
                HttpResponse.BodyHandlers.ofString());
        if (r.statusCode() >= 400) throw new ApiException(r.statusCode(), r.body());
        return r.body();
    }
}
```

**Dos cosas:**

- La llave sale de una variable de entorno, **nunca** escrita en el `.java`. En IntelliJ: *Run → Edit Configurations → Environment variables*.
- Las llamadas HTTP **no pueden ir en el hilo de JavaFX** o la interfaz se congela. Envolvelas en un `Task<T>` y actualizá la UI desde `setOnSucceeded`.

---

## 7. Android / Kotlin — módulo trabajador

**Dos trampas del emulador:**

1. `localhost` es el propio emulador, no tu PC. El host es **`10.0.2.2`**.
2. Android bloquea HTTP sin cifrar. Hay que permitirlo solo para esa IP.

`res/xml/network_security_config.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
    <domain-config cleartextTrafficPermitted="true">
        <domain includeSubdomains="false">10.0.2.2</domain>
    </domain-config>
</network-security-config>
```

`AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.INTERNET" />

<application android:networkSecurityConfig="@xml/network_security_config" ... >
```

Retrofit:

```kotlin
private const val BASE_URL = "http://10.0.2.2:5084/"

private val cliente = OkHttpClient.Builder()
    .connectTimeout(10, TimeUnit.SECONDS)
    .readTimeout(20, TimeUnit.SECONDS)
    .addInterceptor { cadena ->
        cadena.proceed(
            cadena.request().newBuilder()
                .addHeader("X-Api-Key", BuildConfig.SACOR_API_KEY)
                .build()
        )
    }
    .build()

val retrofit: Retrofit = Retrofit.Builder()
    .baseUrl(BASE_URL)
    .client(cliente)
    .addConverterFactory(GsonConverterFactory.create())
    .build()
```

La llave desde `local.properties` (que no se sube al repo):

```kotlin
// build.gradle.kts del módulo app
val props = Properties().apply { load(rootProject.file("local.properties").inputStream()) }

android {
    defaultConfig {
        buildConfigField("String", "SACOR_API_KEY", "\"${props["sacorApiKey"]}\"")
    }
    buildFeatures { buildConfig = true }
}
```

Modelo de ejemplo:

```kotlin
data class Empleado(
    val idEmpleado: Int = 0,
    val codigoEmpleado: String = "",
    val nombre: String = "",
    val dui: String = "",
    val telefono: String? = null,
    val sueldo: Double = 0.0,
    val fechaIngreso: String = "",
    val estado: String = "",
    val tipo: String = ""
)

data class Pagina<T>(
    val pagina: Int,
    val tamano: Int,
    val total: Int,
    val datos: List<T>
)
```

> Si probás en un celular físico, `10.0.2.2` no sirve. Usá la IP de la PC en la red (`ipconfig`), abrí el puerto 5084 en el firewall de Windows, y el celular tiene que estar en el mismo WiFi.

---

## 8. Acuerdos de trabajo

**No trabajen directo sobre `main`.**

```bash
git checkout -b escritorio/empleados
# trabajar, commitear
git push -u origin escritorio/empleados
```

Después abrís un Pull Request en GitHub.

**Quién toca qué:**

| Carpeta | Dueño |
|---|---|
| `APISacor/` | Steven. Si necesitás un cambio en la API, pedilo — no lo hagas por tu cuenta. |
| Tu módulo | Vos |

**Si necesitás algo de la API**, avisá con esta info: qué pantalla, qué datos te faltan, y si es un endpoint nuevo o un campo nuevo en uno existente.

**Antes de empezar cada día:**

```bash
git pull
```

---

## 9. Lo que todavía no existe

- **Login.** Hoy la API Key identifica a la app, no a la persona. No hay forma de saber qué empleado hizo cada operación. Si tu módulo lo necesita, avisá pronto: agregarlo después obliga a rehacer pantallas.
- **Filtros y búsqueda.** `GET` solo pagina; no hay `?nombre=juan` ni filtros por fecha. Si lo necesitás, pedilo.
- **Subida de archivos.** No hay endpoint para fotos ni PDFs.
- **Contabilidad** más allá de pago y anticipo, pendiente de reunión.
