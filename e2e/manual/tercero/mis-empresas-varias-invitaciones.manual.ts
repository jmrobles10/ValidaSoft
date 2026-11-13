import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, tarjeta } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Gestiona tus empresas cuando varias te invitaron',
    resumen:
      'Un proveedor invitado por dos empresas del grupo acepta ambas invitaciones. «Mis empresas» muestra una tarjeta por empresa y tipo de vínculo, cada una con su propio estado, y el tercero entra a una sola a la vez para revisar lo que le piden.',
    precondiciones: [
      'Dos empresas del mismo grupo, Constructora Norte S.A.S. y Constructora Sur S.A.S., con flujo de Proveedor publicado.',
      'Un mismo tercero invitado por ambas empresas, que aún no ha aceptado ninguna invitación.',
    ],
    relacionados: ['tercero/entrar-sin-invitacion', 'tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería La Esquina S.A.S.');
    const enNorte = await invitar(e, e.norte, tercero, 'PROVEEDOR');
    const enSur = await invitar(e, e.sur, tercero, 'PROVEEDOR');

    // 1. Acepta la primera invitación.
    await entrarPorInvitacion(page, enNorte);
    await aceptarInvitacion(page, enNorte);

    // 2. Acepta la segunda y vuelve a Mis empresas.
    await entrarPorInvitacion(page, enSur);
    await aceptarInvitacion(page, enSur);
    await page.goto('/empresas');
    await expect(page.getByRole('heading', { level: 1, name: 'Mis empresas' })).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true })).toBeVisible();
    const listaDeEmpresas = page.getByRole('list').filter({ has: page.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true }) });
    await paso(page, {
      titulo: 'Revisa tus empresas en «Mis empresas»',
      explicacion:
        '«Mis empresas» muestra una tarjeta por cada empresa que te invitó, con el nombre, el tipo de vínculo (Proveedor, Cliente…) y el estado de tu revisión con esa empresa. ' +
        'Cada empresa revisa por su cuenta: lo que te pida una no tiene que coincidir con lo que te pida la otra.',
      resaltar: listaDeEmpresas,
      variantes: [
        { cuando: 'La misma empresa te invita como proveedor y como cliente', entonces: 'Aparecen dos tarjetas de esa empresa, una por tipo de vínculo, y cada una tiene su revisión.' },
        { cuando: 'Una empresa tiene observaciones', entonces: 'Su tarjeta dice «Tienes observaciones» y ofrece «Ver observaciones»; las demás no cambian.' },
      ],
    });

    // 3. Abre una empresa y revisa sus requisitos.
    await abrirEmpresa(page, e.norte.nombre, 'PROVEEDOR');
    await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` })).toBeVisible();
    await paso(page, {
      titulo: 'Abre la revisión de una empresa',
      explicacion:
        'Al abrir la tarjeta entras a «Tu revisión con» esa empresa: la frase de estado, la fecha límite y lo que te pide. La otra empresa no aparece aquí y lo que le entregues a una tampoco se muestra a la otra.',
      resaltar: page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` }),
    });

    // 4. Sus documentos se guardan una sola vez.
    await expect(tarjeta(page, 'Registro Único Tributario')).toBeVisible();
    await paso(page, {
      titulo: 'Entiende que tu bóveda es una sola',
      explicacion:
        'Tus documentos viven en tu bóveda y se comparten con cada empresa que aceptaste. Si un documento ya lo tiene una empresa, otra no te lo vuelve a pedir: solo te pide lo que le falte. ' +
        'Cuando cambies un documento, las empresas que lo usan lo revisan de nuevo.',
      resaltar: tarjeta(page, 'Registro Único Tributario'),
    });
  },
);
