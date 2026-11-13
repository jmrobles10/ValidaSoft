import { crearEscenario } from '../../soporte/semilla';
import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { expect, paso, recorrido } from '../nucleo';

const SISTEMA = 'Sistema integrado E2E';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Desactiva y reactiva un sistema integrado de un grupo',
    resumen:
      'El operador de Plataforma abre los sistemas integrados de un grupo y desactiva uno que ya no debe entrar a Validasoft. Desactivarlo corta de inmediato sus llaves y sus sesiones; reactivarlo no revive lo que ya se revocó.',
    precondiciones: ['Un grupo con un sistema integrado activo y su API key.', 'Un operador de Plataforma con la consola abierta.'],
    relacionados: ['plataforma/crear-grupo-y-empresa', 'integracion/api-keys', 'integracion/sistema-integrador'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const grupo = `Grupo E2E ${e.sufijo}`;

    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(grupo);
    await expect(consola.getByRole('region', { name: grupo })).toBeVisible();

    await paso(page, {
      titulo: 'Abre los sistemas integrados del grupo',
      explicacion:
        'Cada grupo tiene el botón «Sistemas» en su tarjeta. Desde allí ves los sistemas que el grupo integró con Validasoft: el sistema es el ERP o portal del cliente que autentica a sus personas y firma sus aserciones. ' +
        'El botón abre la lista para ese grupo; no afecta a los otros grupos.',
      resaltar: consola.getByRole('button', { name: `Ver los sistemas de ${grupo}` }),
      configuracion: {
        ajuste: 'Sistemas integrados del grupo',
        ruta: 'Plataforma › Grupos y empresas › Sistemas',
        efecto: 'Permite desactivar o reactivar cada sistema del grupo.',
      },
    });
    await consola.getByRole('button', { name: `Ver los sistemas de ${grupo}` }).click();

    const lista = consola.getByRole('dialog', { name: /Sistemas integrados/ });
    await expect(lista.getByText(SISTEMA, { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa los sistemas del grupo',
      explicacion:
        'La lista muestra cada sistema con su nombre, su fecha de creación y cuántos sitios pueden incrustar Validasoft. Desactivar un sistema cierra de inmediato el acceso de sus llaves y de las sesiones que emitió. ' +
        'Su historial se conserva, así que siempre se puede reactivar después.',
      resaltar: lista.getByRole('button', { name: `Desactivar ${SISTEMA}` }),
      variantes: [
        { cuando: 'El grupo no tiene sistemas', entonces: 'La lista dice «Este grupo no tiene sistemas integrados».' },
        { cuando: 'El sistema está desactivado', entonces: 'El botón cambia a «Reactivar» y el sistema deja de pedir sesiones.' },
      ],
      configuracion: {
        ajuste: 'Estado del sistema integrado',
        efecto: 'Un sistema desactivado no puede pedir sesiones y sus llaves dejan de servir ahora mismo.',
      },
    });

    await lista.getByRole('button', { name: `Desactivar ${SISTEMA}` }).click();
    const confirmar = consola.getByRole('dialog', { name: new RegExp(`^Desactivar ${SISTEMA}`) });
    await expect(confirmar).toBeVisible();
    await paso(page, {
      titulo: 'Confirma la desactivación',
      explicacion:
        'Antes de desactivar, Validasoft pide confirmación: el sistema deja de poder pedir sesiones y sus llaves dejan de servir en ese momento. Su historial no se borra. ' +
        'Si la desactivación es un error, puedes reactivarlo en la misma lista, aunque las sesiones anteriores no se recuperan.',
      resaltar: confirmar,
      configuracion: {
        ajuste: 'Desactivar un sistema integrado',
        efecto: 'Corta las llaves y las sesiones del sistema de inmediato.',
      },
    });

    await confirmar.getByRole('button', { name: 'Desactivar', exact: true }).click();
    await expect(consola.getByText(`${SISTEMA} quedó desactivado.`)).toBeVisible();
    await paso(page, {
      titulo: 'Verifica que el sistema quedó desactivado',
      explicacion:
        'Validasoft confirma con un aviso que el sistema quedó desactivado. En la lista, su botón ahora dice «Reactivar» y el sistema ya no puede abrir sesiones para las personas del grupo. ' +
        'Las llaves revocadas siguen revocadas, aunque el sistema se reactive.',
      resaltar: lista.getByRole('button', { name: `Reactivar ${SISTEMA}` }),
      avisos: true,
    });

    await lista.getByRole('button', { name: `Reactivar ${SISTEMA}` }).click();
    await consola.getByRole('dialog', { name: new RegExp(`^Reactivar ${SISTEMA}`) }).getByRole('button', { name: 'Reactivar', exact: true }).click();
    await expect(consola.getByText(`${SISTEMA} está activo de nuevo.`)).toBeVisible();
    await paso(page, {
      titulo: 'Reactiva el sistema',
      explicacion:
        'Al reactivarlo, el sistema vuelve a poder pedir sesiones con sus llaves vigentes. Las sesiones y los tokens de antes no se recuperan, y las llaves que se revocaron no vuelven: esas se emiten de nuevo desde el grupo.',
      resaltar: lista.getByRole('button', { name: `Desactivar ${SISTEMA}` }),
      avisos: true,
    });
  },
);
