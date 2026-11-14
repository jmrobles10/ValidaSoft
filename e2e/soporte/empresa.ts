import { expect, type FrameLocator, type Locator } from '@playwright/test';

/**
 * Acciones de las personas de la empresa dentro de Validasoft embebido (el iframe que devuelve `abrirEmbebido`).
 * Los textos son los de apps/web/src/i18n (es-CO): si cambian, cambian aquí.
 */

/**
 * Va a una sección de la barra superior: su pestaña si cabe en el ancho del iframe, y si no, la opción del menú «Más»
 * (las pestañas que no caben pasan allí, así que el mismo nombre puede ser un enlace de la barra o un ítem del menú).
 */
export async function irASeccion(app: FrameLocator, nombre: string): Promise<void> {
  const barra = app.getByRole('navigation', { name: 'Secciones' });
  await expect(barra).toBeVisible();
  const pestana = barra.getByRole('link', { name: nombre, exact: true });
  if ((await pestana.count()) > 0) {
    try {
      await pestana.click({ timeout: 3000 });
      return;
    } catch {
      // Al terminar de cargar la tipografía la barra se midió de nuevo y la pestaña pasó a «Más»: se sigue por el menú.
    }
  }
  // Con la sección activa en «Más», el botón lleva su nombre en lugar de «Más»: se reconoce por su menú.
  await barra.locator('button[aria-haspopup="menu"]').click();
  await app.getByRole('menuitem', { name: nombre, exact: true }).click();
}

/** Abre desde la bandeja el expediente del tercero para el área indicada. */
export async function abrirDesdeBandeja(app: FrameLocator, tercero: string, area: string, tipoVinculo?: 'Proveedor' | 'Cliente'): Promise<void> {
  // Se pasa por «Vínculos» para que la bandeja se monte de nuevo y se pida otra vez (si ya estaba abierta, no se refresca).
  await irASeccion(app, 'Vínculos');
  await irASeccion(app, 'Bandeja');
  await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
  let fila = app.getByRole('row', { name: new RegExp(`Abrir expediente .* de ${escapar(tercero)} \\(${escapar(area)}\\)`) });
  // El mismo tercero puede tener dos vínculos con la empresa (proveedor y cliente): la columna «Tipo» los separa.
  if (tipoVinculo) fila = fila.filter({ has: app.getByRole('cell', { name: tipoVinculo, exact: true }) });
  await expect(fila).toBeVisible();
  await fila.click();
  await expect(app.getByRole('heading', { level: 1, name: tercero })).toBeVisible();
}

/** Región «{área}» de «Decisión de cada área» en el detalle del expediente. */
export function regionDeArea(app: FrameLocator, area: string): Locator {
  return app.getByRole('region', { name: area, exact: true }).first();
}

/** «Aprobar área (n documentos)» y confirmar: aprueba en la versión vista todo lo que le queda al área. */
export async function aprobarArea(app: FrameLocator, area: string): Promise<void> {
  await regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = app.getByRole('dialog', { name: new RegExp(`^Aprobar ${escapar(area)}`) });
  await dialogo.getByRole('button', { name: /^Aprobar/ }).last().click();
  await expect(dialogo).toBeHidden();
  // Si era la última área, el expediente se cierra y la decisión por áreas deja de mostrarse.
  await expect(regionDeArea(app, area).filter({ hasText: 'Aprobada' }).or(app.getByText(/Todas las áreas aprobaron el /))).toBeVisible();
}

/**
 * La última área aprueba, pero el tipo de vínculo exige una cuenta bancaria designada y aprobada que aún no está (CB-4): el expediente
 * sigue «En revisión» con el aviso «Falta la cuenta bancaria designada» y no se muestra como aprobado.
 */
export async function aprobarUltimaAreaYEsperarCuenta(app: FrameLocator, area: string): Promise<void> {
  await regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = app.getByRole('dialog', { name: new RegExp(`^Aprobar ${escapar(area)}`) });
  await dialogo.getByRole('button', { name: /^Aprobar/ }).last().click();
  await expect(dialogo).toBeHidden();
  await expect(app.getByRole('alert').filter({ hasText: 'Falta la cuenta bancaria designada' })).toBeVisible();
}

