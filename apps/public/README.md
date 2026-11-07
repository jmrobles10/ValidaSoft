# apps/public: pantallas del tercero

App del tercero (docs/06 §3.2), **móvil primero** y en es-CO: enlace con OTP, mis empresas, invitación con textos legales, mi bóveda con subida en dos pasos y mi revisión con cada empresa. Usa el tema `createValidasoftTheme('light', { app: 'public' })` del design system «Registro»: densidad cómoda, controles de 44 px y radio 18 en tarjetas y diálogos.

Comandos, lint, pruebas y build: los del workspace (ver [apps/web/README.md](../web/README.md)). Esta app agrega:

```bash
pnpm --filter @validasoft/public dev        # Vite en :5181 (proxy de /public/v1 a la API y de /__mailpit a Mailpit)
pnpm --filter @validasoft/public build      # dist/ (sin /dev/correos)
```

## Rutas

| Ruta | Qué hace |
|---|---|
| `/e/:token` | Canjea el enlace del correo **una sola vez** (`POST /public/v1/enlaces/{token}/canjear`; StrictMode no lo repite). Si el tipo de vínculo exige OTP (202), pide el código de 6 dígitos y muestra los intentos restantes (`otp-invalido` trae `intentosRestantes`). Enlace vencido o usado (410), inexistente (404), OTP vencido o bloqueado: pantalla con el siguiente paso. Con un solo vínculo en la sesión va directo a su invitación. |
| `/empresas` | Mis empresas: `GET /public/v1/vinculos` con estado, «Revisar y aceptar» o «Ver mi revisión». |
| `/empresas/:vinculoId` | Invitación: textos vigentes (`GET /public/v1/invitaciones/{id}`) en orden aviso de la plataforma (solo si falta), autorización y términos de la empresa, cada uno con versión y fecha y su casilla. «Aceptar» envía tipo y versión exactos. `texto-desactualizado` recarga y desmarca el texto que cambió; `textos-legales-faltantes`, `otp-requerido` e `invitacion-ya-aceptada` tienen su mensaje. |
| `/boveda` | Mi bóveda (`GET /public/v1/boveda`): cada documento con su versión vigente y, por empresa, qué versión tiene. Menú: descargar (URL prefirmada) y retirar con motivo; «Ver versiones» con las devueltas y su motivo. |
| `/boveda/subir` | Tipo (canónicos globales de archivo) y Dropzone con «Tomar foto» (`capture="environment"`) y «Elegir archivo» (y arrastrar en escritorio). Valida 20 MB y formato en el navegador, calcula el SHA-256, `POST …/documentos` → `PUT` directo al almacenamiento con progreso → `…/confirmar`. Estados: lista (con qué empresas la recibieron), duplicado, devuelta con motivo, 413, 415, sin red y URL vencida. `?documento=` sube una versión nueva del mismo documento. |
| `/expediente/:id` | Mi revisión: estado («te faltan N documentos» en la fase de carga), los documentos que pide la empresa agrupados en «Te toca», «Lo revisa la empresa» y «Listo», cada observado con sus motivos y su observación, y desde cada tarjeta «Subir documento», «Subir versión corregida» u «Subir otra versión» (va a `/boveda/subir` con el tipo fijo y vuelve aquí). «Enviar respuesta» reenvía con o sin respuesta escrita (`requisitos-sin-corregir` se explica). Se refresca solo mientras esté abierto o devuelto: la versión subida llega al expediente por una cola. |
| `/dev/correos` | Solo `vite dev`: lee Mailpit por el proxy `/__mailpit` y muestra «Abrir enlace» y el código OTP de cada correo. |

La sesión del tercero (JWT de 30 min) vive **solo en memoria** y viaja como `Authorization: Bearer`; un 401 o el vencimiento muestran «Tu sesión terminó». Al recargar se pierde: la cookie llegará después.

## Probar el recorrido

