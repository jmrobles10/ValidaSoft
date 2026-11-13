import type { Browser } from '@playwright/test';
import { areaDe } from '../nucleo/semillas';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion } from '../../soporte/empresa';
import { llenarGeneralJuridica } from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, revisarVigencias, type Escenario, type Invitacion } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const RAZON_SOCIAL = 'Transportes de la Vigencia S.A.S.';

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

/** Momento del job de vigencias que avisa 30 días antes del fin de la vigencia. */
const momentoDeAviso = (fecha: string) => new Date(`${fecha}T13:00:00Z`);
/** Momento del job que vence el vínculo al día siguiente de terminar la gracia. */
const momentoDeVencimiento = (finDeGracia: string) => new Date(`${sumar(finDeGracia, 1)}T05:00:00Z`);

/** Cliente de Sur con un flujo de vigencia (90 días, avisos a 30 y 5, 3 de gracia), aprobado por Sofía: el vínculo queda activo. */
async function clienteAprobado(browser: Browser, e: Escenario, invitacion: Invitacion, tercero: ReturnType<typeof nuevoTercero>) {
  const pagina = await (await browser.newContext()).newPage();
  await entrarPorInvitacion(pagina, invitacion);
  await aceptarInvitacion(pagina, invitacion);
  await llenarGeneralJuridica(pagina, RAZON_SOCIAL);
  await pagina.goto('/empresas');
  await abrirEmpresa(pagina, e.sur.nombre, 'CLIENTE');
  await subirDocumento(pagina, RUT, pdf(`RUT vigencia ${tercero.identificacion.numero}`));
  await esperarEstadoDelTercero(pagina, 'EnRevision');
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(pagina, 'Aprobado');
}

recorrido(
  {
    dominio: 'documentos',
    actor: 'Administrador de empresa',
    titulo: 'Sigue la vigencia periódica de un vínculo',
    resumen:
      'La administradora de Constructora Sur ve cómo cambia un cliente cuyo vínculo tiene vigencia: activo y con fecha de vencimiento, por vencer con su renovación abierta, y vencido cuando el tercero no ratifica a tiempo. El certificado sigue la misma suerte.',
    precondiciones: [
      'Un grupo con Constructora Sur y un flujo de Cliente con RUT e información general, cuyo vínculo vale 90 días, avisa a los 30 y 5 días y tiene 3 de gracia.',
      'Un cliente jurídico aprobado por el área Revisión hoy: su vínculo queda activo hasta dentro de 90 días.',
    ],
    relacionados: ['tercero/vinculo-por-vencer-renovar', 'configuracion/flujo-vigencia-del-vinculo', 'documentos/ciclo-de-vigencia'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_VIGENCIA_ADMIN', areaDe(e.sur, 'revision'), [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }], {
      dias: 90,
      diasAviso: [30, 5],
      diasGracia: 3,
    });
    const tercero = nuevoTercero(RAZON_SOCIAL);
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await clienteAprobado(browser, e, invitacion, tercero);

    const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
    const hasta = sumar(hoy, 90);

    // 1. La lista de vínculos muestra el vínculo activo.
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'vinculos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vínculos' })).toBeVisible();
    const fila = app.getByRole('row', { name: `Abrir vínculo de ${invitacion.correo}` });
    await expect(fila).toContainText('Activo');
    await paso(page, {
      titulo: 'Mira el vínculo activo en la lista',
      explicacion:
        'El cliente aparece en la lista de vínculos de Constructora Sur con el estado «Activo». Mientras su validación esté vigente, el vínculo sigue operando normal, sin avisos. ' +
        'La fecha «Vigente hasta» vive dentro del vínculo: ábrelo para verla.',
      resaltar: fila,
      configuracion: {
        ajuste: 'Vigencia del vínculo (por flujo)',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Cada cuántos días se vuelve a validar a los terceros de ese flujo, y cuándo se avisa antes de vencer.',
      },
    });

    // 2. El detalle muestra hasta cuándo vale la validación.
    await fila.click();
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    await expect(app.getByText('Vigente hasta', { exact: true }).first()).toBeVisible();
    await expect(app.getByText(enPantalla(hasta), { exact: true }).first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa hasta cuándo vale la validación',
      explicacion:
        'En el detalle, «Vigente hasta» muestra la fecha en que vence la validación del tercero. La vigencia se cuenta desde la última aprobación, así que una renovación la vuelve a contar desde cero. ' +
        'Mientras falte más que el primer aviso, no hay nada que hacer.',
      resaltar: app.getByText('Vigente hasta', { exact: true }).first(),
    });

    // 3. Llega el primer aviso: pre-vencimiento con renovación abierta.
    expect(await revisarVigencias(e, momentoDeAviso(sumar(hasta, -30)))).toMatchObject({ revisados: 1, avisados: 1, vencidos: 0 });
    await irASeccion(app, 'Vínculos');
    await app.getByRole('row', { name: `Abrir vínculo de ${invitacion.correo}` }).click();
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    await expect(app.getByText('La validación de este tercero está en pre-vencimiento')).toBeVisible();
    await expect(app.getByText(`Vence el ${enPantalla(hasta)}. Su renovación está abierta`)).toBeVisible();
    await paso(page, {
      titulo: 'Atiende el pre-vencimiento',
      explicacion:
        'Treinta días antes de vencer, el vínculo pasa a «Por vencer» y el aviso dice la fecha de vencimiento. Se abre sola una renovación y el tercero recibe un correo para ratificar su información. ' +
        'El certificado sigue vigente mientras tanto. Si el tercero ratifica o actualiza antes de la fecha, el vínculo se renueva y no vence.',
      resaltar: [app.getByText('La validación de este tercero está en pre-vencimiento'), app.getByText('Por vencer', { exact: true }).first()],
    });

    // 4. El tercero no ratifica: el vínculo vence al terminar la gracia.
    expect(await revisarVigencias(e, momentoDeVencimiento(sumar(hasta, 3)))).toMatchObject({ revisados: 1, vencidos: 1 });
    await irASeccion(app, 'Vínculos');
    await app.getByRole('row', { name: `Abrir vínculo de ${invitacion.correo}` }).click();
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    await expect(app.getByText('Vínculo vencido', { exact: true }).first()).toBeVisible();
    await paso(page, {
      titulo: 'Mira el vínculo vencido',
      explicacion:
        'Si el tercero no ratifica a tiempo y terminó la gracia de 3 días, el vínculo pasa a «Vencido» y su certificado también. La renovación que estaba abierta sigue siendo la misma: el tercero la cierra ratificando su información, y el vínculo vuelve a estar activo con una vigencia nueva de 90 días.',
      resaltar: app.getByText('Vínculo vencido', { exact: true }).first(),
    });
  },
);