/** Texto visible de un área (estado y bloqueos), para afirmar sobre lo que ve cada persona. */
export async function estadoDeArea(app: FrameLocator, area: string): Promise<string> {
  return (await regionDeArea(app, area).innerText()).replace(/\s+/g, ' ');
}

function escapar(texto: string): string {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export interface DocumentoObservado {
  /** Etiqueta del documento en el diálogo, p. ej. «Registro Único Tributario · v1». */
  documento: string;
  /** Nombres de los motivos (la primera línea de cada opción), p. ej. «Ilegible». */
  motivos: string[];
}

/** «Observar» del área: marca documentos con sus motivos y devuelve el expediente al tercero. */
export async function observar(app: FrameLocator, area: string, documentos: DocumentoObservado[], nota?: string): Promise<void> {
  await regionDeArea(app, area).getByRole('button', { name: 'Observar', exact: true }).click();
  const dialogo = app.getByRole('dialog', { name: new RegExp(`^Observar en ${escapar(area)}`) });
  for (const [i, d] of documentos.entries()) {
    await dialogo.getByRole('checkbox', { name: d.documento, exact: true }).check();
    const grupo = dialogo.getByRole('group', { name: 'Motivos' }).nth(i);
    for (const motivo of d.motivos) await grupo.getByRole('checkbox', { name: new RegExp(`^${escapar(motivo)} `) }).check();
  }
  if (nota) await dialogo.getByRole('textbox', { name: 'Nota para el tercero (opcional)' }).fill(nota);
  const n = documentos.length;
  await dialogo.getByRole('button', { name: `Observar ${String(n)} ${n === 1 ? 'documento' : 'documentos'}` }).click();
  await expect(app.getByText(/^Observaste \d+ documentos?; el tercero recibe los motivos en un solo correo\.$/)).toBeVisible();
}

/** Imprime el árbol accesible del iframe (o de un diálogo) cuando algo falla, para ver qué había en pantalla. */
export async function conDiagnostico<T>(app: FrameLocator, accion: () => Promise<T>): Promise<T> {
  try {
    return await accion();
  } catch (error) {
    const dialogo = app.getByRole('dialog');
    const donde = (await dialogo.count()) > 0 ? dialogo.first() : app.getByRole('main');
    console.log('DIAGNOSTICO', await donde.ariaSnapshot().catch(() => '(sin árbol)'));
    throw error;
  }
}

/**
 * «Rechazar» (solo áreas con «Puede rechazar»): elige motivos (el tercero lee su texto) y escribe la justificación
 * interna; el expediente se cierra y el vínculo vuelve a Invitado.
 */
export async function rechazar(app: FrameLocator, motivos: string[], justificacion: string): Promise<void> {
  await conDiagnostico(app, async () => {
    await app.getByRole('button', { name: 'Rechazar', exact: true }).first().click();
    const dialogo = app.getByRole('dialog', { name: /^Rechazar expediente/ });
    for (const motivo of motivos) await dialogo.getByRole('checkbox', { name: new RegExp(`^${escapar(motivo)} `) }).check();
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill(justificacion);
    await dialogo.getByRole('button', { name: 'Rechazar expediente' }).click();
    await expect(dialogo).toBeHidden();
    await expect(app.getByText('Expediente rechazado', { exact: true }).first()).toBeVisible();
  });
}

/** «Aprobar en nombre del área» (administrador que no es miembro), con justificación. */
export async function aprobarEnNombreDe(app: FrameLocator, area: string, justificacion: string): Promise<void> {
  await conDiagnostico(app, async () => {
    await app.getByRole('button', { name: `Acciones de administrador en ${area}` }).click();
    await app.getByRole('menuitem', { name: 'Aprobar en nombre del área' }).click();
    const dialogo = app.getByRole('dialog', { name: new RegExp(`^Aprobar en nombre de ${escapar(area)}`) });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill(justificacion);
    await dialogo.getByRole('button', { name: 'Aprobar en nombre del área' }).click();
    await expect(dialogo).toBeHidden();
  });
}
