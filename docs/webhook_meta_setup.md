# Configuración del Webhook en Meta for Developers

## Prerequisitos

- Cuenta en [Meta for Developers](https://developers.facebook.com)
- App de Meta creada con el producto **WhatsApp** habilitado
- ngrok instalado para pruebas locales

---

## Paso 1 — Obtener credenciales de la app

1. Entra a [developers.facebook.com](https://developers.facebook.com) y selecciona tu app
2. Menú izquierdo → **WhatsApp** → **Configuración de la API**
3. Anota los siguientes valores:

| Campo | Dónde encontrarlo | Variable en appsettings |
|-------|-------------------|------------------------|
| Phone Number ID | Sección "Números de teléfono" | `WhatsApp:PhoneNumberId` |
| Access Token | Sección "Token de acceso temporal" (o Token permanente) | `WhatsApp:AccessToken` |

> **Token permanente:** Ve a tu App → Configuración → Avanzada → Token del sistema.
> Es necesario para producción ya que el token temporal expira en 24 horas.

---

## Paso 2 — Configurar el VerifyToken

El `VerifyToken` es un secreto que tú defines. Meta lo envía en la petición GET de verificación para confirmar que eres tú quien controla el endpoint.

1. Elige un token secreto arbitrario (ej. `mi-token-secreto-2025`)
2. Actualiza `appsettings.json`:

```json
"WhatsApp": {
  "VerifyToken": "mi-token-secreto-2025"
}
```

---

## Paso 3 — Exponer el servidor localmente con ngrok

```bash
# Instalar ngrok (si no está instalado)
brew install ngrok        # macOS
# o descargar desde https://ngrok.com/download

# Autenticar (solo una vez)
ngrok config add-authtoken TU_AUTHTOKEN

# Exponer el puerto de la aplicación
ngrok http 5000
```

ngrok mostrará una URL pública similar a:
```
Forwarding  https://abc123.ngrok.io -> http://localhost:5000
```

Copia la URL HTTPS (la HTTP puede causar problemas con Meta).

---

## Paso 4 — Registrar el webhook en Meta

1. En tu app de Meta → **WhatsApp** → **Configuración** → **Webhooks**
2. Haz clic en **Editar**
3. Completa los campos:
   - **URL de devolución de llamada:** `https://abc123.ngrok.io/webhook`
   - **Token de verificación:** el valor de `WhatsApp:VerifyToken` en tu appsettings
4. Haz clic en **Verificar y guardar**

Meta enviará una petición GET a tu URL con el challenge. Si tu app responde correctamente, el webhook queda configurado.

---

## Paso 5 — Suscribirse a los eventos de mensajes

Después de guardar el webhook:

1. En la misma pantalla de Webhooks, haz clic en **Administrar**
2. Activa la suscripción para el campo: **`messages`**
3. Guarda los cambios

---

## Paso 6 — Agregar número de prueba

1. **WhatsApp** → **Configuración de la API** → **Enviar y recibir mensajes**
2. En "Para:", selecciona el número de teléfono de prueba
3. En "Número de WhatsApp receptor:", agrega tu número personal
4. Envía el mensaje de plantilla de bienvenida para iniciar la sesión

---

## Verificación rápida con curl

```bash
# Verificar que el endpoint GET funciona
curl "https://abc123.ngrok.io/webhook?hub.mode=subscribe&hub.verify_token=mi-token-secreto-2025&hub.challenge=test123"
# Respuesta esperada: test123

# Simular un mensaje entrante
curl -X POST "https://abc123.ngrok.io/webhook" \
  -H "Content-Type: application/json" \
  -d '{
    "object": "whatsapp_business_account",
    "entry": [{
      "id": "TEST",
      "changes": [{
        "value": {
          "messaging_product": "whatsapp",
          "metadata": {"display_phone_number": "5200000000", "phone_number_id": "TEST"},
          "contacts": [{"profile": {"name": "Test"}, "wa_id": "521XXXXXXXXXX"}],
          "messages": [{
            "from": "521XXXXXXXXXX",
            "id": "wamid.test",
            "timestamp": "1716163200",
            "type": "text",
            "text": {"body": "hola"}
          }]
        },
        "field": "messages"
      }]
    }]
  }'
# Respuesta esperada: HTTP 200 (vacío)
```

---

## Notas importantes

- **El endpoint siempre debe retornar HTTP 200** en menos de 15 segundos. Si no lo hace, Meta reintenta el envío hasta 3 veces.
- En producción, reemplaza ngrok con tu dominio real con certificado SSL válido.
- El token de acceso temporal expira en 24 horas. Para producción usa un **System User Token** permanente.
