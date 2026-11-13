import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Escenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

recorrido(
  {
    dominio: 'informes',
    actor: 'Administrador de empresa',
    titulo: 'Encuentra los certificados de la empresa',
    resumen:
      'La administradora de Constructora Sur abre Certificados: ve los certificados que la empresa emitió a sus terceros aprobados, con su estado y su folio. Busca uno por el nombre del tercero y abre su ficha.',
    precondiciones: [
      'Un grupo con Constructora Sur y el área Revisión con Sofía como miembro; el RUT y el certificado de seguridad y salud en el trabajo son requisitos del flujo de proveedor.',
      'Un proveedor cuyo expediente fue aprobado: la empresa le emitió un certificado vigente.',
    ],
    relacionados: ['informes/certificado-bloquear-y-revocar', 'publico/verificar-certificado-autentico', 'informes/exportar-un-expediente'],
  },
  async ({ page, browser }) => {
    const e: Escenario = await crearEscenario();
    const invitacion = await invitar(e, e.sur, nuevoTercero('Ferretería La Tuerca S.A.S.'));
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Tuerca') },
      { nombre: SST.nombre, archivo: pdf('SST Tuerca'), campos: { 'Fecha de expedición': haceDias(2) } },
    ]);
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');

    const app = await abrirEmbebido(page, e, e.ana, e.sur);
    await irASeccion(app, 'Certificados');
    await expect(app.getByRole('heading', { level: 1, name: 'Certificados' })).toBeVisible();
    const fila = app.getByRole('row').filter({ hasText: invitacion.nombre });
    await expect(fila).toContainText('Vigente');
    await expect(fila).toContainText(new RegExp(`${e.sur.prefijo}-C-\\d{4}-\\d{6}-1`));

    // 1. La lista de certificados.
    await paso(page, {
      titulo: 'Abre la lista de certificados',
      explicacion:
        'Certificados reúne los certificados que la empresa emitió a sus terceros aprobados. Por cada fila ves el tercero, el estado (vigente, bloqueado, vencido o revocado) y el folio del certificado, que es el número que usa quien lo verifica. ' +
        'Un certificado se emite cuando el vínculo queda aprobado y se reemite en cada renovación.',
      resaltar: fila,
      configuracion: {
        ajuste: 'Vigencia del vínculo (por flujo)',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'La vigencia del vínculo decide cuándo un certificado vence y cuándo hay que renovarlo.',
      },
    });

    // 2. Búsqueda por el nombre del tercero.
    const buscador = app.getByPlaceholder('Buscar por tercero, identificación o folio');
    await buscador.fill('Tuerca');
    await expect(app.getByRole('row').filter({ hasText: invitacion.nombre })).toBeVisible();
    await paso(page, {
      titulo: 'Busca un certificado por el nombre del tercero',
      explicacion:
        'El buscador encuentra un certificado por el tercero, su identificación o su folio. Escribe parte del nombre y la lista se reduce a sus coincidencias. ' +
        'Borra el texto para volver a ver todos.',
      resaltar: buscador,
    });

    // 3. Abre la ficha del tercero desde el certificado.
    await expect(async () => {
      await app.getByRole('row').filter({ hasText: invitacion.nombre }).getByText(new RegExp(`${e.sur.prefijo}-C-\\d{4}-\\d{6}-1`)).click({ timeout: 3000 });
      await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible({ timeout: 3000 });
    }).toPass({ timeout: 30_000 });
    await paso(page, {
      titulo: 'Abre la ficha del tercero desde su certificado',
      explicacion:
        'Al hacer clic en el folio se abre la ficha del tercero (su vista de 360 grados). Desde ahí puedes revisar el estado de su vínculo antes de decidir qué hacer con el certificado.',
      resaltar: app.getByRole('heading', { level: 1, name: invitacion.nombre }),
    });
  },
);