```bash
docker compose -f infra/compose/compose.yaml up -d      # PostgreSQL, Mailpit (:8025) y MinIO (:9000, consola :9001)
ASPNETCORE_ENVIRONMENT=Development Validasoft__Correo__UrlPublica=http://localhost:5181 dotnet run --project src/Validasoft.Api --urls http://localhost:5080
pnpm semilla:dev                                         # PROVEEDOR con OTP, flujo con requisitos (RUT y Cámara) publicado y textos de Norte
pnpm --filter @validasoft/public dev
```

1. `http://localhost:5181/dev/correos` → «Abrir enlace» del correo de `contacto1…` (Aceros del Norte, proveedor de Norte).
2. El código llega en otro correo (aparece en la misma página): escríbelo y acepta los tres textos. Se abre el expediente: «Abierto» mientras falte un documento (si el tercero ya tenía el RUT en su bóveda, falta solo la Cámara).
3. Mi bóveda → Subir documento → RUT → elegir un PDF: queda como versión 1 y «Ya la tiene: Constructora Norte S.A.S.».
4. En `apps/web` (embed-demo, Ana): vínculo de Aceros del Norte → «Documentos compartidos» → «Ver o descargar».

## Pedidos al backend

0. **Volver a entrar a un vínculo aceptado.** Cuando el tercero ya usó su enlace (y la sesión vive en memoria), no tiene forma de volver: `POST /vinculos/{id}/invitaciones` solo admite vínculos `Invitado` (409 `estado-invalido`) y el autorregistro solo escribe a los contactos del tercero global (los de su primera creación, a veces de otro tenant), no al `correoTercero` del vínculo. En la verificación se resolvió invitando otro vínculo al mismo correo. Pedir un `POST /public/v1/acceso { correo }` que envíe un enlace de acceso para los vínculos con ese `correoTercero` (respuesta siempre igual), o permitir reenviar el enlace de acceso de un vínculo `EnValidacion`/`Activo`. Es lo que necesita el tercero para responder una observación.
1. **Catálogo para el tercero.** Para subir por su cuenta (sin requisito), la lista de tipos está fija en `src/boveda/catalogo.ts` (canónicos globales de archivo) porque `GET /api/v1/catalogos/tipos-documento` no acepta su sesión. Desde la revisión ya sube lo que pide cada requisito. `ExpedientePublicoDto` tampoco trae `vinculoId` (hace falta para un tipo propio de la empresa); hoy se toma de `GET /public/v1/vinculos`.
2. **Contexto del enlace antes de canjear** (`EnlaceContexto` de docs/06 §5): empresa, tipo de vínculo, `requiereOtp` y correo enmascarado. Sin eso la landing no puede decir «Constructora Norte te pide…» antes del código, ni a qué correo llegó. El 202 del canje (`OtpRequeridoDto`) tampoco trae el correo enmascarado.
3. **Marca y soporte de la empresa**: `EmpresaPublicaDto` no trae color (para `derivarPaletaEmpresa`) ni correo de soporte, que el design system muestra en el pie y en Mis empresas.
4. **Estado por empresa en la bóveda**: `ComparticionPublicaDto` dice qué versión tiene cada empresa pero no si la aprobó u observó (llega con los requisitos).
5. **CORS del almacenamiento en producción**: el `PUT` prefirmado sale del origen de `apps/public` directo a S3/MinIO. En desarrollo MinIO acepta cualquier origen; en producción el bucket necesita CORS para ese origen (`PUT`, cabecera `Content-Type`).
6. Canje, inicio de carga y autorregistro siguen publicándose como `IResult` en OpenAPI; `transformar.ts` declara sus respuestas reales (`SesionTerceroDto | OtpRequeridoDto`, `CargaPrefirmadaDto`, `SolicitudRecibidaDto`).

## Pendiente

Cookie de sesión (hoy en memoria); conversión HEIC y unir varias fotos en un PDF (el Dropzone del design system usa `pdf-lib` y `heic2any` en un chunk aparte); SSE de la bóveda; ratificar una versión duplicada; personas relacionadas, cuentas bancarias, certificado y verificador; marca de la empresa en el tema; embed `/embed/expediente/:token`; PWA y cola sin conexión.
