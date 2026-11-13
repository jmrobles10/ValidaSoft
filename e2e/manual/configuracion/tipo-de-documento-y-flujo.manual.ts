import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE_TIPO = 'Certificado de trabajo en alturas';
const CODIGO_TIPO = 'CERT_ALTURAS';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Configura un tipo de documento y publica un flujo que lo pide',
    resumen:
      'La administradora crea un tipo de documento propio (archivo con fecha de expedición, límites y vigencia con avisos) y luego un flujo aprobatorio para proveedores que lo pide, lo publica y lo deja como el flujo por defecto.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con una persona como miembro y un flujo de Proveedor ya publicado.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'tipos-documento');
    await expect(app.getByRole('heading', { level: 1, name: 'Tipos de documento' })).toBeVisible();
    await expect(app.getByRole('row', { name: /Editar Registro Único Tributario/ })).toBeVisible();

    // 1. La lista de tipos de documento del grupo.
    await paso(page, {
      titulo: 'Abre Configuración › Tipos de documento',
      explicacion:
        'Desde el engranaje de Configuración entras a «Tipos de documento». La tabla lista, para todo el grupo, qué recibe cada documento (formato y tamaño o datos de un formulario), cuánto vale y si está activo. ' +
        'Los marcados «de la plataforma» parten de un documento conocido, como el RUT, y se comparten entre empresas. Solo quien puede escribir configuración ve el botón para crear uno nuevo.',
      resaltar: app.getByRole('button', { name: 'Nuevo tipo de documento' }),
      configuracion: { ajuste: 'Tipos de documento', ruta: 'Configuración › Tipos de documento (/tipos-documento)', efecto: 'Es el catálogo de documentos que se pueden pedir en los flujos.' },
    });

    // 2. Identificación.
    await app.getByRole('button', { name: 'Nuevo tipo de documento' }).click();
    const panel = app.locator('[aria-labelledby="titulo-tipo-documento"]');
    await expect(panel).toBeVisible();
    await panel.getByRole('textbox', { name: /^Código/ }).fill(CODIGO_TIPO);
    await panel.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE_TIPO);
    await paso(page, {
      titulo: 'Identifica el documento',
      explicacion:
        'Un panel lateral recoge la configuración. Elige «Ninguno: es un documento propio» o parte de un documento de la plataforma (RUT, Cámara de comercio…) cuyas reglas no puedes relajar. ' +
        'Escribe un código en mayúsculas (no cambia después) y el nombre que verá el proveedor. El ámbito decide si el documento queda en la bóveda del proveedor y se reutiliza con otras empresas (Compartido) o es solo de esta relación.',
      resaltar: [panel.getByRole('textbox', { name: /^Código/ }), panel.getByRole('textbox', { name: /^Nombre/ })],
      variantes: [
        { cuando: 'Partes de un documento de la plataforma', entonces: 'El ámbito, el formato y los límites quedan acotados por la plataforma y se comparte la bóveda entre empresas.' },
        { cuando: 'Es un documento propio', entonces: 'Defines tú el ámbito, el archivo y la vigencia; si lleva formulario, siempre es «Solo de este vínculo».' },
      ],
    });

    // 3. Cómo lo entrega el proveedor.
    await panel.getByRole('combobox', { name: 'Se entrega como' }).click();
    await app.getByRole('option', { name: 'Archivo y formulario', exact: true }).click();
    await panel.getByRole('button', { name: 'Agregar fecha de expedición' }).click();
    await expect(panel.getByRole('list', { name: 'Campos del formulario' })).toBeVisible();
    await paso(page, {
      titulo: 'Elige cómo lo entrega el proveedor',
      explicacion:
        'El documento puede ser solo un archivo, solo un formulario con datos o las dos cosas en la misma entrega. Con formulario aparece la lista de campos que llenará el proveedor (texto, número, fecha, lista o Sí/No), cada uno con su etiqueta, su clave y si es obligatorio. ' +
        'El botón «Agregar fecha de expedición» crea el campo reservado fecha_expedicion, que es la base para calcular la vigencia.',
      resaltar: [panel.getByRole('combobox', { name: 'Se entrega como' }), panel.getByRole('list', { name: 'Campos del formulario' })],
      variantes: [
        { cuando: 'Se entrega como «Archivo»', entonces: 'El proveedor solo sube el archivo; no hay campos.' },
        { cuando: 'Se entrega como «Formulario»', entonces: 'El proveedor llena los datos y no sube archivo; no se piden formatos ni peso.' },
      ],
    });

    // 4. Límites del archivo.
    const archivo = panel.locator('section').filter({ has: app.getByRole('heading', { name: 'Archivo', exact: true }) });
    await panel.getByRole('checkbox', { name: /PDF/ }).check();
    await panel.getByRole('textbox', { name: 'Peso máximo (MB)' }).fill('5');
    await panel.getByRole('textbox', { name: 'Páginas máximas' }).fill('10');
    await paso(page, {
      titulo: 'Limita el archivo que se puede subir',
      explicacion:
        'Marca los formatos permitidos (ninguno marcado equivale a PDF, JPG, PNG o WebP) y fija el peso máximo en MB y las páginas máximas. Vacío significa el límite general de la plataforma. ' +
        'Lo que escribas es lo que le dirá la pantalla de carga al proveedor, por ejemplo «Solo PDF de hasta 5 MB y 10 páginas».',
      resaltar: archivo,
      variantes: [{ cuando: 'El tipo parte de un documento de la plataforma', entonces: 'Los formatos y límites no pueden pasar del techo que fija la plataforma; el panel lo indica debajo de cada campo.' }],
    });

    // 5. Vigencia y avisos.
    await panel.getByRole('combobox', { name: 'Vale' }).click();
    await app.getByRole('option', { name: 'Días desde su expedición', exact: true }).click();
    await panel.getByRole('textbox', { name: /^Días$/ }).fill('365');
    await panel.getByRole('textbox', { name: 'Avisar antes de vencer (días)' }).fill('30, 15');
    await panel.getByRole('textbox', { name: 'Días de gracia' }).fill('10');
    await paso(page, {
      titulo: 'Define la vigencia y los avisos',
      explicacion:
        'Indica cuánto tiempo sirve el documento: siempre, un número de días desde su expedición (la fecha que declara el proveedor), un número de días desde que lo entrega o un periodo mínimo. ' +
        'Los días de aviso son los momentos antes del vencimiento en que Validasoft le escribe al proveedor para que lo renueve; los días de gracia son los que sigue valiendo después de vencer mientras lo renueva.',
      resaltar: panel.locator('section').filter({ has: app.getByRole('heading', { name: 'Vigencia y avisos', exact: true }) }),
      variantes: [
        { cuando: 'La regla es «Siempre (no vence)»', entonces: 'No hay días ni avisos: el documento no pierde vigencia.' },
        { cuando: 'La regla es «Días desde que se entrega»', entonces: 'La vigencia se cuenta desde el día en que el proveedor lo entrega, sin pedir fecha de expedición.' },
      ],
      configuracion: { ajuste: 'Avisos de vencimiento y plantillas de correo de vencimiento', ruta: 'Configuración › Plantillas de correo (/plantillas)', efecto: 'Los avisos que se envían usan estas plantillas.' },
    });

    // 6. Crear.
    await panel.getByRole('button', { name: 'Crear tipo' }).click();
    await expect(app.getByText(`Tipo «${NOMBRE_TIPO}» creado.`)).toBeVisible();
    await expect(app.getByRole('row', { name: `Editar ${NOMBRE_TIPO}` })).toBeVisible();
    await paso(page, {
      titulo: 'Crea el tipo y revísalo en la tabla',
      explicacion:
        'Al pulsar «Crear tipo» el panel se cierra, aparece el aviso de que quedó creado y el nuevo tipo entra a la tabla con su resumen: qué recibe, cuánto vale y que está activo. Mientras falte un dato obligatorio, el botón está deshabilitado y dice qué falta. ' +
        'Para cambiarlo después, haz clic en su fila; un tipo inactivo deja de poder pedirse en los flujos nuevos.',
      resaltar: app.getByRole('row', { name: `Editar ${NOMBRE_TIPO}` }),
      avisos: true,
    });

    // 7. Flujos aprobatorios.
    await app.getByRole('navigation', { name: 'Configuración' }).getByRole('link', { name: 'Flujos aprobatorios' }).click();
    await expect(app.getByRole('heading', { level: 1, name: `Flujos aprobatorios de ${e.sur.nombre}` })).toBeVisible();
    await expect(app.getByRole('row', { name: /Proveedores de Sur/ })).toBeVisible();
    await paso(page, {
      titulo: 'Abre Configuración › Flujos aprobatorios',
      explicacion:
        'Un flujo define qué áreas revisan a cada tipo de vínculo, en qué orden y qué documentos se piden. La tabla muestra cada flujo con su tipo de vínculo, su versión publicada, si hay un borrador y cuántas etapas tiene; ' +
        'el que usa la empresa cuando invita a un tercero de ese tipo lleva la marca «Por defecto». Sin un flujo publicado, los terceros que aceptan la invitación quedan en «aperturas pendientes».',
      resaltar: app.getByRole('button', { name: 'Nuevo flujo' }),
      configuracion: { ajuste: 'Flujos aprobatorios', ruta: 'Configuración › Flujos aprobatorios (/flujos)', efecto: 'Cada empresa del grupo tiene sus propios flujos.' },
    });

    // 8. Datos generales del flujo.
    await app.getByRole('button', { name: 'Nuevo flujo' }).click();
    await expect(app.getByRole('heading', { level: 1, name: 'Nuevo flujo' })).toBeVisible();
    await app.getByRole('textbox', { name: /^Código/ }).fill('PROV_ALTURAS');
    await app.getByRole('textbox', { name: /^Nombre/ }).fill('Proveedores de trabajo en alturas');
    await app.getByRole('combobox', { name: /^Tipo de vínculo/ }).click();
    await app.getByRole('option', { name: 'Proveedor', exact: true }).click();
    await paso(page, {
      titulo: 'Escribe los datos generales del flujo',
      explicacion:
        'Un flujo nuevo empieza con su código (mayúsculas, números y guion bajo; no cambia después), su nombre y el tipo de vínculo al que aplica (tampoco cambia después). ' +
        'Más abajo se configura qué pasa cuando un área observa, la separación de funciones, el plazo de cargue, los recordatorios, el reintento tras un rechazo y la vigencia periódica del vínculo.',
      resaltar: [app.getByRole('textbox', { name: /^Código/ }), app.getByRole('textbox', { name: /^Nombre/ }), app.getByRole('combobox', { name: /^Tipo de vínculo/ })],
      variantes: [
        { cuando: 'Si un área observa: «Devolver de inmediato»', entonces: 'La primera observación devuelve el expediente al tercero y frena a las demás áreas.' },
        { cuando: 'Si un área observa: «Esperar a que la etapa se pronuncie»', entonces: 'Las demás áreas siguen revisando y el expediente se devuelve cuando todas se pronunciaron.' },
        { cuando: 'Activas la separación de funciones', entonces: 'Quien invitó al tercero o abrió el expediente no puede decidir sobre él.' },
      ],
    });

    // 9. Etapas y áreas.
    await app.getByRole('region', { name: 'Etapa 1' }).getByRole('combobox', { name: 'Área' }).click();
    await app.getByRole('option', { name: /^Revisión/ }).click();
    await paso(page, {
      titulo: 'Elige las áreas que revisan en cada etapa',
      explicacion:
        'Las etapas se revisan en secuencia; las áreas de una misma etapa revisan al mismo tiempo y la etapa siguiente empieza cuando todas aprueban. Para cada área eliges cuántos aprobadores distintos necesita (de 1 a 10), un SLA opcional en horas, ' +
        'si puede rechazar el expediente y si es automática (aprueba sola al empezar la etapa). Bajo el área ves cuántos miembros tiene: si son menos que los aprobadores pedidos, el editor lo marca porque la etapa nunca se completaría.',
      resaltar: app.getByRole('region', { name: 'Etapa 1' }),
      variantes: [
        { cuando: 'Agregas otra área a la misma etapa', entonces: 'Las dos revisan en paralelo y la etapa termina cuando ambas aprueban.' },
        { cuando: 'Agregas otra etapa', entonces: 'Sus áreas solo ven el expediente cuando la etapa anterior terminó.' },
      ],
      configuracion: { ajuste: 'Áreas de la empresa y sus miembros', ruta: 'Configuración › Áreas (/areas)', efecto: 'Solo se pueden elegir áreas activas; quién decide en cada una sale de sus miembros.' },
    });

    // 10. Documentos que pide.
    await app.getByRole('button', { name: 'Agregar documento' }).click();
    await app.getByRole('combobox', { name: 'Tipo de documento' }).click();
    await app.getByRole('option', { name: new RegExp(NOMBRE_TIPO) }).click();
    await app.getByRole('combobox', { name: 'Lo revisan' }).click();
    await app.getByRole('option', { name: /^Revisión/ }).click();
    await page.keyboard.press('Escape');
    await paso(page, {
      titulo: 'Pide el documento nuevo en el flujo',
      explicacion:
        '«Documentos que pide» lista lo que el tercero debe cargar. Al agregar uno eliges su tipo (el «Certificado de trabajo en alturas» recién creado ya aparece, con su resumen), qué áreas lo revisan, a quién se le pide, ' +
        'cuántos aprobadores necesita y si es obligatorio. Toda área debe revisar al menos un documento; sin documentos, cada área aprueba u observa el expediente completo.',
      resaltar: app.getByRole('combobox', { name: 'Tipo de documento' }),
      variantes: [
        { cuando: 'Se pide a «Solo personas jurídicas» o «Solo personas naturales»', entonces: 'El documento solo aparece en la lista del tercero de ese tipo de persona.' },
        { cuando: 'Agregas una sección de la información', entonces: 'El tercero la diligencia en «Mi información» y la empresa la revisa como un requisito más.' },
      ],
      configuracion: { ajuste: 'Tipos de documento', ruta: 'Configuración › Tipos de documento (/tipos-documento)', efecto: 'Solo se pueden pedir tipos activos que no sean solo formulario.' },
    });

    // 11. Crear el borrador.
    await app.getByRole('button', { name: 'Crear borrador' }).click();
    await expect(app.getByRole('button', { name: 'Publicar v1' })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda el flujo como borrador',
      explicacion:
        'Con «Crear borrador» el flujo queda guardado como borrador v1: todavía no lo usa ningún expediente. Si falta algo, el editor marca los campos con error y dice qué corregir. ' +
        'Mientras haya cambios sin guardar no puedes publicar; cada cambio que guardes sobre una versión ya publicada crea un borrador nuevo y la versión vigente sigue funcionando hasta que publiques.',
      resaltar: app.getByRole('button', { name: 'Publicar v1' }),
      avisos: true,
    });

    // 12. Publicar.
    await app.getByRole('button', { name: 'Publicar v1' }).click();
    const confirmacion = app.getByRole('dialog', { name: /^Publicar v1 de/ });
    await expect(confirmacion).toBeVisible();
    await paso(page, {
      titulo: 'Publica la versión',
      explicacion:
        'Antes de publicar, Validasoft te avisa qué va a pasar: los expedientes nuevos usarán esta versión, los que ya están abiertos siguen con la suya y la versión publicada anterior queda retirada. ' +
        'Al confirmar, la versión queda congelada y se registra en la auditoría de configuración.',
      resaltar: confirmacion,
    });
    await confirmacion.getByRole('button', { name: 'Publicar v1' }).click();
    await expect(app.getByText('Versión 1 publicada.')).toBeVisible();

    // 13. Dejarlo por defecto.
    await expect(app.getByRole('button', { name: 'Usar por defecto para Proveedor' })).toBeVisible();
    await paso(page, {
      titulo: 'Usa el flujo por defecto para el tipo de vínculo',
      explicacion:
        'Publicar no cambia a qué flujo va cada invitación: eso lo decide el flujo por defecto de cada tipo de vínculo. Mientras no marques este, las invitaciones a proveedores siguen usando el anterior. ' +
        '«Usar por defecto para Proveedor» solo aparece en flujos publicados del mismo tipo.',
      resaltar: app.getByRole('button', { name: 'Usar por defecto para Proveedor' }),
      avisos: true,
    });
    await app.getByRole('button', { name: 'Usar por defecto para Proveedor' }).click();
    await expect(app.getByText('Ahora es el flujo por defecto de Proveedor.')).toBeVisible();
    await app.getByRole('link', { name: 'Flujos aprobatorios' }).first().click();
    await expect(app.getByRole('row', { name: /Abrir flujo Proveedores de trabajo en alturas/ })).toContainText('Por defecto');
    await paso(page, {
      titulo: 'Comprueba el resultado en la lista de flujos',
      explicacion:
        'La lista muestra el flujo nuevo con su versión publicada (v1) y la marca «Por defecto». Desde ahora, cuando el sistema integrado invite a un proveedor de Constructora Sur, el tercero recibirá una revisión con el documento nuevo; ' +
        'las revisiones ya abiertas no cambian. Para modificar el flujo se abre su fila y se guarda un borrador nuevo.',
      resaltar: app.getByRole('row', { name: /Abrir flujo Proveedores de trabajo en alturas/ }),
      variantes: [{ cuando: 'La empresa no tiene un flujo por defecto para el tipo de vínculo', entonces: 'La lista de «Flujo por defecto por tipo de vínculo» dice «Sin flujo por defecto» y los terceros que aceptan quedan en aperturas pendientes.' }],
    });
  },
);
