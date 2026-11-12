import { expect, type Locator, type Page } from '@playwright/test';
import { deflateSync } from 'node:zlib';
import { escribirFecha } from './fechas';

/**
 * Declaración jurada PEP (HU 49093) en el wizard «Mi información» del tercero para las pruebas de punta a punta: llena el diálogo
 * «Declaración jurada PEP» como lo haría la persona (firma dibujada con el mouse o imagen subida). Los textos son los de
 * apps/public/src/i18n/es-CO-ficha-pep.ts.
 */

export const TITULO_DECLARACION = 'Declaración jurada PEP';

/** El diálogo de la declaración, abierto. */
export const dialogoDeclaracion = (page: Page): Locator => page.getByRole('dialog', { name: TITULO_DECLARACION });

function crc32(bytes: Buffer): number {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc ^= byte;
    for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return (crc ^ 0xffffffff) >>> 0;
}

/** Un PNG RGB de verdad (con su CRC): fondo blanco y un trazo azul oscuro, del tamaño pedido (la firma que sube la persona). */
export function pngDeFirma(ancho = 360, alto = 120): { name: string; mimeType: string; buffer: Buffer } {
  const crudo = Buffer.alloc(alto * (1 + ancho * 3), 255);
  for (let y = 0; y < alto; y++) {
    crudo[y * (1 + ancho * 3)] = 0;
    for (let x = 0; x < ancho; x++) {
      const trazo = Math.abs(y - (alto / 2 + 20 * Math.sin(x / 25))) < 3 && x > 20 && x < ancho - 20;
      if (!trazo) continue;
      const i = y * (1 + ancho * 3) + 1 + x * 3;
      crudo[i] = 20;
      crudo[i + 1] = 30;
      crudo[i + 2] = 110;
    }
  }

  const trozo = (tipo: string, datos: Buffer): Buffer => {
    const cabeza = Buffer.alloc(8);
    cabeza.writeUInt32BE(datos.length, 0);
    cabeza.write(tipo, 4, 'ascii');
    const cola = Buffer.alloc(4);
    cola.writeUInt32BE(crc32(Buffer.concat([Buffer.from(tipo, 'ascii'), datos])), 0);
    return Buffer.concat([cabeza, datos, cola]);
  };
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(ancho, 0);
  ihdr.writeUInt32BE(alto, 4);
  ihdr.set([8, 2, 0, 0, 0], 8);
  const buffer = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), trozo('IHDR', ihdr), trozo('IDAT', deflateSync(crudo)), trozo('IEND', Buffer.alloc(0))]);
  return { name: 'firma.png', mimeType: 'image/png', buffer };
}

/** Dibuja una firma en el lienzo con el mouse (la pestaña «Dibujar» es la que abre por omisión). */
export async function dibujarFirma(page: Page, dialogo: Locator): Promise<void> {
  const lienzo = dialogo.getByRole('img', { name: 'Lienzo para dibujar la firma' });
  await lienzo.scrollIntoViewIfNeeded();
  const caja = await lienzo.boundingBox();
  if (!caja) throw new Error('El lienzo de la firma no tiene caja.');
  const x = caja.x + 20;
  const y = caja.y + caja.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  for (let paso = 1; paso <= 24; paso++) await page.mouse.move(x + paso * ((caja.width - 40) / 24), y + Math.sin(paso / 2) * (caja.height / 4));
  await page.mouse.up();
  // Con trazos, «Limpiar» se habilita (el lienzo no muestra vista previa: la firma normalizada se entrega al confirmar).
  await expect(dialogo.getByRole('button', { name: 'Limpiar' })).toBeEnabled();
}

/** «Subir imagen»: elige un PNG como firma. */
export async function subirFirma(dialogo: Locator): Promise<void> {
  await dialogo.getByRole('tab', { name: 'Subir imagen' }).click();
  await dialogo.getByTestId('entrada-firma').setInputFiles(pngDeFirma());
  await expect(dialogo.getByRole('img', { name: 'Vista previa de la firma' })).toBeVisible();
}

