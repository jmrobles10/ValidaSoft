import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Completa tu información paso a paso',
    resumen:
      'En «Mi información» ves cuántos datos te faltan en cada sección. Puedes guardar un borrador sin completarla, pero solo se puede continuar cuando la sección queda completa. La barra de abajo te dice en todo momento qué falta.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y un flujo de Proveedor que pide RUT, Cámara de comercio y las seis secciones de la ficha.',
      'Un tercero jurídico invitado por Sur que aceptó la invitación y tiene su ficha sin diligenciar, salvo la razón social que llega prellenada.',
    ],
    relacionados: ['tercero/ficha-secciones-con-soporte', 'tercero/ficha-actualizar-desde-mi-rut'],
  },
  async ({ page }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Ferretería La Ficha S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);

    // 1. El resumen de «Mi información».
    await page.goto('/ficha');
    await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
    const panel = page.getByRole('navigation', { name: 'Secciones de mi información' });
    await expect(page.getByText('Sin diligenciar').first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el avance de tu información',
      explicacion:
        'En «Mi información» cada sección muestra su estado: «Sin diligenciar», «Faltan N datos» o «Completa». El botón «Continuar» te lleva a la primera sección pendiente. ' +
        'Las secciones se completan en orden desde el panel de la izquierda; cada una se guarda por separado.',
      resaltar: page.getByRole('main'),
    });

    // 2. La barra de la sección General.
    await page.getByRole('button', { name: 'Continuar' }).or(page.getByRole('link', { name: 'Continuar' })).first().click();
    await expect(page).toHaveURL(/\/ficha\/general/);
    const barra = page.getByRole('group', { name: 'Guardar la sección' });
    await expect(page.getByRole('textbox', { name: 'Razón social' })).toHaveValue(tercero.nombre);
    await expect(barra.getByText(/Faltan? \d+ datos? para continuar/)).toBeVisible();
    await expect(barra.getByRole('button', { name: 'Guardar y continuar' })).toBeDisabled();
    await expect(barra.getByRole('button', { name: 'Guardar', exact: true })).toBeEnabled();
    await paso(page, {
      titulo: 'Lee cuánto te falta para continuar',
      explicacion:
        'La razón social ya viene escrita porque la tomamos de tu registro. La barra de abajo dice cuántos datos faltan para continuar y el botón «Guardar y continuar» se activa solo cuando la sección está completa. ' +
        'Mientras tanto puedes usar «Guardar»: queda como borrador y no te deja avanzar.',
      resaltar: barra,
    });

    // 3. Guardar un borrador con datos pendientes.
    await page.getByRole('textbox', { name: 'Teléfono' }).fill('601 555 1234');
    await expect(barra.getByText('Tienes cambios sin guardar')).toBeVisible();
    await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(page.getByText(/Para completar esta sección faltan? \d+ datos?/)).toBeVisible();
    await expect(page).toHaveURL(/\/ficha\/general/);
    await expect(panel.getByRole('link', { name: /^Información general: Faltan? \d+ datos?$/ })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda un borrador con lo que falta',
      explicacion:
        'Con «Guardar» tu avance queda en el borrador y la página te dice cuántos datos faltan, con el mensaje «Para completar esta sección faltan N datos». ' +
        'No avanzas a la siguiente sección hasta completar esta. En el panel, la sección muestra «Faltan N datos» para que la retomes después.',
      resaltar: [page.getByText(/Para completar esta sección faltan? \d+ datos?/), panel],
    });

    // 4. De vuelta en el resumen.
    await panel.getByRole('link', { name: 'Mi información' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
    await expect(page.getByText(/Faltan? \d+ datos?/).first()).toBeVisible();
    await paso(page, {
      titulo: 'Vuelve a tu información cuando quieras',
      explicacion:
        'Desde el resumen de «Mi información» ves de nuevo qué secciones están en borrador y cuántos datos les faltan. Puedes salir y volver: lo guardado no se pierde.',
      resaltar: page.getByText(/Faltan? \d+ datos?/).first(),
    });
  },
);

