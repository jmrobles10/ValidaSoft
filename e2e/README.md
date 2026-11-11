# Pruebas de punta a punta

Playwright con un navegador real contra Validasoft tal como se despliega: la API sirve la app embebible en `/app/` y la del tercero en `/`, con PostgreSQL, Mailpit (correo) y MinIO (archivos) de verdad. Cada prueba crea su propio grupo empresarial, así que corren en paralelo sin pisarse ni depender de datos previos.

```bash
docker compose -f infra/compose/compose.yaml up -d postgres mailpit minio
pnpm --filter @validasoft/e2e navegadores   # la primera vez: descarga Chromium
pnpm e2e                                     # compila las apps, publica la API en e2e/.api y la levanta en :5090
```

Con una API ya levantada que sirva las dos apps: `E2E_BASE_URL=http://localhost:5090 pnpm e2e`. Para no recompilar: `E2E_SIN_COMPILAR=1`.

En local las pruebas corren **aisladas** del entorno de desarrollo: base `validasoft_e2e` (se crea sola) y Mailpit propio (perfil `e2e` del compose, bandeja en http://localhost:8026). Así sus invitaciones y códigos no llegan al Mailpit de desarrollo, y la API de desarrollo no toma sus mensajes de la cola. Para usar la base y el Mailpit de desarrollo: `E2E_COMPARTIDO=1`. Para correr dos suites a la vez: otro `E2E_PUERTO` y otra `E2E_BASE_DATOS` (letras minúsculas y guion bajo). En CI no hace falta: el entorno ya es limpio.

## Cómo se arma cada caso

- **Empresa** (`soporte/semilla.ts`): grupo con Constructora Norte (Compras y Cumplimiento; Cumplimiento puede rechazar) y Constructora Sur (Revisión), el sistema integrado E2E con sus claves y personas con roles distintos.
- **Proveedor** (`soporte/proveedor.ts`): entra con el enlace del correo y el código (Mailpit), acepta, sube documentos (PDF generados en `soporte/archivos.ts`) y responde observaciones.
- **Tercero embebido por un sistema cualquiera** (`pruebas/embebido-tercero.spec.ts`, IO-6): la página anfitriona es `docs/ejemplos/anfitrion-tercero/index.html` servida por `page.route` en otro origen; su «backend» (la prueba) canjea la sesión del tercero con `POST /api/v1/sesiones-tercero`. Con `E2E_CAPTURAS=<carpeta>` las pruebas de pantallas nuevas guardan capturas en claro y en celular (`soporte/capturas.ts`).
- **Personas de la empresa** (`soporte/embebido.ts`, `soporte/empresa.ts`): usan Validasoft embebido en un iframe de otro origen; la sesión llega por `postMessage` con identidad delegada firmada por el sistema integrado. Para cambiar de sección se usa `irASeccion(app, nombre)`: la barra superior pasa a «Más» las pestañas que no caben en el ancho del iframe, así que la sección puede ser un enlace de la barra o un ítem de ese menú.
- **Sistema integrado** (`soporte/receptor.ts`): recibe los webhooks y verifica su firma.

## Qué cubre

| Archivo | Casos |
|---|---|
| `decisiones.spec.ts` | Aprobación de las dos áreas; observación con versión corregida que invalida lo decidido; rechazo; rechazo durante la devolución; respuesta con explicación; aprobación en nombre de un área |
| `multiempresa.spec.ts` | Dos empresas del grupo con el mismo proveedor (el enlace de cada correo abre su invitación); proveedor que también es cliente (reutiliza lo ya aprobado y revisa solo lo adicional, o queda activo al aceptar si no hay nada más); dos grupos distintos con su propia vigencia |
| `actualizaciones.spec.ts` | Versión nueva durante la revisión; el diálogo de decisión abierto cuando llega una versión nueva (el tiempo real avisa y no deja confirmar); formato no admitido y archivo repetido; actualización desde la bóveda después de aprobado |
| `vencimientos.spec.ts` | Vigente, por vencer, en gracia y vencido (con avisos); fecha futura; recálculo al cambiar la regla |
| `certificado.spec.ts` | Verificación pública auténtica y PDF; huella o folio inválidos; bloqueo y desbloqueo; revocación |
| `eventos.spec.ts` | Webhooks firmados en orden; entrega fallida y reenvío; campana y correo a la revisora |
| `lotes.spec.ts` | Invitación masiva: plantilla subida desde la pantalla, vista previa con errores por fila, lote procesado y terceros invitados; caducidad por plazo de cargue (job con fecha simulada para el tenant de la prueba) y reapertura |
| `informes.spec.ts` | Informe de estado de expedientes con tiempos por área, filtro y descarga en Excel; exportación del expediente en ZIP; las exportaciones en la auditoría de accesos |
| `desaprobacion.spec.ts` | Desaprobar (DS-1, DS-2): reabrir una etapa aprobada, volver a la fase inicial, desaprobar un expediente aprobado (vínculo a validación y certificado revocado, otro certificado al aprobarse de nuevo) y que solo el administrador ve las acciones |
| `reintegro.spec.ts` | Desvincular y reintegrar (DS-3): sin revalidar (estado restituido y certificado de nuevo), revalidando (renovación con todo el flujo) y el vínculo que nunca fue aprobado (el diálogo explica por qué no se puede sin revalidar) |
| `ciclo-vinculo.spec.ts` | Anular el veredicto de un área (cierre manual de una observación), anular el expediente y abrir otro, anular el vínculo (palabra de confirmación) y desvincular con el expediente abierto |
| `secuencia.spec.ts` | Varias etapas en secuencia (cada área ve el expediente solo cuando le toca), un área con dos aprobadores y la separación de funciones |
| `reintento.spec.ts` | Reinicio tras un rechazo (DS-4): el tercero ve los motivos y vuelve a postularse, la espera en días y el flujo que no lo permite (la empresa reabre desde la web) |
| `novedades-tercero.spec.ts` | Campana del portal del tercero: la observación llega con la marca de la empresa, se abre desde ahí al expediente y se apaga el correo del tipo; recordatorio de observaciones individual (desde el expediente) y en lote (desde la bandeja, uno por día, con correo y campana); aviso a las áreas que vuelven a revisar lo corregido cuando el tercero responde |
| `operacion.spec.ts` | Operación de la revisión (OP): campaña de renovación (filtro, vista previa, aviso antes de confirmar, proceso en segundo plano y correo y campana del tercero), plazo de revisión (configuración, avisos con el job y fecha simulada, indicador de la bandeja e informe de cumplimiento), ayuda de documentos de la empresa que lee el tercero, delegación de revisión (aviso, bandeja de quien recibe y revocación) y las plantillas de pre-vencimiento y vigencia |
| `tiempo-real.spec.ts` | Dos o tres navegadores a la vez, **sin recargar** (cada pantalla lleva una marca en su ventana: si se recargara, se perdería): el tercero designa su cuenta y la ficha ya abierta de Olga y de Ana la muestra con el número completo y la campana de Olga se enciende; Olga aprueba y Ana lo ve; Ana completa y el portal pasa a «Designada» con su campana; al revés, la empresa observa y el portal ya abierto pasa a «Observado» con la campana encendida, y la respuesta del tercero se ve en el expediente abierto de la empresa. Deja capturas en `test-results/tiempo-real-*.png` |
| `navegacion.spec.ts` | Barra superior con «Más» adaptativo a 1536, 1100 y 700 px (sin desplazamiento horizontal, todas las secciones a un clic, activa dentro de «Más»), engranaje de Configuración, cambio de ancho en caliente y modo oscuro; deja capturas en `test-results/navegacion-*.png` |

Los vencimientos se prueban sin mover el reloj: el proveedor declara una fecha de expedición vieja y los avisos o vencimientos que caen en el pasado se ejecutan enseguida.
