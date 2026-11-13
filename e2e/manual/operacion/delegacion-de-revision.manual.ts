import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { escribirFecha } from '../../soporte/fechas';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

/** Fecha ISO de hoy en Bogotá más `dias`. */
function hoyMas(dias: number): string {
  const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
  const [a, m, d] = hoy.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

recorrido(
  {
    dominio: 'operacion',
    actor: 'Revisor/área',
    titulo: 'Delega tu revisión mientras estás ausente',
    resumen:
      'Rubén, revisor de Compras, delega su revisión en Olga por unas fechas de vacaciones. Olga ve en su bandeja los expedientes de Compras durante ese tiempo, y Rubén puede revocar la delegación cuando quiera.',
    precondiciones: [
      'Un grupo con Constructora Norte, Rubén como miembro del área Compras y Olga como oficial, y un proveedor que ya entregó sus documentos y quedó en revisión.',
    ],
    relacionados: ['operacion/plazos-de-revision'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros de la Delegación S.A.S.'));
    const tercero = await (await browser.newContext()).newPage();
    await completarInvitacion(tercero, invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT delegacion') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara delegacion') },
    ]);

    const paginaRuben = await (await browser.newContext()).newPage();
    const ruben = await abrirEmbebido(paginaRuben, e, e.ruben, e.norte, 'delegaciones');
    await expect(ruben.getByRole('heading', { level: 1, name: 'Delegación de revisión' })).toBeVisible();
    await expect(ruben.getByText('Nadie revisa en lugar de otra persona por ahora.')).toBeVisible();
    await paso(paginaRuben, {
      titulo: 'Abre la delegación de revisión',
      explicacion:
        'En «Delegación de revisión» ves quién revisa en tu lugar. Si no hay ninguna delegación vigente, la lista lo dice. «Delegar mi revisión» crea una delegación para que otra persona revise tu área entre dos fechas.',
      resaltar: ruben.getByRole('button', { name: 'Delegar mi revisión' }).first(),
      configuracion: {
        ajuste: 'Delegación de revisión',
        ruta: 'Configuración › Delegación de revisión (/delegaciones)',
        efecto: 'Mientras la delegación esté vigente, la persona delegada ve y decide los expedientes de tu área.',
      },
    });

    await ruben.getByRole('button', { name: 'Delegar mi revisión' }).first().click();
    const dialogo = ruben.getByRole('dialog', { name: 'Delegar mi revisión' });
    await dialogo.getByRole('combobox', { name: 'Quién revisa en su lugar' }).click();
    await ruben.getByRole('option', { name: /Olga Oficial/ }).click();
    await escribirFecha(paginaRuben, 'Desde', hoyMas(0), dialogo);
    await escribirFecha(paginaRuben, 'Hasta', hoyMas(5), dialogo);
    await dialogo.getByLabel('Motivo (opcional)').fill('Vacaciones');
    await expect(dialogo.getByText(/Olga Oficial revisará en tu lugar/)).toBeVisible();
    await expect(dialogo.getByText(/separación de funciones y de conflicto de intereses se le siguen aplicando/)).toBeVisible();
    await paso(paginaRuben, {
      titulo: 'Elige quién revisa y las fechas',
      explicacion:
        'Eliges a la persona que revisa en tu lugar el rango de fechas (hasta 366 días) y, si quieres, las áreas que delegas: sin elegir ninguna, delegas todas las tuyas. El motivo es opcional y queda en el historial. Antes de crear la delegación, el diálogo dice quién revisará y recuerda que la separación de funciones se siguen aplicando a la persona delegada.',
      resaltar: [dialogo.getByRole('combobox', { name: 'Quién revisa en su lugar' }), dialogo.getByLabel('Motivo (opcional)')],
    });

    await dialogo.getByRole('button', { name: 'Delegar', exact: true }).click();
    await expect(ruben.getByText(/Delegación creada: Olga Oficial revisa en tu lugar/)).toBeVisible();
    await expect(ruben.getByRole('row').filter({ hasText: 'Olga Oficial' }).getByText('Vigente', { exact: true })).toBeVisible();
    await paso(paginaRuben, {
      titulo: 'Comprueba la delegación vigente',
      explicacion:
        'La fila queda como «Vigente» con las fechas que elegiste. Desde ese momento Olga recibe en su bandeja los expedientes de Compras, y tú los sigues viendo como siempre.',
      resaltar: ruben.getByRole('row').filter({ hasText: 'Olga Oficial' }),
    });

    const paginaOlga = await (await browser.newContext()).newPage();
    const olga = await abrirEmbebido(paginaOlga, e, e.olga, e.norte, 'bandeja');
    await expect(olga.getByRole('row').filter({ hasText: invitacion.nombre }).getByText('Compras', { exact: true })).toBeVisible();
    await paso(paginaOlga, {
      titulo: 'Revisa el área delegada en tu bandeja',
      explicacion:
        'Olga ve el expediente de Compras en su bandeja, aunque Compras no sea su área. Lo decide como revisora delegada,.',
      resaltar: olga.getByRole('row').filter({ hasText: invitacion.nombre }),
      variantes: [{ cuando: 'La delegación ya venció o fue revocada', entonces: 'El expediente deja de aparecer en la bandeja de la persona delegada.' }],
    });

    await ruben.getByRole('button', { name: 'Revocar la delegación de Rubén Revisor a Olga Oficial' }).click();
    const revocar = ruben.getByRole('dialog', { name: 'Revocar delegación' });
    await paso(paginaRuben, {
      titulo: 'Revoca la delegación',
      explicacion:
        'Al revocarla, Olga deja de ver los expedientes de tu área. Úsala si cambian tus fechas de ausencia antes de que termine el rango.',
      resaltar: revocar,
      completa: false,
    });
    await revocar.getByRole('button', { name: 'Revocar delegación' }).click();
    await expect(ruben.getByText('Delegación revocada.')).toBeVisible();
    await paginaOlga.reload();
    await expect(olga.getByRole('row').filter({ hasText: invitacion.nombre }).getByText('Compras', { exact: true })).toHaveCount(0);
  },
);
