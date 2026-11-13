import type { Browser, FrameLocator, Page } from '@playwright/test';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, type Escenario, type Tercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const BANCARIA = 'Certificación bancaria';

function marcasRut(tercero: Tercero, razonSocial = tercero.nombre): string {
  return `[[nit=${tercero.identificacion.numero}]][[razonSocial=${razonSocial}]]`;
}

function marcasBancaria(tercero: Tercero): string {
  return `[[banco=Bancolombia]][[tipoCuenta=Ahorros]][[numeroCuenta=${tercero.identificacion.numero}01]][[titular=${tercero.nombre}]]`;
}

/** Cliente de Sur con RUT y certificación bancaria, aprobado por Sofía (Revisión). Devuelve su ventana de proveedor. */
async function clienteAprobado(browser: Browser, e: Escenario, sofia: FrameLocator, nombre: string) {
  const tercero = nuevoTercero(nombre);
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  const ventana = await (await browser.newContext()).newPage();
  await completarInvitacion(ventana, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${nombre} v1 ${marcasRut(tercero)}`) },
    { nombre: BANCARIA, archivo: pdf(`Bancaria ${nombre} v1 ${marcasBancaria(tercero)}`) },
  ]);
  await abrirDesdeBandeja(sofia, nombre, 'Revisión', 'Cliente');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(ventana, 'Aprobado');
  return { tercero, ventana };
}

/** El tercero sube una versión nueva desde su bóveda. */
async function actualizarDesdeLaBoveda(ventana: Page, documento: string, archivo: ReturnType<typeof pdf>, version: number): Promise<void> {
  await ventana.goto('/boveda');
  await ventana.getByRole('article', { name: documento }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = ventana.waitForEvent('filechooser');
  await ventana.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(archivo);
  await expect(ventana.getByText(`Es tu versión ${String(version)} de ${documento}`)).toBeVisible({ timeout: 30_000 });
}

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Aprueba en lote las revalidaciones sin cambios',
    resumen:
      'Un cliente ya aprobado sube una versión nueva de su RUT y de su certificación bancaria con los mismos datos, y otro cambia su razón social. Sofía revisa la lista de revalidaciones, selecciona en lote las que no cambiaron y deja aparte la que sí cambió.',
    precondiciones: [
      'Un grupo con Constructora Sur, con el área Revisión (Sofía es miembro) y el análisis de documentos con IA activo.',
      'Dos clientes ya aprobados con RUT y certificación bancaria. Ambos suben versiones nuevas: uno con los mismos datos y otro con una razón social distinta.',
    ],
    relacionados: ['revision/actualizacion-de-un-documento', 'revision/eximir-un-documento'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_RV_DOC', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { tipoDocumento: 'CERTIFICACION_BANCARIA' }]);

    const sofia = await abrirEmbebido(page, e, e.sofia, e.sur, 'bandeja');
    const a = await clienteAprobado(browser, e, sofia, 'Aceros Sin Cambios S.A.S.');
    const b = await clienteAprobado(browser, e, sofia, 'Aceros Con Cambios S.A.S.');

    await actualizarDesdeLaBoveda(a.ventana, RUT, pdf(`RUT ${a.tercero.nombre} v2 ${marcasRut(a.tercero)}`), 2);
    await actualizarDesdeLaBoveda(a.ventana, BANCARIA, pdf(`Bancaria ${a.tercero.nombre} v2 ${marcasBancaria(a.tercero)}`), 2);
    await actualizarDesdeLaBoveda(b.ventana, RUT, pdf(`RUT ${b.tercero.nombre} v2 ${marcasRut(b.tercero, 'Otra Razón Social S.A.S.')}`), 2);

    // 1) La lista de revalidaciones: el análisis dice si cambió algo.
    const paginaRevalidaciones = await (await browser.newContext()).newPage();
    const revalidaciones = await abrirEmbebido(paginaRevalidaciones, e, e.sofia, e.sur, 'revalidaciones');
    const fila = (tercero: Tercero, documento: string) => revalidaciones.getByRole('row').filter({ hasText: tercero.nombre }).filter({ hasText: documento });
    await expect(async () => {
      await paginaRevalidaciones.reload();
      await expect(fila(a.tercero, RUT)).toContainText('Sin cambios', { timeout: 1500 });
      await expect(fila(b.tercero, RUT)).toContainText('Con cambios', { timeout: 1500 });
    }).toPass({ timeout: 120_000 });
    await paso(page, {
      titulo: 'Revisa la lista de revalidaciones',
      explicacion:
        'Cada versión nueva de un documento ya aprobado aparece en Revalidaciones con el resultado de la comparación: «Sin cambios» si los datos son los mismos, o «Con cambios» si cambió algo, con la versión anterior y la nueva. ' +
        'Las que dicen «Con cambios» se revisan una por una, en el expediente.',
      resaltar: revalidaciones.getByRole('table'),
    });

    // 2) Selección en lote: solo las que no cambiaron se pueden aprobar juntas.
    await revalidaciones.getByRole('button', { name: 'Seleccionar las sin cambios' }).click();
    await expect(revalidaciones.getByRole('checkbox', { name: `Seleccionar ${RUT} de ${a.tercero.nombre}` })).toBeChecked();
    await expect(revalidaciones.getByRole('checkbox', { name: `Seleccionar ${RUT} de ${b.tercero.nombre}` })).toBeDisabled();
    await revalidaciones.getByRole('button', { name: 'Aprobar 2 sin cambios' }).click();
    const dialogo = revalidaciones.getByRole('dialog', { name: '¿Aprobar 2 revalidaciones?' });
    await expect(dialogo).toContainText('cada una con su propio registro en el historial del expediente');
    await paso(page, {
      titulo: 'Aprueba las revalidaciones sin cambios en lote',
      explicacion:
        '«Seleccionar las sin cambios» marca solo las que no cambiaron; la que sí cambió queda deshabilitada y no entra al lote. ' +
        'El diálogo dice que cada revalidación tendrá su propio registro en el historial del expediente.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Aprobar 2 sin cambios' }).click();
    await expect(revalidaciones.getByText('Aprobaste 2 revalidaciones.')).toBeVisible();

    // 3) Lo que queda: la revalidación con cambios sigue en la lista.
    await expect(fila(a.tercero, RUT)).toHaveCount(0);
    await expect(fila(b.tercero, RUT)).toContainText('Con cambios');
    await paso(page, {
      titulo: 'Revisa lo que queda pendiente',
      explicacion:
        'Las revalidaciones aprobadas salen de la lista. La que cambió sigue aquí, para revisarla en su expediente antes de aprobarla.',
      resaltar: fila(b.tercero, RUT),
    });
  },
);
