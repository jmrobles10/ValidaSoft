import { CLAVE_PLATAFORMA, llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Revisa el plan del grupo y su uso del mes',
    resumen:
      'La administradora abre Plan del grupo para ver qué permite el plan que fijó Plataforma, cuánto lleva usado el grupo y qué pasa al llegar a un límite. El grupo está en el límite de empresas.',
    precondiciones: [
      'Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.».',
      'Plataforma fijó al grupo el plan «empresarial» con límite de 2 empresas, 20 usuarios y bloqueo al llegar al límite.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/empresas-y-textos-legales'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    // Plataforma fija el plan del grupo (la misma llamada que hace la consola de Plataforma al guardar «Plan y límites»).
    await llamar('PUT', `/api/v1/plataforma/tenants/${e.tenantId}/plan`, {
      llave: CLAVE_PLATAFORMA,
      cuerpo: {
        codigo: 'empresarial',
        limites: { usuarios: 20, empresas: 2, vinculosActivos: null, almacenamientoMb: null, llamadasApiMes: null },
        bloqueoSuave: true,
      },
    });
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'plan-grupo');
    await expect(app.getByRole('heading', { level: 1, name: 'Plan del grupo' })).toBeVisible();
    await expect(app.getByText('empresarial', { exact: true })).toBeVisible();
    await expect(app.getByText('2 de 2', { exact: true })).toBeVisible();
    await expect(app.getByText('Llegaste al límite de tu plan', { exact: false })).toBeVisible();

    // 1. El plan y lo que pasa al llegar a un límite.
    await paso(page, {
      titulo: 'Abre Configuración › Plan del grupo',
      explicacion:
        'La tarjeta «Tu plan» muestra el código del plan que te asignó Plataforma y qué pasa al llegar a un límite. Con el bloqueo activo no se pueden crear más usuarios, empresas ni vínculos de lo que ya se agotó; lo que ya existe sigue funcionando. ' +
        'Sin bloqueo, Validasoft solo avisa a los administradores del grupo y se puede seguir trabajando.',
      resaltar: app.getByRole('heading', { name: 'Tu plan', exact: true }),
      configuracion: {
        ajuste: 'Plan del grupo',
        ruta: 'Configuración › Plan del grupo (/plan-grupo)',
        efecto: 'Los límites los fija Plataforma; esta pantalla solo muestra lo que permiten y cuánto se usa.',
      },
      variantes: [
        { cuando: 'Plataforma no le ha fijado plan al grupo', entonces: 'La tarjeta dice «Sin plan asignado»: no hay límites y solo se mide el uso.' },
        { cuando: 'El bloqueo al llegar al límite está apagado', entonces: 'La tarjeta dice que solo se avisa y el grupo puede seguir creando elementos.' },
      ],
    });

    // 2. El uso del mes contra el límite.
    await paso(page, {
      titulo: 'Revisa el uso contra los límites',
      explicacion:
        'Cada medidor muestra lo usado y su límite, con una barra. Usuarios, empresas y vínculos activos cuentan lo que hay hoy; el almacenamiento suma lo que el grupo recibe de sus terceros, y las llamadas a la API se cuentan en el mes. ' +
        'En el recuadro 1 el grupo tiene 2 de 2 empresas, y el recuadro 2 avisa «Llegaste al límite de tu plan»: para crear otra empresa, Plataforma tiene que ampliar el plan.',
      resaltar: [app.getByText('2 de 2', { exact: true }), app.getByText('Llegaste al límite de tu plan', { exact: false })],
      variantes: [
        { cuando: 'Un medidor llega al 80 %', entonces: 'Aparece el aviso «Estás cerca del límite de tu plan» con el medidor que va llegando.' },
        { cuando: 'Un medidor no tiene límite', entonces: 'Muestra solo lo usado con la frase «sin límite».' },
      ],
    });
  },
);
