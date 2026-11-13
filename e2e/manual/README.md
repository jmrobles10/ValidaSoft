# Manual de Validasoft generado por recorridos

El manual **no se escribe a mano**. Cada flujo es un **recorrido**: una prueba de Playwright (`e2e/manual/<dominio>/<flujo>.manual.ts`) que ejecuta
el flujo real en la interfaz y, en cada momento que merece explicación, llama a `await paso(page, {…})`. `paso` toma el pantallazo (claro 1280 × 800
y, si corresponde, celular 390 × 844), resalta lo que se usa y anota el paso en un manifiesto. Un generador convierte esos manifiestos en:

- un **sitio HTML** navegable (`salida/manual/index.html`: índice por actor y dominio, buscador, claro/oscuro, celular, zoom de imágenes);
- **Markdown** por recorrido en `docs/manual/<dominio>/` (texto con referencias a las imágenes; las imágenes no van a git);
- la **referencia de la API** (sección «API» del sitio y `docs/manual/api/`), generada del contrato OpenAPI sin intervención tuya.

Si el producto cambia y el recorrido falla, el manual está desactualizado: eso es lo que se quiere.

La lista de lo que hay que documentar está en [`INVENTARIO.md`](INVENTARIO.md). **Tú solo escribes recorridos** (y, si encuentras un defecto real del producto,
lo anotas en tu informe: no toques el código del producto).

## Comandos

```bash
# Una vez por máquina: navegador, y los servicios de apoyo (PostgreSQL, Mailpit y MinIO)
pnpm --filter @validasoft/e2e navegadores
docker compose -f infra/compose/compose.yaml up -d postgres minio          # el Mailpit de los recorridos lo levanta el propio arranque

# Correr recorridos (deja manifiestos e imágenes en salida/manual-recorridos/)
E2E_PUERTO=<tu puerto> E2E_BASE_DATOS=<tu_base> pnpm manual:recorrer                  # todos
E2E_PUERTO=<tu puerto> E2E_BASE_DATOS=<tu_base> pnpm manual:recorrer --grep tercero   # los de un dominio (el título lleva «[dominio] …»)
E2E_PUERTO=<tu puerto> E2E_BASE_DATOS=<tu_base> pnpm manual:recorrer tercero/aceptar  # por archivo (parte del nombre)

pnpm manual          # genera salida/manual/ (HTML) y docs/manual/ (Markdown) con lo que haya en salida/manual-recorridos/
pnpm manual:todo     # recorrer + generar
```

- **Tu puerto y tu base** (`E2E_PUERTO`, `E2E_BASE_DATOS`, letras minúsculas y guion bajo) son obligatorios si hay otras personas corriendo pruebas: no uses la
  base `validasoft`, el puerto 5080 ni el Mailpit 8025. La primera corrida compila y publica la API (varios minutos); `E2E_SIN_COMPILAR=1` salta la compilación
  si no cambiaste código del producto.
- **Para iterar rápido**, deja la API corriendo aparte y corre los recorridos con las mismas variables (Playwright la reutiliza):
  `cd e2e && E2E_AISLADO=1 E2E_PUERTO=<p> E2E_BASE_DATOS=<b> node soporte/api.mjs` (en otra terminal).
- Un recorrido que termina bien reescribe su carpeta `salida/manual-recorridos/<dominio>/<flujo>/`. Si falla, no deja manifiesto y el generador lo omite.
- Para ver por qué falló: la terminal muestra el paso y el locator; `e2e/test-results-manual/<prueba>/test-failed-1.png` es el pantallazo del fallo
  (`error-context.md` trae el árbol accesible).
- `--repeat-each=3` (de Playwright) repite un recorrido para comprobar que no es frágil: `pnpm manual:recorrer --grep tercero --repeat-each=3`.

## La plantilla (copia esto y cámbialo)

Un archivo = un recorrido. El nombre es `<flujo>.manual.ts` (minúsculas y guiones) y vive en la carpeta de su **dominio** (`e2e/manual/<dominio>/`; los
dominios y actores válidos están en [`catalogo.json`](catalogo.json) y abajo).

