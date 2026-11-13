import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'operacion',
    actor: 'Administrador de empresa',
    titulo: 'Activa el recordatorio automático de observaciones',
    resumen:
      'La administradora activa el recordatorio automático: mientras un expediente siga devuelto al tercero, Validasoft le recuerda sus observaciones cada cierto número de días. Cada flujo puede seguir lo de la empresa, apagarlo o fijar sus propios días.',
    precondiciones: ['Un grupo con Constructora Sur y su administradora Ana, con el recordatorio automático apagado.'],
    relacionados: ['operacion/recordatorio-en-lote'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'plantillas');
    await expect(ana.getByRole('heading', { level: 1, name: 'Plantillas de correo' })).toBeVisible();
    const interruptor = ana.getByLabel('Enviar recordatorios automáticos');
    await expect(interruptor).not.toBeChecked();

    await paso(paginaAna, {
      titulo: 'Abre el recordatorio automático en Plantillas de correo',
      explicacion:
        'En «Plantillas de correo» aparece el bloque «Recordatorio automático de observaciones». Mientras un expediente siga devuelto al tercero, Validasoft le recuerda sus observaciones por correo y en su portal cada cierto número de días, a lo sumo una vez al día. Se detiene cuando el tercero responde.',
      resaltar: interruptor,
      configuracion: {
        ajuste: 'Recordatorio automático de observaciones',
        ruta: 'Configuración › Plantillas de correo (/plantillas)',
        efecto: 'Es el valor de la empresa; cada flujo puede sobrescribirlo.',
      },
      variantes: [{ cuando: 'Eres revisor, no administrador', entonces: 'Ves el bloque, pero el interruptor y los días son solo de lectura.' }],
    });

    await interruptor.check();
    const dias = ana.getByRole('textbox', { name: 'Cada (días)' });
    await dias.fill('3');
    await expect(ana.getByText('Entre 1 y 60 días desde que se devolvió o desde el último recordatorio.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Activa el envío y elige cada cuántos días',
      explicacion:
        'Enciende «Enviar recordatorios automáticos» y escribe en «Cada (días)» un número entero de 1 a 60. Los días cuentan desde que el expediente se devolvió o desde el último recordatorio. Con 3 días, el tercero recibe uno cada tres días mientras siga devuelto.',
      resaltar: [interruptor, dias],
      variantes: [
        { cuando: 'Escribes un número fuera de 1 a 60 o no entero', entonces: 'El campo muestra «Escribe un número entero de 1 a 60» y el botón Guardar no se activa.' },
        { cuando: 'Apagas el interruptor', entonces: 'El campo de días se desactiva y la empresa no envía recordatorios automáticos.' },
      ],
    });

    await ana.getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(ana.getByText('Guardamos el recordatorio automático.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Guarda el recordatorio automático',
      explicacion:
        'Al guardar, Validasoft confirma el cambio. El envío automático empieza a aplicarse a los expedientes devueltos desde ese momento, sin que nadie tenga que hacerlo.',
      resaltar: ana.getByRole('button', { name: 'Guardar', exact: true }),
      avisos: true,
    });
  },
);