export interface OpcionesDeclaracion {
  cargo?: string;
  institucion?: string;
  /** Persona de la legal: la dirección no está en su fila, la escribe quien declara. */
  direccion?: string;
  firma: 'dibujar' | 'subir';
  /** Persona de la legal que firma como representante legal: nombre, tipo y número del firmante. */
  firmante?: { nombre: string; tipo: string; numero: string };
  /** Agrega un familiar y una sociedad (con «Otra»), el cónyuge y las cuentas en el exterior. */
  completa?: boolean;
}

async function elegirEnCombo(page: Page, dialogo: Locator, combo: string, escribir: string, opcion: string | RegExp): Promise<void> {
  await dialogo.getByRole('combobox', { name: combo, exact: true }).fill(escribir);
  await page.getByRole('option', { name: opcion }).first().click();
}

/** Llena el diálogo «Declaración jurada PEP» abierto y lo confirma. */
export async function diligenciarDeclaracion(page: Page, opciones: OpcionesDeclaracion): Promise<void> {
  const dialogo = dialogoDeclaracion(page);
  await expect(dialogo).toBeVisible();
  const { cargo = 'Alcalde municipal', institucion = 'Alcaldía de Sopó' } = opciones;

  await dialogo.getByRole('combobox', { name: 'Nacionalidad', exact: true }).fill('Colombia');
  await page.getByRole('option', { name: 'Colombia' }).first().click();
  if (opciones.direccion) await dialogo.getByRole('textbox', { name: 'Dirección', exact: true }).fill(opciones.direccion);

  await dialogo.getByRole('textbox', { name: 'Cargo desempeñado' }).fill(cargo);
  await dialogo.getByRole('textbox', { name: 'Institución', exact: true }).fill(institucion);
  await elegirEnCombo(page, dialogo, 'País de la institución', 'Colombia', 'Colombia');
  await escribirFecha(page, 'Fecha de vinculación', '2018-01-01', dialogo);
  await escribirFecha(page, 'Fecha de desvinculación', '2022-01-01', dialogo);

  const cuentas = dialogo.getByRole('radiogroup', { name: '¿Tiene poder de firma u otro derecho sobre cuentas financieras en el exterior?' });
  if (!opciones.completa) await cuentas.getByRole('radio', { name: 'No' }).check();
  if (opciones.completa) {
    await cuentas.getByRole('radio', { name: 'Sí' }).check();
    await dialogo.getByRole('combobox', { name: 'Países de las cuentas' }).fill('Estados');
    await page.getByRole('option', { name: 'Estados Unidos' }).first().click();
    await page.keyboard.press('Escape');
    await dialogo.getByRole('textbox', { name: 'Nombre del cónyuge' }).fill('Luis Ruiz');
    await dialogo.getByRole('combobox', { name: 'Tipo de documento del cónyuge' }).click();
    await page.getByRole('option', { name: 'CC', exact: true }).click();
    await dialogo.getByRole('textbox', { name: 'Número de documento del cónyuge' }).fill('80111222');
    await dialogo.getByRole('button', { name: 'Agregar familiar' }).click();
    const familiar = dialogo.getByRole('listitem', { name: 'Familiar 1' });
    await familiar.getByRole('textbox', { name: 'Nombre completo' }).fill('Marta Pérez');
    await familiar.getByRole('textbox', { name: 'Parentesco' }).fill('Hermana');
    await familiar.getByRole('combobox', { name: 'Tipo de documento' }).click();
    await page.getByRole('option', { name: 'CC', exact: true }).click();
    await familiar.getByRole('textbox', { name: 'Número de documento' }).fill('52111222');
    await dialogo.getByRole('button', { name: 'Agregar sociedad' }).click();
    const sociedad = dialogo.getByRole('listitem', { name: 'Sociedad 1' });
    await sociedad.getByRole('textbox', { name: 'Nombre o razón social' }).fill('Inversiones Sopó S.A.S.');
    await sociedad.getByRole('textbox', { name: 'Identificación' }).fill('900111222');
    await sociedad.getByRole('combobox', { name: 'En calidad de' }).click();
    await page.getByRole('option', { name: 'Otra', exact: true }).click();
    await sociedad.getByRole('textbox', { name: '¿Cuál?' }).fill('Fideicomitente');
  }

  await dialogo.getByRole('radiogroup', { name: '¿Tiene algún vínculo familiar o societario con una PEP?' }).getByRole('radio', { name: 'No' }).check();

  if (opciones.firmante) {
    await dialogo.getByRole('combobox', { name: 'Firma en calidad de' }).click();
    await page.getByRole('option', { name: 'Representante legal', exact: true }).click();
    await dialogo.getByRole('textbox', { name: 'Nombre completo del firmante' }).fill(opciones.firmante.nombre);
    await dialogo.getByRole('combobox', { name: 'Tipo de documento del firmante' }).click();
    await page.getByRole('option', { name: opciones.firmante.tipo, exact: true }).click();
    await dialogo.getByRole('textbox', { name: 'Número de documento del firmante' }).fill(opciones.firmante.numero);
  }

  if (opciones.firma === 'dibujar') await dibujarFirma(page, dialogo);
  else await subirFirma(dialogo);
  await dialogo.getByRole('checkbox', { name: /^Declaro bajo juramento/ }).check();
  await dialogo.getByRole('button', { name: 'Confirmar declaración' }).click();
  try {
    await expect(dialogo).toBeHidden({ timeout: 8000 });
  } catch (error) {
    // Dice qué campos quedaron marcados (es lo que el diálogo pide corregir) en vez de un «sigue visible» sin contexto.
    const marcados = await dialogo.evaluate((nodo) =>
      [...nodo.querySelectorAll('[aria-invalid="true"]')].map((campo) => {
        const etiqueta = campo.id ? nodo.querySelector(`label[for="${campo.id}"]`)?.textContent : null;
        const ayuda = campo.closest('.MuiFormControl-root')?.querySelector('.MuiFormHelperText-root')?.textContent;
        return `${etiqueta ?? campo.getAttribute('aria-label') ?? campo.id}: ${ayuda ?? ''}`;
      }),
    );
    throw new Error(`La declaración no se confirmó; campos marcados: ${marcados.join(' | ') || 'ninguno'}`, { cause: error });
  }
}