```ts
// e2e/manual/revision/aprobar-un-area.manual.ts
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja } from '../../soporte/empresa';
import { pdf } from '../../soporte/archivos';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';        // siempre desde '../nucleo'

recorrido(
  {
    dominio: 'revision',                       // = la carpeta del archivo
    actor: 'Revisor/área',                     // uno de los siete actores
    titulo: 'Aprueba el área de Compras',      // imperativo corto; es el título del manual
    resumen: 'Rubén abre el expediente que le llegó a su bandeja, revisa los documentos y aprueba su área.', // 1–3 frases
    precondiciones: [                           // lo que se siembra por API
      'Un grupo con Constructora Norte, su flujo de Proveedor y Rubén como miembro del área Compras.',
      'Un proveedor que ya entregó sus documentos y quedó «En revisión».',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica'],   // otros recorridos: 'dominio/archivo'
  },
  async ({ page, browser }) => {
    // 1) SEMBRAR por API lo que no es objeto del recorrido (helpers de e2e/soporte). Aquí nada se documenta.
    const e = await crearEscenario();                          // grupo nuevo: Norte, Sur, Ana (admin), Rubén, Olga, Sofía
    const proveedor = nuevoTercero('Aceros del Norte S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara') },
    ]);

    // 2) LO QUE SE DOCUMENTA se hace por la interfaz. Las personas de la empresa usan la app dentro del iframe del sistema integrado:
    //    `app` es un FrameLocator; el pantallazo se toma sobre `page` (la página anfitriona) y `paso` recorta el iframe.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();   // espera el resultado ANTES del paso

    await paso(page, {
      titulo: 'Abre tu bandeja de revisión',                  // imperativo corto
      explicacion:                                            // 2–5 frases: qué ve, qué hace, qué pasa después, reglas
        'La bandeja lista los expedientes que esperan decisión de tu área, con el tercero, el tipo de vínculo y el estado. ' +
        'Ves solo lo de las áreas de las que eres miembro. Haz clic en una fila para abrir el expediente.',
      resaltar: app.getByRole('table'),                       // qué se usa en este paso (un locator o una lista: salen numerados 1, 2…)
      variantes: [                                            // qué cambia según tipo de persona, configuración, rol, plan, nivel de IA…
        { cuando: 'Eres administrador y no miembro del área', entonces: 'Ves las acciones de administrador y puedes aprobar en nombre del área.' },
      ],
      configuracion: {                                        // qué pantalla/ajuste controla esto, con su ruta en la app
        ajuste: 'Miembros del área',
        ruta: 'Configuración › Áreas (/areas)',
        efecto: 'Quién es miembro decide qué expedientes aparecen aquí.',
      },
    });

    await abrirDesdeBandeja(app, proveedor.nombre, 'Compras');
    await paso(page, {
      titulo: 'Revisa el expediente',
      explicacion: 'El detalle muestra el estado de cada área, los documentos con su versión y lo que ya decidieron las demás áreas.',
      resaltar: [app.getByRole('region', { name: 'Compras', exact: true }).first(), app.getByRole('heading', { level: 1, name: proveedor.nombre })],
    });
  },
);
```

Notas de la plantilla:

- `paso(page, …)` recibe **la página** (`page`), no el `FrameLocator`. Los locators de `resaltar` sí pueden salir de `app`.
- El **primer** `paso` debe llamarse cuando la pantalla ya muestra lo que quieres explicar; no hace falta esperar a que desaparezcan skeletons: `paso` lo espera.
- Termina el recorrido cuando el flujo termina. No hay `await paso(…)` final «de cierre»: el último paso es el último resultado visible.

## Qué hace `paso` por ti

| Opción | Para qué | Por omisión |
|---|---|---|
| `titulo` | Imperativo corto («Invita a un proveedor») | obligatorio |
| `explicacion` | 2–5 frases en español claro para una persona de negocio (60 a 1400 caracteres). Admite `**negrita**`, `` `código` `` y `[[dominio/archivo]]` (enlace a otro recorrido) y párrafos con línea en blanco | obligatorio |
| `resaltar` | Un `Locator` o una lista. Dibuja un recuadro naranja (solo durante la captura) y, con varios, los numera 1, 2…: **cita esos números en la explicación** | sin resaltar |
| `variantes` | `{ cuando, entonces }[]` (o `'Cuando X: Y'`): qué cambia según tipo de persona, configuración, rol, plan, nivel de IA | — |
| `configuracion` | `{ ajuste, ruta?, efecto? }` (uno o una lista): qué ajuste controla esto y dónde se cambia (`'Configuración › Flujos aprobatorios (/flujos)'`) | — |
| `celular` | Captura también 390 × 844 de la **misma página** (sin perder la sesión ni lo escrito) | `true` si el actor es Tercero o Público; `false` en los demás |
| `completa` | Toda la página en vez de la ventana (para pantallas largas; no lo uses con diálogos ni paneles) | `false` |
| `avisos` | Deja visibles los avisos emergentes («Guardaste…»). Por omisión se ocultan: tapan la pantalla y desaparecen solos | `false` |
| `esperarCarga` | `false` si el paso muestra a propósito un skeleton o spinner | `true` |
| `mantenerPuntero` | No mueve el puntero a una esquina (para mostrar un tooltip o un menú que se abre con el puntero) | `false` |

