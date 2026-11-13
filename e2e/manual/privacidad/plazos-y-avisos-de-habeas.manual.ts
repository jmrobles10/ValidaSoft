import { CLAVE_PLATAFORMA, llamar } from '../../soporte/api';
import { pdf } from '../../soporte/archivos';
import { esperarCorreo } from '../../soporte/correo';
import { abrirEmbebidoConLlave, llaveConHabeas } from '../../soporte/derechos';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

interface ResultadoDeRevision {
  revisadas: number;
  porVencer: number;
  vencidas: number;
}

/** Lo que hace Plataforma al pedir el barrido de plazos ya, con una fecha simulada (solo en Development): la misma revisión que corre sola cada cierto tiempo. */
async function barrerPlazos(fecha: Date): Promise<ResultadoDeRevision> {
  return llamar<ResultadoDeRevision>('POST', '/api/v1/plataforma/habeas/revisar-plazos', { llave: CLAVE_PLATAFORMA, cuerpo: { fecha: fecha.toISOString() } });
}

/** `dd/mm/aaaa` → mediodía de ese día en Bogotá (UTC−5), más `dias`. */
function mediodiaDe(texto: string, dias: number): Date {
  const [d, m, a] = texto.split('/').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias, 17, 0, 0));
}

recorrido(
  {
    dominio: 'privacidad',
    actor: 'Administrador de empresa',
    titulo: 'Atiende los avisos de plazo de una solicitud de habeas data',
    resumen:
      'Una solicitud de habeas data se acerca a su fecha límite: la administradora recibe un aviso en su campana y por correo cuando le quedan dos días hábiles o menos, y otro cuando el plazo legal ya pasó. Cada aviso lleva a la solicitud para responderla.',
    precondiciones: [
      'Un grupo con Constructora Norte, un proveedor con su expediente completado y una solicitud de habeas data de conocer sus datos dirigida a la empresa.',
      'Plataforma corre el barrido de plazos con una fecha simulada: un día antes del vencimiento y un día después (en producción corre solo).',
    ],
    relacionados: ['privacidad/habeas-data-de-la-empresa', 'privacidad/solicitud-sobre-mis-datos'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const llave = await llaveConHabeas(e);
    const tercero = nuevoTercero('Aceros con Plazo S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);
    const paginaTercero = await (await browser.newContext()).newPage();
    await completarInvitacion(paginaTercero, invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT Aceros con Plazo') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara Aceros con Plazo') },
    ]);
    await paginaTercero.goto('/privacidad');
    await paginaTercero.getByRole('button', { name: 'Nueva solicitud' }).click();
    const nueva = paginaTercero.getByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
    await nueva.getByRole('radio', { name: /Conocer mis datos/ }).check();
    await nueva.getByRole('combobox', { name: 'Se la pides a' }).click();
    await paginaTercero.getByRole('option', { name: e.norte.nombre }).click();
    await nueva.getByRole('button', { name: 'Enviar solicitud' }).click();
    const tarjetaDelTercero = paginaTercero.getByRole('article').filter({ hasText: 'Conocer mis datos' });
    await expect(tarjetaDelTercero).toContainText(/HD-\d{4}-[0-9A-F]{8}/);
    const radicado = (await tarjetaDelTercero.textContent())?.match(/HD-\d{4}-[0-9A-F]{8}/)?.[0] ?? '';

    // 1) La solicitud y su fecha límite.
    const app = await abrirEmbebidoConLlave(page, e, llave, e.ana, e.norte, 'habeas-data');
    await expect(app.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
    const solicitud = app.getByRole('article').filter({ hasText: radicado });
    const plazo = solicitud.getByText(/Responde antes del \d{2}\/\d{2}\/\d{4}: quedan \d+ días hábiles\./);
    await expect(plazo).toBeVisible();
    const fechaLimite = (await plazo.textContent())?.match(/\d{2}\/\d{2}\/\d{4}/)?.[0] ?? '';
    await paso(page, {
      titulo: 'Mira la fecha límite de la solicitud',
      explicacion:
        'Cada solicitud dice hasta cuándo debe responderse (1) y cuántos días hábiles quedan: la ley da 10 o 15 días hábiles según el derecho que ejerce el titular. ' +
        'Nadie tiene que acordarse: Validasoft revisa los plazos de las solicitudes abiertas y avisa a quienes deben responder cuando la fecha se acerca y cuando pasa.',
      resaltar: plazo,
      configuracion: { ajuste: 'Quién responde el habeas data', ruta: 'Configuración › Usuarios y roles (/usuarios)', efecto: 'Reciben los avisos los administradores (del grupo o de la empresa) y el oficial de cumplimiento de la empresa.' },
      variantes: [{ cuando: 'La solicitud se dirige a Validasoft y no a una empresa', entonces: 'El aviso va a los correos de la plataforma que se configuran en el servidor, no a la campana de ninguna empresa.' }],
    });

    // 2) El aviso de plazo por vencer: faltan dos días hábiles o menos.
    const porVencer = await barrerPlazos(mediodiaDe(fechaLimite, -1));
    expect(porVencer.porVencer).toBeGreaterThanOrEqual(1);
    await page.reload();
    await expect(app.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
    await app.getByRole('button', { name: /^Notificaciones/ }).click();
    const campana = app.getByRole('dialog', { name: 'Notificaciones' });
    const avisoPorVencer = campana.getByText(/Vence pronto una solicitud de/).first();
    await expect(avisoPorVencer).toBeVisible();
    await paso(page, {
      titulo: 'Recibe el aviso de plazo por vencer',
      explicacion:
        'Cuando a una solicitud le quedan dos días hábiles o menos, la campana de la administradora y del oficial de cumplimiento muestra «Vence pronto una solicitud de…» (1), con el radicado, la fecha límite y los días que quedan. ' +
        'El aviso llega una sola vez por solicitud y también por correo, salvo que la persona haya apagado ese tipo de aviso en sus preferencias.',
      resaltar: avisoPorVencer,
      configuracion: { ajuste: 'Preferencias de notificaciones', ruta: 'Campana › Preferencias', efecto: 'Cada persona decide si le llega por correo el aviso de plazo de habeas data; la campana siempre lo muestra.' },
    });

    // 3) El aviso lleva a la solicitud.
    await avisoPorVencer.click();
    await expect(app.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
    await expect(solicitud).toBeVisible();
    await paso(page, {
      titulo: 'Abre la solicitud desde el aviso',
      explicacion:
        'Al abrir el aviso, la campana lleva directo a «Habeas data», donde está la solicitud (1) con su radicado, el titular y el plazo. Desde ahí se responde con «Responder»; al enviarla, la solicitud sale de las abiertas y no vuelve a generar avisos.',
      resaltar: solicitud,
    });

    // 4) El aviso de plazo vencido.
    const vencida = await barrerPlazos(mediodiaDe(fechaLimite, 1));
    expect(vencida.vencidas).toBeGreaterThanOrEqual(1);
    await page.reload();
    await expect(app.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
    await app.getByRole('button', { name: /^Notificaciones/ }).click();
    const campanaVencida = app.getByRole('dialog', { name: 'Notificaciones' });
    const avisoVencido = campanaVencida.getByText(/Venció el plazo de una solicitud de/).first();
    await expect(avisoVencido).toBeVisible();
    await paso(page, {
      titulo: 'Recibe el aviso de plazo vencido',
      explicacion:
        'Si la fecha límite pasa sin respuesta, llega un segundo aviso: «Venció el plazo de una solicitud de…» (1), que dice que ya pasó el plazo legal y pide responder cuanto antes. ' +
        'Es otro aviso distinto del anterior, y también sale una sola vez. La solicitud sigue abierta hasta que se responda.',
      resaltar: avisoVencido,
    });
    await page.keyboard.press('Escape');

    // 5) El correo.
    const correo = await esperarCorreo(e.ana.correo, /Venció el plazo de una solicitud de/);
    await page.setContent(`<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
      <body style="margin:0;background:#e9edf2;font-family:system-ui,'Segoe UI',sans-serif;color:#1b2129">
        <div id="correo" style="max-width:600px;margin:40px auto;padding:0 12px">
          <div style="background:#fff;border:1px solid #d5dbe3;border-radius:14px;overflow:hidden">
            <div style="padding:16px 22px;background:#f7f8fa;border-bottom:1px solid #e3e7ec;font-size:14px;line-height:1.6;color:#4a5460">
              <div><b>De:</b> ${correo.desde}</div><div><b>Para:</b> ${e.ana.correo}</div><div><b>Asunto:</b> ${correo.asunto}</div>
            </div>
            <div style="padding:26px 22px;font-size:16px;line-height:1.65">${correo.html}</div>
          </div>
        </div>
      </body>`);
    await paso(page, {
      titulo: 'Recibe el mismo aviso por correo',
      explicacion:
        'El correo repite el aviso con el radicado, la fecha en que venció y el plazo legal, e invita a responder. Llega a cada administrador y al oficial de cumplimiento de la empresa, uno por persona.',
      resaltar: page.locator('#correo'),
    });
  },
);
