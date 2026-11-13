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
    titulo: 'Compara la huella impresa en el PDF con la del certificado',
    resumen:
      'Quien recibe el PDF del certificado escanea su código QR. Si el enlace no trae la huella completa, la página verifica el contenido y pide comparar la huella que muestra con la impresa en el PDF; las dos deben coincidir para confiar en el documento.',
    precondiciones: [
      'Un proveedor aprobado por Constructora Sur con su certificado vigente y su PDF descargado.',
      'Quien verifica abre el enlace sin sesión, desde una ventana nueva del navegador.',
    ],
    relacionados: ['publico/verificar-certificado-autentico', 'publico/verificar-certificado-invalido'],
  },
  async ({ page, browser }) => {
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

    const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
    await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    const enlace = await ana.getByRole('link', { name: 'Página de verificación' }).getAttribute('href');
    const publica = await (await browser.newContext()).newPage();
    const url = new URL(enlace ?? '');
    // El enlace del código QR sin la huella: el caso en que hay que compararla a mano con el PDF.
    await publica.goto(url.pathname);
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();
    await expect(publica.getByRole('listitem').filter({ hasText: 'Huella del enlace' })).toContainText('compárala con la impresa en el PDF');

    // 1. Qué dice la página sin huella en el enlace.
    await paso(publica, {
      titulo: 'Lee la huella que muestra la página',
      explicacion:
        'Con el certificado vigente, la página muestra su «Huella», los 12 caracteres que identifican ese certificado, y la sección de autenticidad. ' +
        'Si el enlace no trae la huella, la comprobación «Huella del enlace» queda en aviso y te pide compararla con la impresa en el PDF.',
      resaltar: [publica.getByText('Huella', { exact: true }), publica.getByRole('list').filter({ hasText: 'Huella del enlace' })],
      celular: false,
    });

    // 2. Comparar con el PDF.
    const huella = (await publica.getByText('Huella', { exact: true }).locator('xpath=following-sibling::*[1]').textContent())?.trim() ?? '';
    expect(huella.length).toBeGreaterThan(0);
    await paso(publica, {
      titulo: 'Compara la huella con la impresa en el PDF',
      explicacion:
        'Abre el PDF del certificado y busca la huella impresa; debe ser igual a la que muestra la página, carácter por carácter. Si coincide, el documento que tienes es el que Validasoft emitió. ' +
        'Si no coincide, o si el PDF no trae huella, no confíes en el documento: pide a la empresa que te envíe el certificado actual.',
      resaltar: publica.getByText('Huella', { exact: true }),
      variantes: [
        { cuando: 'Abres el enlace completo del código QR', entonces: 'El enlace trae la huella y la página compara sola el contenido: no tienes que hacerlo a mano.' },
        { cuando: 'La huella del enlace no es la de este certificado', entonces: 'La página dice «puede tratarse de otro documento».' },
      ],
    });
  },
);