Lo que `paso` hace solo: espera a que no haya skeletons, spinners ni regiones ocupadas; parte de lo alto de la página y baja solo si lo resaltado no se ve; recorta el
iframe de la empresa (sin la barra de la página anfitriona); lleva el puntero a una esquina (sin estados «hover»); y falla con un mensaje claro si algún locator de `resaltar`
no se ve, si la explicación es muy corta/larga o si el dominio, actor o nombre de archivo no son válidos.

## Reglas para que el manual sea verdadero y claro

1. **No inventes comportamiento.** Si dudas, lee el código (`apps/web/src`, `apps/public/src`, los textos en `i18n/es-CO*.ts`), `docs/16-estado-y-pendientes.md` y
   `docs/decisiones/registro.md`. Lo que afirmes en `explicacion` o `variantes` y **puedas comprobar en la pantalla, compruébalo con un `expect`** antes del `paso`
   (si es falso, el recorrido falla y te enteras).
2. **Siembra por API** todo lo que no es objeto del recorrido (grupo, empresas, flujos, invitaciones, documentos de otros pasos) con `e2e/soporte/*` y
   `e2e/manual/nucleo/semillas.ts`; **lo que se documenta se hace por la interfaz**, con clics y texto, como lo haría la persona.
3. **Locators**: `getByRole` primero; `getByText` corto siempre con `exact: true` o con una regex anclada. **Nada de esperas fijas**: espera un `expect(...).toBeVisible()` o el
   helper que ya lo hace (`esperarEstadoDelTercero`, `esperarCorreo`…).
4. **Un paso por idea**: título imperativo, explicación de 2 a 5 frases, qué ve, qué hace, qué pasa después y qué reglas aplican. Resalta lo que se usa. Añade `variantes`
   cuando el comportamiento cambia (tipo de persona, configuración, rol, plan, nivel de IA) y `configuracion` cuando un ajuste lo controla.
5. **Pantallazos**: claro y 1280 × 800; celular (390 × 844) en lo del tercero (automático). En la app de la empresa el celular es opcional (`celular: true`).
6. **Un archivo, un recorrido**; de 6 a 18 pasos suele ser lo justo. Si pasa de 20, pártelo en dos recorridos y enlázalos con `relacionados`.
7. **Commits pequeños por recorrido** (mensaje en español) y lint al día: `pnpm exec eslint e2e` y `pnpm --filter @validasoft/e2e typecheck`.
8. **Si un recorrido falla dos veces seguidas por algo que no entiendes, detente** y repórtalo (lo diagnostica otra persona). No toques código del producto.

- Los índices `docs/manual/README.md` y `docs/manual/<dominio>/README.md` los escribe el generador a partir de lo que hay en `docs/manual/`: si dos ramas chocan en ellos, vuelve a correr `pnpm manual` y quédate con el resultado.

## Dónde está cada cosa

| Quiero… | Uso |
|---|---|
| Crear el grupo, empresas, personas, áreas y flujos de base | `crearEscenario()` (`soporte/semilla.ts`): Constructora Norte (Compras y Cumplimiento) y Sur (Revisión), Ana (administradora), Rubén, Olga (oficial), Sofía |
| Un flujo propio con documentos y secciones de la ficha, como flujo por defecto | `publicarFlujoPorDefecto(...)`, `escenarioConFichaCompleta()` (`manual/nucleo/semillas.ts`) y `publicarFlujoDeFicha` (`soporte/ficha.ts`); otros: `soporte/flujos.ts` |
| Invitar a un tercero | `invitar(e, empresa, tercero, tipo)`; terceros: `nuevoTercero(nombre)` (jurídica), `nuevaPersonaNatural(nombre)` |
| Que el tercero entre, acepte y suba documentos | `soporte/proveedor.ts`: `entrarPorInvitacion`, `aceptarInvitacion`, `subirDocumento`, `completarInvitacion`, `esperarEstadoDelTercero`, `responder` |
| Llenar «Mi información» del tercero | `soporte/ficha.ts`: `llenarGeneralJuridica`, `llenarGeneralNatural`, `llenarTributaria`, `llenarFinanciera`, `llenarSst`, `llenarEspecialidades`, `agregarPersona` |
| Leer correos (invitación, código, avisos) | `soporte/correo.ts`: `esperarCorreo`, `enlace`, `codigo` (y `page.setContent(correo.html)` para mostrar el correo) |
| Abrir la app de la empresa como una persona | `abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja')` (`soporte/embebido.ts`); ir a otra sección: `irASeccion(app, 'Vínculos')`; consola de Plataforma: `soporte/plataforma.ts` |
| Decidir un expediente | `soporte/empresa.ts`: `abrirDesdeBandeja`, `aprobarArea`, `observar`, `rechazar`, `aprobarEnNombreDe` |
| Cumplimiento, integración, certificados… | `soporte/cumplimiento.ts`, `soporte/receptor.ts`, `soporte/devolucion.ts`, `soporte/derechos.ts`, `soporte/declaracion-pep.ts`; lee antes una spec parecida de `e2e/pruebas/` |
| Archivos de prueba | `pdf('texto')` (`soporte/archivos.ts`) |
| Mostrar una llamada de API (Sistema integrador) | `mostrarCodigo(page, { titulo, peticion, cuerpo, respuesta })` (`manual/nucleo/codigo.ts`): dibuja la petición y la respuesta reales de un `llamar(...)` para usarlas con `paso`; nunca muestres llaves reales |

