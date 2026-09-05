# Configuración de Google Cloud — Service Account para Google Sheets

## ¿Por qué una Service Account?

La aplicación necesita acceso a Google Sheets **sin intervención humana** (sin login interactivo). Una Service Account es una identidad de aplicación que puede autenticarse automáticamente con sus propias credenciales (archivo JSON).

---

## Paso 1 — Crear proyecto en Google Cloud

1. Abre [console.cloud.google.com](https://console.cloud.google.com)
2. Haz clic en el selector de proyectos (arriba) → **Nuevo proyecto**
3. Nombre sugerido: `bot-facturacion`
4. Haz clic en **Crear**
5. Selecciona el nuevo proyecto

---

## Paso 2 — Habilitar la API de Google Sheets

1. Menú izquierdo → **APIs y servicios** → **Biblioteca**
2. Busca: `Google Sheets API`
3. Haz clic en el resultado → **Habilitar**

---

## Paso 3 — Crear la Service Account

1. Menú izquierdo → **APIs y servicios** → **Credenciales**
2. Haz clic en **+ Crear credenciales** → **Cuenta de servicio**
3. Completa:
   - **Nombre:** `bot-facturacion-sheets`
   - **ID:** se llena automáticamente
   - **Descripción:** `Cuenta de servicio para escritura en Google Sheets`
4. Haz clic en **Crear y continuar**
5. En "Rol", selecciona: **Editor** (o "Básico > Editor")
6. Haz clic en **Continuar** → **Listo**

---

## Paso 4 — Descargar el archivo JSON de credenciales

1. En la página de Credenciales, haz clic en la Service Account que acabas de crear
2. Ve a la pestaña **Claves**
3. Haz clic en **Agregar clave** → **Crear clave nueva**
4. Selecciona formato: **JSON**
5. Haz clic en **Crear** — el archivo se descarga automáticamente
6. Renombra el archivo a `google-service-account.json`
7. Muévelo a: `src/API/config/google-service-account.json`

> **IMPORTANTE:** Este archivo contiene credenciales privadas.
> Nunca lo subas a git. Ya está en `.gitignore`.

El archivo tiene esta estructura:
```json
{
  "type": "service_account",
  "project_id": "bot-facturacion",
  "private_key_id": "...",
  "private_key": "-----BEGIN RSA PRIVATE KEY-----\n...",
  "client_email": "bot-facturacion-sheets@bot-facturacion.iam.gserviceaccount.com",
  "client_id": "...",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token"
}
```

---

## Paso 5 — Crear y configurar el Google Spreadsheet

1. Abre [sheets.google.com](https://sheets.google.com)
2. Crea un nuevo spreadsheet: **"Solicitudes de Factura — Consultorio"**
3. En la primera pestaña, renómbrala a: `Solicitudes`
4. Agrega los encabezados en la fila 1 (columnas A–S):

| Col | Encabezado |
|-----|-----------|
| A | Folio |
| B | Fecha Solicitud |
| C | Teléfono |
| D | RFC |
| E | Razón Social |
| F | Régimen (Clave) |
| G | Régimen (Descripción) |
| H | Código Postal |
| I | Email |
| J | Fecha Consulta |
| K | Monto |
| L | Método Pago (Clave) |
| M | Método Pago (Descripción) |
| N | Forma de Pago |
| O | Uso CFDI (Clave) |
| P | Uso CFDI (Descripción) |
| Q | Estado |
| R | Notas Contador |
| S | Folio Fiscal UUID |

---

## Paso 6 — Compartir el Spreadsheet con la Service Account

1. Obtén el email de la Service Account (está en el archivo JSON, campo `client_email`)
   - Ejemplo: `bot-facturacion-sheets@bot-facturacion.iam.gserviceaccount.com`
2. En el Spreadsheet, haz clic en **Compartir** (botón azul, arriba a la derecha)
3. Pega el email de la Service Account
4. Rol: **Editor**
5. Desmarca "Notificar a las personas"
6. Haz clic en **Compartir**

---

## Paso 7 — Obtener el ID del Spreadsheet

La URL del Spreadsheet tiene este formato:
```
https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit#gid=0
```

Copia el `SPREADSHEET_ID` y actualiza `appsettings.json`:
```json
"GoogleSheets": {
  "ServiceAccountPath": "config/google-service-account.json",
  "SpreadsheetId": "TU_SPREADSHEET_ID_AQUI",
  "SheetName": "Solicitudes"
}
```

---

## Paso 8 — Verificar la conexión

Ejecuta la aplicación y revisa los logs. Deberías ver:
```
info: GoogleSheetsService[0]
      Conexión a Google Sheets OK: 'Solicitudes de Factura — Consultorio'
```

Si hay error de autenticación, verifica:
- La ruta del archivo JSON es correcta (relativa al directorio de trabajo del API)
- El Spreadsheet fue compartido con el email exacto de la Service Account
- La API de Google Sheets está habilitada en el proyecto

---

## Configuración para producción

Para producción, en lugar de almacenar el archivo JSON en disco, considera:

1. **Variables de entorno:** Convertir el JSON a base64 y leerlo como variable de entorno
2. **Azure Key Vault / AWS Secrets Manager:** Almacenar el JSON como secreto
3. **Workload Identity** (si se despliega en GKE/Cloud Run): Sin archivos de credenciales

Ejemplo con variable de entorno (modifica `GoogleSheetsService.cs`):
```csharp
var jsonContent = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_JSON");
if (!string.IsNullOrEmpty(jsonContent))
{
    credential = GoogleCredential
        .FromJson(jsonContent)
        .CreateScoped(SheetsService.Scope.Spreadsheets);
}
```
