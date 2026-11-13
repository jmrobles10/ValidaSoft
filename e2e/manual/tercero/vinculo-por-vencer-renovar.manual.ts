import { areaDe } from '../nucleo/semillas';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { llenarGeneralJuridica } from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, revisarVigencias } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const RAZON_SOCIAL = 'Aceros de la Vigencia S.A.S.';

/** `YYYY-MM-DD` más `dias` días (calendario, sin zona). */
function sumar(fecha: string, dias: number): string {
  const [a, m, d] = fecha.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

/** `YYYY-MM-DD` → `dd/mm/aaaa`, como lo muestran las pantallas. */
function enPantalla(fecha: string): string {
  const [a, m, d] = fecha.split('-');
  return `${d ?? ''}/${m ?? ''}/${a ?? ''}`;
}

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Ratifica tu información antes de que venza el vínculo',
    resumen:
      'Tu vínculo con una empresa tiene vigencia. Algunos días antes de vencer, la empresa te pide ratificar tu información: si sigue igual, la confirmas sin volver a revisarla; si cambió, la actualizas.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y un flujo de Cliente con RUT e información general, cuyo vínculo vale 90 días con avisos a 30 y 5 días y 3 de gracia.',
      'Un tercero jurídico cuyo vínculo fue aprobado; la fecha de vencimiento está a 30 días, y el primer aviso de pre-vencimiento ya llegó.',
    ],
    relacionados: ['tercero/documento-por-vencer-y-vencido', 'tercero/vinculo-en-revision-por-actualizacion'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_VIGENCIA_MANUAL', areaDe(e.sur, 'revision'), [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }], {
      dias: 90,
      diasAviso: [30, 5],
      diasGracia: 3,
    });
    const tercero = nuevoTercero(RAZON_SOCIAL);
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await llenarGeneralJuridica(page, RAZON_SOCIAL);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await subirDocumento(page, RUT, pdf(`RUT vigencia ${tercero.identificacion.numero}`));
    await esperarEstadoDelTercero(page, 'EnRevision');
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await aprobarArea(sofia, 'Revisión');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // Llega el primer aviso de pre-vencimiento (30 días antes del fin de la vigencia).
    const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
    const hasta = sumar(hoy, 90);
    const momentoAviso = new Date(`${sumar(hasta, -30)}T13:00:00Z`);
    await revisarVigencias(e, momentoAviso);

    // 1. «Mis empresas» dice cuándo vence y qué hacer.
    const tarjetaSur = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true }) });
    await expect(async () => {
      await page.goto('/empresas');
      await expect(tarjetaSur).toContainText('En pre-vencimiento', { timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await expect(tarjetaSur).toContainText(`ratifica tu información con ${e.sur.nombre} antes del ${enPantalla(hasta)}`);
    await paso(page, {
      titulo: 'Mira que tu vínculo está por vencer',
      explicacion:
        'La tarjeta de la empresa dice «En pre-vencimiento» y la fecha límite para ratificar tu información. Mientras tanto tu vínculo sigue activo: solo necesitas confirmarlo antes de esa fecha. ' +
        'El enlace «Ratificar mi información» te lleva a la sección que la empresa quiere renovar.',
      resaltar: tarjetaSur,
    });

    // 2. Ratificar sin volver a revisar.
    await page.goto('/ficha');
    await expect(page.getByText('1 empresa te pide ratificar tu información')).toBeVisible({ timeout: 30_000 });
    await page.getByRole('button', { name: `Ratificar para ${e.sur.nombre}` }).click();
    const dialogo = page.getByRole('dialog', { name: '¿Tu información de Información general sigue igual?' });
    await expect(dialogo).toContainText(`${e.sur.nombre} la da por renovada sin volver a revisarla`);
    await paso(page, {
      titulo: 'Confirma que tu información sigue igual',
      explicacion:
        'En «Mi información» aparece el pedido de ratificar. Si la sección sigue igual, pulsa «Sí, sigue igual»: la empresa da la renovación por hecha sin volver a revisarla. ' +
        'Si algo cambió, usa «Actualizar en Mi información» antes de la fecha, porque la empresa sí revisa lo que cambies.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Sí, sigue igual' }).click();

    // 3. Queda confirmada.
    await expect(page.getByText(`Confirmaste tu información de Información general. ${e.sur.nombre} la dio por renovada.`)).toBeVisible();
    await paso(page, {
      titulo: 'Confirma que la renovación quedó hecha',
      explicacion:
        'Validasoft confirma que ratificaste la sección y que la empresa la dio por renovada. El vínculo sigue activo con una vigencia nueva que se cuenta desde esa aprobación.',
      resaltar: page.getByText(`Confirmaste tu información de Información general. ${e.sur.nombre} la dio por renovada.`),
    });
  },
);
