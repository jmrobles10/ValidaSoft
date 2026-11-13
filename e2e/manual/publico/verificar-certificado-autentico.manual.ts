import { expect, paso, recorrido } from '../nucleo';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'publico',
    actor: 'Público',
    titulo: 'Verifica que un certificado es auténtico',
    resumen:
      'Cualquier persona con el enlace del código QR o el folio de un certificado vigente lo consulta sin iniciar sesión: ve quién lo emite, a quién certifica y si la firma de Validasoft corresponde a su contenido, y descarga el PDF.',
    precondiciones: [
      'Un proveedor aprobado por Constructora Sur (una sola área) que queda con su certificado vigente.',
      'La consulta se hace sin sesión, desde una ventana nueva del navegador.',
    ],
    relacionados: ['publico/certificado-en-otros-estados', 'publico/verificar-con-el-pdf'],
  },
  async ({ page, browser }) => {
    // Siembra: el proveedor entrega sus documentos y Sofía (Revisión de Sur) lo aprueba.
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.sur, nuevoTercero('Andamios Seguros S.A.S.'));
    const hace2Dias = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - 2 * 86_400_000));
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
      { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': hace2Dias } },
    ]);
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // El enlace de verificación lo tiene la administradora en el vínculo; aquí se toma tal como lo recibe el público.
    const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
    await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    const enlace = await ana.getByRole('link', { name: 'Página de verificación' }).getAttribute('href');
    const publica = await (await browser.newContext()).newPage();
    const url = new URL(enlace ?? '');
    await publica.goto(`${url.pathname}${url.search}`);

    // 1. Estado y datos.
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();
    await expect(publica.getByRole('main')).toContainText(`${e.sur.nombre} validó los documentos de ${invitacion.nombre}`);
    await paso(publica, {
      titulo: 'Lee el estado del certificado',
      explicacion:
        'La página abre con el estado: «Este certificado está vigente» y la empresa que validó al proveedor. Debajo están los datos del certificado: la empresa que lo emite, el titular, el folio, la fecha de emisión y hasta cuándo vale. ' +
        'El NIT aparece enmascarado y el correo del titular no se muestra; la consulta no te da datos que no necesitas para confirmar el estado.',
      resaltar: publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' }),
      celular: false,
      variantes: [
        { cuando: 'El certificado tiene fecha de vencimiento', entonces: 'Aparece «Vence el» con la fecha; si no tiene, dice que no tiene fecha de vencimiento.' },
        { cuando: 'El certificado ya venció', entonces: 'El estado cambia a «Este certificado venció» y el PDF ya no se entrega.' },
      ],
    });

    // 2. Autenticidad en el navegador.
    await expect(publica.getByRole('status').filter({ hasText: 'Auténtico: el contenido está íntegro y lo selló Validasoft.' })).toBeVisible();
    for (const comprobacion of ['Contenido íntegro', 'Huella del enlace', 'Clave de la plataforma', 'Sello válido'])
      await expect(publica.getByRole('listitem').filter({ hasText: comprobacion })).toContainText('Correcto');
    await paso(publica, {
      titulo: 'Comprueba la autenticidad',
      explicacion:
        'La sección «Autenticidad» la comprueba tu propio navegador: recalcula el contenido sellado, compara su huella con la del enlace y valida la firma con la clave pública de Validasoft. ' +
        'Si las cuatro comprobaciones salen en «Correcto», el mensaje dice «Auténtico»; si alguna falla, el certificado no debe aceptarse.',
      resaltar: [publica.getByRole('status').filter({ hasText: 'Auténtico' }), publica.getByRole('list').filter({ hasText: 'Sello válido' })],
      variantes: [
        { cuando: 'Tu navegador no soporta la verificación', entonces: 'La sección dice que no puede verificar el sello y te pide abrirlo en una versión reciente de Chrome, Edge, Firefox o Safari.' },
        { cuando: 'El contenido fue alterado', entonces: 'El mensaje dice «No confíes en este certificado: la verificación falló».' },
      ],
    });

    // 3. Descargar el PDF.
    const descarga = publica.waitForEvent('download');
    await publica.getByRole('button', { name: 'Descargar PDF' }).click();
    expect((await (await descarga).path()) !== '').toBe(true);
    await paso(publica, {
      titulo: 'Descarga el PDF del certificado',
      explicacion:
        'El botón «Descargar PDF» entrega el certificado tal como se emitió. Solo se entrega mientras el certificado está vigente: si está suspendido o revocado, el botón no baja el archivo y la página lo explica.',
      resaltar: publica.getByRole('button', { name: 'Descargar PDF' }),
      celular: false,
      avisos: true,
    });
  },
);
