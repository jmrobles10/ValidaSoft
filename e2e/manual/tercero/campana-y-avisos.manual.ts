import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, observar } from '../../soporte/empresa';
import { abrirEmpresa, completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Entérate de los avisos desde la campana',
    resumen:
      'La campana de la barra superior te avisa cuando una empresa te pide corregir algo o cuando un documento está por vencer. Los avisos llegan en vivo, sin recargar la página, y puedes elegir cuáles además te llegan por correo.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Norte» (RUT y Cámara de comercio) y un área «Compras» con una persona como miembro.',
      'Un tercero que completó su invitación de Norte y tiene su expediente en revisión, mientras el área Compras observa el RUT.',
    ],
    relacionados: ['tercero/subir-version-nueva', 'tercero/responder-observaciones'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería Campana S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero, 'PROVEEDOR');
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero}`) },
      { nombre: CAMARA, archivo: pdf(`Camara ${tercero.identificacion.numero}`) },
    ]);
    const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte);
    await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
    await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Ilegible'] }], 'No se lee el NIT.');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.norte.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(page, 'Observado');

    // 1. La campana muestra cuántas notificaciones no has leído.
    const campana = page.getByRole('button', { name: /^Notificaciones, \d+ sin leer$/ });
    await expect(campana).toBeVisible({ timeout: 45_000 });
    const sinLeer = Number(/(\d+) sin leer/.exec((await campana.getAttribute('aria-label')) ?? '')?.[1]);
    expect(sinLeer).toBeGreaterThanOrEqual(1);
    await paso(page, {
      titulo: 'Mira la campana de notificaciones',
      explicacion:
        `La campana de la barra superior muestra un número cuando tienes notificaciones sin leer. Aquí hay ${String(sinLeer)}: entre ellas, Constructora Norte te pidió corregir el RUT. ` +
        'El número se actualiza solo: si llega un aviso nuevo mientras miras la página, la campana cambia sin que recargues.',
      resaltar: campana,
    });

    // Lo que dice la explicación: un aviso nuevo llega en vivo. Rubén le recuerda al tercero sus observaciones y la campana sube en uno sin recargar la página.
    await ruben.getByRole('button', { name: 'Recordar al tercero', exact: true }).click();
    const recordatorio = ruben.getByRole('dialog', { name: '¿Recordarle sus observaciones al tercero?' });
    await recordatorio.getByRole('button', { name: 'Enviar recordatorio', exact: true }).click();
    await expect(recordatorio).toBeHidden();
    const campanaConDos = page.getByRole('button', { name: `Notificaciones, ${String(sinLeer + 1)} sin leer` });
    await expect(campanaConDos).toBeVisible({ timeout: 45_000 });

    // 2. Preferencias de correo.
    await campanaConDos.click();
    await expect(page.getByRole('dialog').getByText(/corregir/i).first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa tus notificaciones',
      explicacion:
        'Al abrir la campana ves tus notificaciones más recientes, con la marca de la empresa que las envía. ' +
        'Al pulsar una, vas a la pantalla correspondiente, y «Marcar todas como leídas» quita el número de la campana.',
      resaltar: page.getByRole('button', { name: 'Marcar todas como leídas' }),
    });
    await page.getByRole('button', { name: 'Preferencias de correo' }).click();
    const preferencias = page.getByRole('dialog', { name: 'Preferencias de correo' });
    await expect(preferencias).toBeVisible();
    await paso(page, {
      titulo: 'Elige qué avisos además te llegan por correo',
      explicacion:
        'En «Preferencias de correo» marcas los tipos de aviso que también quieres recibir en tu correo, como las observaciones de una empresa o el vencimiento de un documento. ' +
        'Las notificaciones de la campana llegan siempre; las preferencias solo deciden el correo.',
      resaltar: preferencias,
    });
    await expect(preferencias.locator('.MuiSwitch-root').first()).toBeVisible();
    await preferencias.locator('.MuiSwitch-root').first().click();
    await preferencias.getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(page.getByText('Guardamos tus preferencias de correo.')).toBeVisible();
    await paso(page, {
      titulo: 'Confirma que tus preferencias quedaron guardadas',
      explicacion:
        'Al guardar, Validasoft confirma con un aviso breve que tus preferencias quedaron guardadas.',
      resaltar: page.getByText('Guardamos tus preferencias de correo.'),
    });

    // Lo que dice la explicación del segundo paso: «Marcar todas como leídas» quita el número y pulsar un aviso lleva a su pantalla.
    await page.getByRole('button', { name: /^Notificaciones/ }).click();
    await page.getByRole('button', { name: 'Marcar todas como leídas' }).click();
    await expect(page.getByRole('dialog').getByText('Sin leer', { exact: true })).toHaveCount(0);
    await page.goto('/boveda');
    await page.getByRole('button', { name: /^Notificaciones/ }).click();
    await page.getByRole('dialog').getByRole('button').filter({ hasText: /corregir|observ/i }).first().click();
    await expect(page.getByRole('heading', { level: 1, name: /^Tu revisión con / })).toBeVisible();
  },
);
