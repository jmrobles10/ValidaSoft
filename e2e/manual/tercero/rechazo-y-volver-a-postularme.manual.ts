import { llamar } from '../../soporte/api';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, rechazar } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const MOTIVO = 'El documento no corresponde a la persona o empresa que se está validando.';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Vuelve a postularte después de un rechazo',
    resumen:
      'Cumplimiento rechaza tu expediente y te dice el motivo, sin mostrarte la justificación interna. Desde «Mis empresas» vuelves a postularte: tus documentos ya están en tu bóveda, subes el que falló corregido y se abre una revisión nueva.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Norte», su flujo de Proveedor (RUT y Cámara de comercio) con reintento permitido sin espera, un área «Compras» y un área «Cumplimiento» que puede rechazar.',
      'Un tercero que subió sus dos documentos y cuyo expediente fue rechazado por Cumplimiento con un motivo.',
    ],
    relacionados: ['tercero/expediente-aprobado', 'tercero/responder-observaciones'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    // El flujo de habilitación de Norte permite volver a postularse al instante tras un rechazo.
    const flujos = await llamar<{ id: string; tipoVinculo: string }[]>('GET', '/api/v1/flujos?tipoVinculo=PROVEEDOR', { llave: e.admin, empresa: e.norte.id });
    const habilitacion = flujos[0];
    if (!habilitacion) throw new Error('No está el flujo de habilitación de Norte.');
    await llamar('PUT', `/api/v1/flujos/${habilitacion.id}`, { llave: e.admin, empresa: e.norte.id, cuerpo: { reintento: { permitido: true, diasEspera: 0 } } });
    await llamar('POST', `/api/v1/flujos/${habilitacion.id}/publicar`, { llave: e.admin, empresa: e.norte.id });

    const tercero = nuevoTercero('Cementos del Valle S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero, 'PROVEEDOR');
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero} primera`) },
      { nombre: CAMARA, archivo: pdf(`Camara ${tercero.identificacion.numero}`) },
    ]);
    const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga);
    await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
    await rechazar(olga, ['No corresponde al tercero'], 'El representante legal aparece en una lista restrictiva con otro nombre.');

    // 1. El rechazo con el motivo que lee el tercero.
    await page.goto('/empresas');
    const norte = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true }) });
    await expect(norte.getByText(MOTIVO)).toBeVisible();
    await paso(page, {
      titulo: 'Lee por qué te rechazaron',
      explicacion:
        'La tarjeta de la empresa dice que cerró tu revisión sin aprobarla y muestra el motivo que escogió el área, en este caso que el documento no corresponde a la persona o empresa que se valida. ' +
        'Lo que no ves es la justificación interna que escribió la empresa ni quién la escribió: esa parte es privada. Tus documentos siguen en tu bóveda.',
      resaltar: norte,
    });

    // 2. «Volver a postularme» abre una revisión nueva con tus documentos ya cargados.
    await expect(async () => {
      if (/\/empresas\/?$/.test(new URL(page.url()).pathname)) await norte.getByRole('button', { name: 'Volver a postularme' }).click({ timeout: 3000 });
      await expect(page).toHaveURL(/\/expediente\/[0-9a-f-]{36}$/, { timeout: 5000 });
    }).toPass({ timeout: 30_000 });
    await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` })).toBeVisible();
    await expect(tarjeta(page, RUT)).toContainText('Versión 1');
    await expect(tarjeta(page, CAMARA)).toContainText('Versión 1');
    await paso(page, {
      titulo: 'Vuelve a postularte con los mismos documentos',
      explicacion:
        'Al pulsar «Volver a postularme» se abre una revisión nueva para la misma empresa. El RUT y la Cámara de comercio aparecen con la versión 1 que ya estaba en tu bóveda, así que no tienes que cargarlos de nuevo. ' +
        'Solo sube otra versión del documento que la empresa rechazó.',
      resaltar: page.getByRole('main'),
      configuracion: {
        ajuste: 'Después de un rechazo › Permitir que el tercero rechazado vuelva a postularse',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Con Días de espera, el botón aparece solo cuando pasan esos días desde el rechazo; con 0, de inmediato.',
      },
    });

    // 3. Sube la versión corregida del RUT.
    await esperarEstadoDelTercero(page, 'EnRevision');
    await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero} corregido`), { version: 2 });
    await expect(tarjeta(page, RUT)).toContainText('Versión 2');
    await paso(page, {
      titulo: 'Sube la versión corregida del documento que falló',
      explicacion:
        'En la tarjeta del RUT pulsa «Subir» y elige el archivo corregido. Queda como la versión 2 y la nueva revisión la recibe junto con los demás documentos. ' +
        'Las áreas revisan el expediente nuevo y deciden por separado, como en la primera revisión.',
      resaltar: tarjeta(page, RUT),
    });
  },
);
