import { CLAVE_PLATAFORMA, llamar } from '../../soporte/api';
import { pdf } from '../../soporte/archivos';
import { esperarCorreo } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'operacion',
    actor: 'Administrador de empresa',
    titulo: 'Fija el plazo de revisión de cada área',
    resumen:
      'La administradora fija cuántas horas tiene cada área para revisar un expediente. Cuando el plazo se acerca o vence, el responsable recibe un aviso; la bandeja lo muestra y el informe de plazos mide el cumplimiento.',
    precondiciones: ['Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento, y un proveedor que ya entregó sus documentos y quedó en revisión.'],
    relacionados: ['operacion/delegacion-de-revision', 'revision/aprobar-un-area'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, 'plazos-de-revision');
    await expect(ana.getByRole('heading', { level: 1, name: 'Plazos de revisión' })).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Abre los plazos de revisión',
      explicacion:
        'En Configuración, «Plazos de revisión» define en horas el tiempo que tiene cada área para decidir un expediente que le llega. Cada área tiene su propio campo; el plazo corre desde que el expediente entra a revisión.',
      resaltar: ana.getByRole('textbox', { name: 'Plazo en horas de Compras' }),
      configuracion: {
        ajuste: 'Plazo de revisión por área',
        ruta: 'Configuración › Plazos de revisión (/plazos-de-revision)',
        efecto: 'Cuando se acerca o vence, el responsable recibe un aviso y la bandeja marca el plazo de la fila.',
      },
    });

    await ana.getByRole('textbox', { name: 'Plazo en horas de Compras' }).fill('10');
    await ana.getByRole('button', { name: 'Guardar cambios' }).click();
    await expect(ana.getByText('Plazos de revisión guardados.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Guarda el plazo del área',
      explicacion:
        'Con 10 horas en Compras, cada expediente que llegue a esa área tiene 10 horas para decidirse. Al guardar, Validasoft confirma el cambio y aplica el plazo a los expedientes que entren de ahí en adelante.',
      resaltar: [ana.getByRole('textbox', { name: 'Plazo en horas de Compras' }), ana.getByRole('button', { name: 'Guardar cambios' })],
      variantes: [{ cuando: 'Dejas vacío el plazo de un área', entonces: 'Esa área no tiene plazo y sus expedientes no generan avisos de vencimiento.' }],
    });

    const tercero = await (await browser.newContext()).newPage();
    const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros del Plazo S.A.S.'));
    await completarInvitacion(tercero, invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT plazo') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara plazo') },
    ]);

    const paginaRuben = await (await browser.newContext()).newPage();
    const ruben = await abrirEmbebido(paginaRuben, e, e.ruben, e.norte, 'bandeja');
    await expect(ruben.getByRole('columnheader', { name: 'Plazo de revisión' })).toBeVisible();
    await expect(ruben.getByRole('row').filter({ hasText: invitacion.nombre }).getByText('A tiempo', { exact: true })).toBeVisible();
    await paso(paginaRuben, {
      titulo: 'Mira el plazo en la bandeja',
      explicacion:
        'La bandeja tiene la columna «Plazo de revisión» con el estado de cada expediente: «A tiempo» mientras le queda plazo al área, y el indicador cambia cuando se acerca o vence. Quien revisa Compras ve el plazo de los expedientes de su área.',
      resaltar: [ruben.getByRole('columnheader', { name: 'Plazo de revisión' }), ruben.getByRole('row').filter({ hasText: invitacion.nombre })],
      variantes: [{ cuando: 'El plazo ya venció', entonces: 'La fila deja de decir «A tiempo» y el expediente aparece como vencido en el informe de plazos.' }],
    });

    // El tiempo pasa: el job de plazos corre con fechas simuladas solo para este grupo de prueba.
    const expedientes = await llamar<{ datos: { id: string; etapas: { areas: { codigo: string; slaVence: string | null }[] }[] }[] }>('GET', '/api/v1/expedientes', {
      llave: e.admin,
      empresa: e.norte.id,
    });
    const compras = expedientes.datos[0]!.etapas.flatMap((x) => x.areas).find((a) => a.codigo === 'COMPRAS' || a.codigo === 'compras');
    const vence = new Date(compras!.slaVence!);
    const inicio = new Date(vence.getTime() - 10 * 3600 * 1000);
    const aviso = await llamar<{ avisos: number }>('POST', '/api/v1/plataforma/sla/revisar', {
      llave: CLAVE_PLATAFORMA,
      cuerpo: { tenantId: e.tenantId, fecha: new Date(inicio.getTime() + 9 * 3600 * 1000).toISOString() },
    });
    expect(aviso.avisos).toBe(1);
    await esperarCorreo(e.ruben.correo, /^Plazo de revisión por vencer:/);
    const vencido = await llamar<{ incumplidos: number }>('POST', '/api/v1/plataforma/sla/revisar', {
      llave: CLAVE_PLATAFORMA,
      cuerpo: { tenantId: e.tenantId, fecha: new Date(inicio.getTime() + 11 * 3600 * 1000).toISOString() },
    });
    expect(vencido.incumplidos).toBe(1);
    await esperarCorreo(e.ana.correo, /^Plazo de revisión vencido:/);

    const paginaInforme = await (await browser.newContext()).newPage();
    const informe = await abrirEmbebido(paginaInforme, e, e.ana, e.norte, 'informes/sla');
    await expect(informe.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(informe.getByText('Compras', { exact: true }).first()).toBeVisible();
    await paso(paginaInforme, {
      titulo: 'Revisa el informe de cumplimiento de plazos',
      explicacion:
        'El informe de plazos de revisión agrupa por área cuántos expedientes se decidieron a tiempo y cuántos vencieron. Ana recibe el correo «Plazo de revisión vencido» cuando un área incumple; Rubén recibió antes el aviso de «Plazo de revisión por vencer».',
      resaltar: [informe.getByRole('heading', { level: 1 }), informe.getByText('Compras', { exact: true }).first()],
    });
  },
);
