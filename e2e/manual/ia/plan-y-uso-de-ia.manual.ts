import { abrirEmbebido } from '../../soporte/embebido';
import { activarIa, crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'ia',
    actor: 'Administrador de empresa',
    titulo: 'Revisa el plan y el uso de IA de la empresa',
    resumen:
      'La administradora consulta «Plan y uso de IA»: cuántas validaciones automáticas incluye el plan, cuántas ha usado este mes, si tiene validaciones adicionales y cuánto consumió. Esta pantalla no la ve el tercero.',
    precondiciones: [
      'Un grupo con Constructora Sur y el plan de IA contratado para la empresa, con 20 validaciones incluidas al mes y sin adicional. El proveedor de IA es simulado.',
    ],
    relacionados: ['ia/revision-automatica-niveles', 'ia/cupo-agotado'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur, 20);
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'uso-ia');
    await expect(ana.getByRole('heading', { level: 1, name: 'Plan y uso de IA' })).toBeVisible();
    await expect(ana.getByText('0 de 20', { exact: true })).toBeVisible();
    await expect(ana.getByText('Sin adicional', { exact: true })).toBeVisible();

    await paso(paginaAna, {
      titulo: 'Abre Plan y uso de IA',
      explicacion:
        'La pantalla muestra el plan de IA de la empresa para el mes elegido. «Validaciones incluidas» cuenta cuántos documentos se analizaron con el cupo del plan (por ejemplo, «0 de 20»), y debajo dice cuántas quedan. «Validaciones adicionales» dice si el plan permite comprar más cuando se agota el cupo.',
      resaltar: [ana.getByText('0 de 20', { exact: true }), ana.getByText('Sin adicional', { exact: true })],
      configuracion: {
        ajuste: 'Plan de IA de la empresa',
        ruta: 'Configuración › Plan y uso de IA (/uso-ia)',
        efecto: 'El plan lo configura Validasoft desde Plataforma; aquí solo se consulta el uso.',
      },
      variantes: [
        { cuando: 'La empresa no tiene plan de IA', entonces: 'La pantalla dice «La revisión automática no está contratada»: sus documentos se revisan manualmente.' },
        { cuando: 'Se agotaron las incluidas y no hay adicional', entonces: 'Los documentos nuevos pasan a revisión manual, sin análisis automático.' },
      ],
    });

    await paso(paginaAna, {
      titulo: 'Revisa cómo se cuenta una validación',
      explicacion:
        'Una validación es el análisis automático de un documento que recibe la empresa. Si otra empresa ya pagó el análisis de ese mismo documento, igual cuenta una validación de tu cupo. Los administradores reciben un aviso al llegar al 80 % y al 100 % de las incluidas.',
      resaltar: ana.getByText('Validaciones por día', { exact: true }).or(ana.getByText('Sin validaciones en este mes.')),
      avisos: false,
    });
  },
);
