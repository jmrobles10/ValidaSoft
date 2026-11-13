import { llamar, CLAVE_PLATAFORMA } from '../../soporte/api';
import { esperarCorreo } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'operacion',
    actor: 'Administrador de empresa',
    titulo: 'Atiende un expediente caducado por falta de plazo y reábrelo',
    resumen:
      'El flujo da un plazo para completar el expediente. Cuando el tercero no lo completa a tiempo, el expediente caduca y vuelve a Invitado. La administradora lo reabre con un plazo nuevo y el tercero recibe un enlace nuevo.',
    precondiciones: [
      'Un grupo con Constructora Norte y un flujo de Proveedor con plazo de cargue de 30 días (el valor por omisión).',
      'Un tercero invitado que aceptó la invitación pero no ha subido documentos.',
    ],
    relacionados: ['operacion/recordatorio-automatico-por-flujo', 'operacion/invitacion-por-lotes'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.norte, nuevoTercero('Plazo Vencido S.A.S.'));
    const tercero = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(tercero, invitacion);
    await aceptarInvitacion(tercero, invitacion);
    await expect(tercero.getByText(/Tienes hasta el .* para completarla\./)).toBeVisible();
    const expedienteId = await expedienteAbierto(e, invitacion);

    // Pasa el tiempo: el job de plazos corre con una fecha 31 días después, solo para este grupo de prueba.
    const fecha = new Date(Date.now() + 31 * 24 * 3600 * 1000).toISOString();
    const resultado = await llamar<{ caducados: number }>('POST', '/api/v1/plataforma/plazos-de-cargue/revisar', {
      llave: CLAVE_PLATAFORMA,
      cuerpo: { tenantId: e.tenantId, fecha },
    });
    expect(resultado.caducados).toBe(1);
    await esperarCorreo(invitacion.correo, /caducó$/);

    const paginaAna = await (await browser.newContext()).newPage();
    const app = await abrirEmbebido(paginaAna, e, e.ana, e.norte, `expedientes/${expedienteId}`);
    const aviso = app.getByRole('alert').filter({ hasText: 'Expediente caducado' });
    await expect(aviso).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Revisa el expediente caducado',
      explicacion:
        'Cuando el tercero no completa el expediente dentro del plazo de cargue, el expediente caduca: el aviso lo indica con la fecha en que venció el plazo. El vínculo vuelve a Invitado. ' +
        'Desde aquí puedes reabrir el expediente, que da al tercero un plazo nuevo completo y un enlace nuevo.',
      resaltar: aviso,
      configuracion: {
        ajuste: 'Plazo de cargue',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Son los días que tiene el tercero para completar el expediente inicial. 0 significa sin plazo; el flujo también define los recordatorios antes de vencer.',
      },
      variantes: [{ cuando: 'El flujo no tiene plazo de cargue', entonces: 'El expediente no caduca y este aviso no aparece.' }],
    });

    await app.getByRole('button', { name: 'Reabrir expediente' }).click();
    const dialogo = app.getByRole('dialog');
    await dialogo.getByLabel('Justificación (opcional)').fill('El proveedor pidió más tiempo.');
    await paso(paginaAna, {
      titulo: 'Reabre el expediente con un plazo nuevo',
      explicacion:
        'El diálogo explica que el expediente vuelve a Abierto con un plazo nuevo del tamaño configurado en el flujo, y que el tercero recibe un correo con un enlace para completarlo. La justificación es opcional y queda en el historial.',
      resaltar: [dialogo.getByLabel('Justificación (opcional)'), dialogo.getByRole('button', { name: 'Reabrir expediente' })],
    });
    await dialogo.getByRole('button', { name: 'Reabrir expediente' }).click();

    await expect(aviso).toBeHidden();
    await expect(app.getByText(/El tercero tiene hasta el .* para completar el expediente\./)).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Comprueba la reapertura',
      explicacion:
        'El aviso de caducidad desaparece y el expediente queda con el nuevo plazo indicado. El tercero recibe el correo de reapertura y ve de nuevo cuántos días le quedan para completarlo.',
      resaltar: app.getByText(/El tercero tiene hasta el .* para completar el expediente\./),
    });
    await esperarCorreo(invitacion.correo, new RegExp('reabrió tu validación'));
  },
);
