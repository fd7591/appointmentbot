# Bot de Facturación — WhatsApp

Bot de WhatsApp para consultorios médicos que permite a los pacientes **agendar citas** y **solicitar facturas** (CFDI) de forma conversacional, directamente desde WhatsApp.

## Características

- **Módulo 1 — Agendar Cita:** Flujo guiado con selección de fecha y horario disponible. Recordatorios automáticos 24 horas antes.
- **Módulo 2 — Solicitar Factura:** Captura de RFC, datos fiscales y datos de la consulta. Registro automático en Google Sheets para el contador.
- **Estado de conversación persistido** en SQL Server (timeout de sesión configurable).
- **Menú interactivo** con botones nativos de WhatsApp.
- **Arquitectura limpia** (Domain / Application / Infrastructure / API).
- **Fase 2 preparada** con interfaces para integración con PAC (timbrado de CFDI).

## Stack tecnológico

| Componente | Tecnología |
|---|---|
| Framework | .NET 8 / ASP.NET Core |
| WhatsApp API | Meta Cloud API (Webhooks) |
| Base de datos | SQL Server + Entity Framework Core 8 |
| Google Sheets | Google.Apis.Sheets.v4 |
| Autenticación Sheets | Google Service Account |
| Background Jobs | IHostedService (.NET) |

---

## Estructura del proyecto

```
botFacturacion/
├── botFacturacion.sln
├── src/
│   ├── Domain/                          # Entidades, Enums (sin dependencias)
│   │   ├── Entities/
│   │   │   ├── Cita.cs
│   │   │   ├── ConversationState.cs
│   │   │   ├── Disponibilidad.cs
│   │   │   ├── Doctor.cs
│   │   │   ├── ReceptorFiscal.cs
│   │   │   └── SolicitudFactura.cs
│   │   └── Enums/
│   │       ├── EstadoSolicitud.cs
│   │       ├── FlowType.cs
│   │       ├── MetodoPago.cs
│   │       ├── RegimenFiscal.cs
│   │       ├── StepType.cs
│   │       └── UsoCFDI.cs
│   ├── Application/                     # Lógica de negocio (casos de uso)
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   └── Services/
│   │       ├── ConversationService.cs   # Dispatcher principal del bot
│   │       ├── CitaService.cs           # Flujo de agendar cita
│   │       └── FacturaService.cs        # Flujo de solicitar factura
│   ├── Infrastructure/                  # Implementaciones externas
│   │   ├── Persistence/                 # EF Core, repositorios
│   │   ├── ExternalServices/            # WhatsApp API, Google Sheets
│   │   └── BackgroundServices/          # Recordatorios, limpieza de sesiones
│   └── API/                             # ASP.NET Core Web API
│       ├── Controllers/WebhookController.cs
│       ├── Middleware/
│       ├── Program.cs
│       └── appsettings.json
└── docs/
    ├── setup.sql                        # Script de creación de tablas
    ├── webhook_meta_setup.md            # Instrucciones Meta for Developers
    ├── google_cloud_setup.md            # Instrucciones Google Cloud Console
    └── postman_collection.json          # Colección de Postman para pruebas
```

---

## Instalación y ejecución local

### Prerequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server 2019+ o SQL Server Express o LocalDB
- ngrok (para pruebas del webhook)
- Cuenta de Meta for Developers con una app de WhatsApp configurada

### 1. Clonar y restaurar dependencias

```bash
git clone <url-del-repo>
cd botFacturacion
dotnet restore
```

### 2. Configurar la base de datos

**Opción A — EF Core Migrations (recomendado para desarrollo):**

```bash
# Instalar herramientas EF Core (solo una vez)
dotnet tool install --global dotnet-ef

# Crear la migración inicial
dotnet ef migrations add InitialCreate --project src/Infrastructure --startup-project src/API

# Aplicar la migración (crea la BD y todas las tablas)
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

**Opción B — Script SQL:**

```bash
# Abrir SQL Server Management Studio o sqlcmd
sqlcmd -S localhost -i docs/setup.sql
```

### 3. Configurar Google Sheets

Sigue las instrucciones en `docs/google_cloud_setup.md`.

Coloca el archivo JSON de la Service Account en:
```
src/API/config/google-service-account.json
```

### 4. Actualizar appsettings

Edita `src/API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=BotFacturacion;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "WhatsApp": {
    "PhoneNumberId": "TU_PHONE_NUMBER_ID",
    "AccessToken": "TU_ACCESS_TOKEN",
    "VerifyToken": "MI_TOKEN_SECRETO"
  },
  "GoogleSheets": {
    "ServiceAccountPath": "config/google-service-account.json",
    "SpreadsheetId": "TU_SPREADSHEET_ID",
    "SheetName": "Solicitudes"
  }
}
```

> Para desarrollo, usa `appsettings.Development.json` (tiene configuración de LocalDB).

### 5. Ejecutar la aplicación

```bash
cd src/API
dotnet run
```

La API arranca en `http://localhost:5000` (o el puerto configurado en `launchSettings.json`).