Actores: `Plataforma`, `Administrador de empresa`, `Revisor/área`, `Oficial de cumplimiento`, `Tercero`, `Sistema integrador`, `Público`.

Dominios (carpeta del archivo): `acceso`, `tercero`, `vinculos`, `revision`, `documentos`, `configuracion`, `cumplimiento`, `informes`, `operacion`, `ia`, `privacidad`,
`plataforma`, `integracion`, `embebido`, `publico`, `movil`. Para agregar uno nuevo, se edita `catalogo.json` (nombre y descripción).

## Trampas conocidas (ya pisadas)

- **Paneles laterales (Drawer de MUI)**: `getByRole('dialog', { name })` no los encuentra. Usa `app.locator('[aria-labelledby="titulo-tipo-documento"]')` (el id del título del panel)
  o el encabezado. Los **diálogos** sí: `app.getByRole('dialog', { name: /^Publicar v1 de/ })`.
- **`filter({ has: … })`**: el locator de `has` se resuelve **desde el elemento filtrado** y debe nacer del mismo `app`/`page`: `panel.locator('section').filter({ has: app.getByRole('heading', { name: 'Archivo' }) })`.
- **Teclado**: `page.keyboard.press('Escape')` (el `FrameLocator` no tiene `keyboard`).
- **Selects de MUI**: abre con `getByRole('combobox', { name })`, elige con `app.getByRole('option', { name })` (las opciones salen en un portal fuera del panel).
- **Espera el resultado antes del `paso`**: si el paso explica «Lista: puedes continuar» o un aviso, haz `await expect(...).toBeVisible()` de eso antes; si no, el pantallazo puede mostrar el estado anterior.
- **El documento sube por una cola**: tras `subirDocumento`, usa `esperarEstadoDelTercero` o `esperarTarjeta` antes de mostrar el resultado en «Tu revisión con …».
- **Correo**: cada recorrido crea su tercero con correo único; no vacíes Mailpit. Para mostrar un correo: `page.setContent(...)` con su `html`, como en `tercero/aceptar-invitacion-persona-juridica.manual.ts`.
- **Celular que pierde lo escrito**: el pantallazo celular cambia el tamaño de la ventana de la misma página; si una pantalla se reconstruye con otro ancho y pierde lo escrito, pon `celular: false`
  en ese paso y dilo en la explicación.
- **Fechas**: usa `escribirFecha` (`soporte/fechas.ts`); los controles son de `@validasoft/ui`, no `<input type="date">`.

## Modelos para copiar

- Tercero, con celular y flujo largo: [`tercero/aceptar-invitacion-persona-juridica.manual.ts`](tercero/aceptar-invitacion-persona-juridica.manual.ts) (correo, código, textos legales, ficha completa, documentos).
- Administrador, en la app de la empresa: [`configuracion/tipo-de-documento-y-flujo.manual.ts`](configuracion/tipo-de-documento-y-flujo.manual.ts) (panel lateral, formulario largo, diálogo de confirmación, avisos).

## Cómo funciona por dentro (por si algo no cuadra)

- `playwright.manual.config.ts` reutiliza el arranque de `playwright.config.ts` (API compilada que sirve las dos apps, PostgreSQL, Mailpit, MinIO) y solo cambia qué corre
  (`manual/**/*.manual.ts`). No hace parte de `pnpm e2e` ni del CI.
- `nucleo/manual.ts` (`recorrido`, `paso`) escribe `salida/manual-recorridos/<dominio>/<flujo>/manifiesto.json` y los PNG; `nucleo/captura.ts` hace la espera, el resaltado y el recorte.
- `generador/` convierte los manifiestos en el sitio y el Markdown; `generador/api*.mjs` genera la referencia de la API de `packages/api-client/openapi/v1.json` y del código de los
  endpoints (permisos y descripciones). Pruebas en `generador/generador.test.mjs` y `nucleo/validacion.test.ts` (corren con `pnpm test`).