/** El token de la sesión del tercero, tomado de lo que su propia página envía (para probar la API como lo haría su navegador). */
export function guardarAutorizacion(page: Page): { valor: () => string } {
  let autorizacion = '';
  page.on('request', (peticion) => {
    const cabecera = peticion.headers().authorization;
    if (peticion.url().includes('/public/v1/') && cabecera) autorizacion = cabecera;
  });
  return { valor: () => autorizacion };
}

/**
 * Captura del diálogo (reglas visuales: en claro y en celular). Con `E2E_CAPTURAS=<carpeta>`: el diálogo solo en escritorio y la pantalla
 * visible a 390 × 844, donde el diálogo ocupa toda la pantalla (la captura de página completa lo dejaría a media altura).
 */
export async function capturarDialogo(page: Page, dialogo: Locator, nombre: string): Promise<void> {
  const carpeta = process.env.E2E_CAPTURAS;
  if (!carpeta) return;
  const original = page.viewportSize();
  await dialogo.screenshot({ path: `${carpeta}/${nombre}-claro.png` });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(dialogo).toBeVisible();
  await page.screenshot({ path: `${carpeta}/${nombre}-celular.png` });
  if (original) await page.setViewportSize(original);
}

/** Captura de un bloque de la pantalla de la empresa (el iframe de la app recorta la página completa): `E2E_CAPTURAS=<carpeta>`. */
export async function capturarBloque(bloque: Locator, nombre: string): Promise<void> {
  const carpeta = process.env.E2E_CAPTURAS;
  if (!carpeta) return;
  await bloque.scrollIntoViewIfNeeded();
  await bloque.screenshot({ path: `${carpeta}/${nombre}-claro.png` });
}
