# Pruebas de punta a punta

Playwright con un navegador real contra Validasoft tal como se despliega: la API sirve la app embebible en `/app/` y la del tercero en `/`, con PostgreSQL, Mailpit (correo) y MinIO (archivos) de verdad. Cada prueba crea su propio grupo empresarial, así que corren en paralelo sin pisarse ni depender de datos previos.

```bash
docker compose -f infra/compose/compose.yaml up -d postgres mailpit minio
pnpm --filter @validasoft/e2e navegadores   # la primera vez: descarga Chromium
pnpm e2e                                     # compila las apps, publica la API en e2e/.api y la levanta en :5090
```

Con una API ya levantada que sirva las dos apps: `E2E_BASE_URL=http://localhost:5090 pnpm e2e`. Para no recompilar: `E2E_SIN_COMPILAR=1`.

En local las pruebas corren **aisladas** del entorno de desarrollo: base `validasoft_e2e` (se crea sola) y Mailpit propio (perfil `e2e` del compose, bandeja en http://localhost:8026). Así sus invitaciones y códigos no llegan al Mailpit de desarrollo, y la API de desarrollo no toma sus mensajes de la cola. Para usar la base y el Mailpit de desarrollo: `E2E_COMPARTIDO=1`. Para correr dos suites a la vez: otro `E2E_PUERTO` y otra `E2E_BASE_DATOS` (letras minúsculas y guion bajo). En CI no hace falta: el entorno ya es limpio.

Para correr **exactamente lo del CI**, en un contenedor Linux y con los servicios en una red propia (sin tocar ningún puerto de la máquina), usa la réplica local: `infra/ci-local/ci-local.sh --solo e2e` (ver `infra/ci-local/README.md`).

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
| `asistente-tipo-persona.spec.ts` | El asistente «Mi información» según el tipo de persona, hasta enviar: la natural (cédula, cuatro secciones, general con nombres, expedición y PEP, sin Legal ni Financiera) y la jurídica (Cámara de comercio, seis secciones, razón social, la Legal exige socio); el expediente pasa solo a revisión y el área ve lo que se le pidió a ese tipo |
| `multiempresa-documento-global.spec.ts` | Dos empresas validan el mismo RUT y la misma información; con una versión nueva (RUT y dirección) cada una revalida por su cuenta: una acepta la versión 2 y la otra ve su aceptación desactualizada, sin ver la decisión de la primera |
| `alta-empresa-certificacion.spec.ts` | Empresa nueva creada desde la consola de Plataforma hasta certificar a su primer proveedor: configuración mínima, invitación, carga, revisión y certificado verificable en la página pública |
| `volver-a-postularme.spec.ts` | «Volver a postularme» completo: rechazo con documentos, motivo sin la justificación interna, revisión nueva con los documentos de la bóveda, versión corregida, aprobación de las dos áreas y certificado |
| `ia-cupo-y-escalamiento.spec.ts` | IA con el proveedor simulado: cupo agotado (el documento sin análisis cae de inmediato a Excepciones con «Sin análisis de IA», la administradora ve el consumo en 100 % y el aviso por campana y correo, y el tercero no ve nada de la IA) y escalamiento de una lectura dudosa al modelo superior (confianza, «Revisado por un modelo de mayor capacidad» y modelo en el expediente; tokens y costo de las tres llamadas en «Plan y uso de IA») |
| `revalidaciones-y-exencion.spec.ts` | Aprobación en lote de las revalidaciones sin cambios (la que cambió no se selecciona) con su efecto en el certificado, la línea de tiempo del vínculo y lo que ve el tercero; y eximir un documento con su motivo (el expediente pasa a revisión, las áreas no lo revisan, el tercero lo ve eximido y el vínculo no lo comparte) |
| `cumplimiento-vigilancia.spec.ts` | Coincidencia confirmada que impide aprobar y termina en rechazo sin tocar a la otra empresa del tercero; estudio de cumplimiento vencido con el expediente abierto (aviso, bloqueo y renovación, con fecha simulada); monitoreo periódico de un vínculo activo que reconsulta solo y lleva a la bandeja del oficial la persona nueva que suma el tercero |
| `vinculo-en-revision.spec.ts` | Un tercero aprobado por dos empresas sube una versión nueva del RUT que las dos exigen: el documento y el vínculo quedan «En revisión» en las dos empresas (lista, filtro, inicio y detalle) y en el portal del tercero (encabezado de «Mis empresas» y de su revisión, y la tarjeta del documento, sin «Aprobado» arriba); el certificado queda «Con pendientes»; una aprueba y su vínculo vuelve a «Activo» mientras la otra sigue en revisión (VR-1…VR-9) |

Los vencimientos se prueban sin mover el reloj: el proveedor declara una fecha de expedición vieja y los avisos o vencimientos que caen en el pasado se ejecutan enseguida.
