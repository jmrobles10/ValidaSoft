import { confirmarConPasskey, crearGrupoDirecto, entrarYRegistrarPasskey } from '../../soporte/ingreso';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'acceso',
    actor: 'Administrador de empresa',
    titulo: 'Elige cómo entran las personas de tu grupo y exige passkey',
    resumen:
      'Ana, como administradora del grupo, define en Configuración › Ingreso cómo entran las personas: desde el sistema integrado, directo a Validasoft con correo y código, o ambos. ' +
      'También elige si todas las personas deben registrar una passkey.',
    precondiciones: [
      'Un grupo con ingreso directo activo y Ana como administradora de todo el grupo, con su passkey registrada.',
    ],
    relacionados: ['acceso/ingreso-directo-por-correo', 'acceso/ingreso-con-passkey', 'acceso/sso-configurar', 'configuracion/plan-del-grupo'],
  },
  async ({ page }) => {
    const g = await crearGrupoDirecto();
    await entrarYRegistrarPasskey(page, g);

    await page.goto(`/app/#/e/${g.empresaId}/ingreso`);
    const titulo = page.getByRole('heading', { name: 'Ingreso', exact: true });
    await expect(titulo).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Cómo entran', exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Abre la política de ingreso del grupo',
      explicacion:
        'En Configuración › Ingreso eliges cómo entran las personas del grupo. «Desde el sistema integrado» es para quienes entran por tu sistema; «Directo a Validasoft» es para quienes entran con su correo y un código. ' +
        'Puedes admitir uno o ambos. Quien entra desde el sistema integrado no cambia aunque apagues el modo directo.',
      resaltar: page.getByRole('heading', { name: 'Cómo entran', exact: true }),
      configuracion: {
        ajuste: 'Cómo entran (modos de ingreso)',
        ruta: 'Configuración › Ingreso (/ingreso)',
        efecto: 'Define si las personas pueden entrar directo a Validasoft, desde el sistema integrado, o desde los dos.',
      },
      variantes: [
        { cuando: 'Apagas el ingreso directo', entonces: 'Las sesiones directas abiertas se cierran y la pantalla avisa de ese efecto antes de guardar.' },
        { cuando: 'Desactivas el modo «Desde el sistema integrado»', entonces: 'Las pantallas embebidas de tus sistemas dejan de abrir sesión.' },
      ],
    });

    await page.getByLabel('Exigir passkey a todas las personas del grupo').check();
    await expect(page.getByLabel('Exigir passkey a todas las personas del grupo')).toBeChecked();
    await paso(page, {
      titulo: 'Exige passkey a todas las personas',
      explicacion:
        'Activa «Exigir passkey a todas las personas del grupo» para que cada persona registre una passkey en su próximo ingreso. ' +
        'Los administradores, oficiales de cumplimiento y auditores ya la necesitan siempre; esta opción extiende la regla a revisores y demás personas.',
      resaltar: page.getByLabel('Exigir passkey a todas las personas del grupo'),
      configuracion: {
        ajuste: 'Exigir passkey a todas las personas del grupo',
        ruta: 'Configuración › Ingreso (/ingreso)',
        efecto: 'Cada persona debe registrar una passkey la próxima vez que ingrese directo.',
      },
    });

    await page.getByRole('button', { name: 'Guardar', exact: true }).click();
    if (await page.getByRole('dialog', { name: 'Confirma que eres tú' }).isVisible({ timeout: 2_000 }).catch(() => false)) await confirmarConPasskey(page);
    await expect(page.getByText('Política de ingreso guardada.')).toBeVisible();
    await paso(page, {
      titulo: 'Guarda la política de ingreso',
      explicacion:
        'Al pulsar «Guardar», la política queda aplicada al grupo. Si cambiaste algo sin guardar, la pantalla avisa «Tienes cambios sin guardar» hasta que lo hagas.',
      resaltar: page.getByText('Política de ingreso guardada.'),
    });
  },
);