Swagger disponible en: `http://localhost:5000`

### 6. Exponer con ngrok

```bash
ngrok http 5000
```

Copia la URL HTTPS y sigue las instrucciones en `docs/webhook_meta_setup.md`.

### 7. Probar con Postman

Importa `docs/postman_collection.json` en Postman:
1. Abre Postman → **Import**
2. Selecciona el archivo JSON
3. Actualiza la variable `base_url` con tu URL de ngrok
4. Ejecuta las peticiones en orden

---

## Flujos del bot

### Menú principal

Escribe cualquiera de estos textos para ver el menú:
- `hola`, `menu`, `menú`, `inicio`

```
Hola, [Nombre]! 👋

Bienvenido al bot del consultorio.

[📅 Agendar cita]  [🧾 Solicitar factura]  [📋 Mis citas]

Escribe ayuda para más información o cancelar para reiniciar.
```

### Comandos globales

| Comando | Efecto |
|---|---|
| `menu` | Muestra el menú principal y reinicia el flujo |
| `cancelar` | Cancela la operación actual y reinicia el flujo |
| `ayuda` | Muestra información de ayuda |

### Flujo Agendar Cita

```
1. Nombre del paciente (texto libre)
2. Fecha (lista de los próximos 7 días, sin domingos)
3. Horario disponible (recuperado de la tabla Disponibilidades)
4. Motivo de consulta (texto libre)
5. Confirmación del resumen (Sí / No)
→ Genera folio CITA-YYYYMMDD-XXXX
→ Recordatorio automático 24h antes (Background Service)
```

### Flujo Solicitar Factura

```
1. RFC del receptor (validado con regex SAT)
   ├── RFC encontrado → mostrar datos guardados → confirmar o actualizar
   └── RFC nuevo → capturar datos fiscales campo por campo
2. Datos de la consulta:
   - Fecha (DD/MM/YYYY, no futura, máx 2 años atrás)
   - Monto (numérico, 0 < monto ≤ 999,999)
   - Método de pago (lista SAT)
   - Forma de pago (PUE / PPD)
   - Uso del CFDI (lista SAT)
3. Resumen completo → confirmación final
→ Genera folio FACT-YYYYMMDD-XXXX
→ Registra en BD y Google Sheets (columnas A-S)
```

---

## Migraciones de base de datos

```bash
# Crear nueva migración
dotnet ef migrations add NombreDeLaMigracion \
  --project src/Infrastructure \
  --startup-project src/API

# Aplicar migraciones pendientes
dotnet ef database update \
  --project src/Infrastructure \
  --startup-project src/API

# Revertir última migración
dotnet ef database update NombreMigracionAnterior \
  --project src/Infrastructure \
  --startup-project src/API
```

---

## Fase 2 — Timbrado de CFDI (pendiente)

Las interfaces `IPACService` y `ICFDIBuilder` están definidas en:
`src/Application/Interfaces/IPACService.cs`

El flujo previsto es:
1. Contador abre Google Sheets y cambia el estado de "PENDIENTE" a "EN_PROCESO"
2. Un job de polling detecta el cambio
3. El sistema genera el XML del CFDI 4.0
4. Se timbra contra el PAC configurado
5. El UUID se almacena en BD y en la columna S del Sheets
6. Se envía el PDF + XML al solicitante por WhatsApp

---

## Variables de entorno para producción

```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;"
export WhatsApp__AccessToken="token-permanente"
export WhatsApp__PhoneNumberId="id-del-numero"
export WhatsApp__VerifyToken="token-secreto"
export GoogleSheets__SpreadsheetId="id-del-spreadsheet"
```

---

## Licencia

Uso interno — consultorio médico.
