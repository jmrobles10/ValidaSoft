import { pdf } from '../../soporte/archivos';
import { esperarCorreo, literal } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, irASeccion, observar } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'operacion',
    actor: 'Revisor/área',
    titulo: 'Recuerda a varios terceros sus observaciones desde la bandeja',
    resumen:
      'Rubén, de Compras, tiene dos expedientes devueltos a terceros que no han respondido. Desde la bandeja selecciona los observados y envía un recordatorio a todos de una vez; cada tercero recibe un correo y un aviso en su portal, y solo se le puede recordar una vez al día.',
    precondiciones: [
      'Un grupo con Constructora Norte, Rubén en el área Compras y dos proveedores con el RUT observado por Compras.',
    ],
    relacionados: ['operacion/recordatorio-automatico-por-flujo'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const a = nuevoTercero('Aceros del Norte S.A.S.');
    const b = nuevoTercero('Tornillos del Sur S.A.S.');
    for (const tercero of [a, b]) {
      const invitacion = await invitar(e, e.norte, tercero);
      await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
        { nombre: RUT, archivo: pdf(`RUT ${tercero.nombre}`) },
        { nombre: 'Certificado de existencia y representación legal', archivo: pdf(`Cámara ${tercero.nombre}`) },
      ]);
    }

    const paginaRuben = await (await browser.newContext()).newPage();
    const ruben = await abrirEmbebido(paginaRuben, e, e.ruben, e.norte, 'bandeja');
    for (const tercero of [a, b]) {
      await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
      await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Ilegible'] }], 'Falta nitidez.');
      await irASeccion(ruben, 'Bandeja');
    }

    await expect(ruben.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await expect(ruben.getByRole('button', { name: 'Seleccionar observados' })).toBeVisible();
    await paso(paginaRuben, {
      titulo: 'Abre la bandeja con los expedientes observados',
      explicacion:
        'La bandeja de revisión lista los expedientes que esperan decisión de tus áreas. Los que devolviste con observaciones quedan a la espera de la respuesta del tercero. Desde la barra superior puedes recordarles a todos los observados de una vez.',
      resaltar: [ruben.getByRole('button', { name: 'Seleccionar observados' }), ruben.getByRole('button', { name: 'Recordar a los observados' })],
    });

    await ruben.getByRole('button', { name: 'Seleccionar observados' }).click();
    await ruben.getByRole('button', { name: 'Recordar (2)' }).click();
    const dialogo = ruben.getByRole('dialog', { name: '¿Recordar las observaciones de 2 expedientes?' });
    await paso(paginaRuben, {
      titulo: 'Selecciona los expedientes y confirma',
      explicacion:
        '«Seleccionar observados» marca los expedientes devueltos. El botón indica cuántos vas a recordar. Antes de enviar, el aviso dice que cada tercero recibe un correo con lo que falta corregir y un aviso en su portal, y que a quien ya se le recordó hoy no se le vuelve a enviar.',
      resaltar: [ruben.getByRole('button', { name: 'Recordar (2)' }), dialogo],
      variantes: [
        { cuando: 'Un expediente ya se recordó hoy', entonces: 'Se omite y el resultado lo dice: «1 omitido: ya se les recordó hoy».' },
        { cuando: 'Eliges más de 200 expedientes a la vez', entonces: 'Validasoft no envía: usa «Recordar a los observados».' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Enviar recordatorios' }).click();
    await expect(ruben.getByText('Recordamos a 2 terceros.')).toBeVisible();
    await paso(paginaRuben, {
      titulo: 'Comprueba el resultado del envío',
      explicacion:
        'El resultado dice a cuántos terceros se recordó y cuántos se omitieron, con el motivo. Cada tercero recibe un correo con el texto «te recuerda las observaciones de tu validación», con lo que falta corregir y un enlace para responder, sin nombres internos de la empresa.',
      resaltar: ruben.getByText('Recordamos a 2 terceros.'),
      avisos: true,
    });

    await esperarCorreo(a.correo, new RegExp(`^${literal(e.norte.nombre)} te recuerda las observaciones de tu validación `));

    await ruben.getByRole('button', { name: 'Recordar a los observados' }).click();
    await ruben.getByRole('dialog', { name: '¿Recordar a todos los terceros con observaciones pendientes?' }).getByRole('button', { name: 'Enviar recordatorios' }).click();
    await expect(ruben.getByText('No hay terceros por recordar: nadie tiene observaciones pendientes o ya se les recordó hoy.')).toBeVisible();
    await paso(paginaRuben, {
      titulo: 'Intenta recordar de nuevo el mismo día',
      explicacion:
        'Como ya se les recordó hoy, un segundo envío no hace nada: Validasoft lo indica sin enviar correos. El recordatorio se puede repetir al día siguiente.',
      resaltar: ruben.getByText('No hay terceros por recordar: nadie tiene observaciones pendientes o ya se les recordó hoy.'),
    });
  },
);
