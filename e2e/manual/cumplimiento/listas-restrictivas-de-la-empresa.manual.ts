import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Administrador de empresa',
    titulo: 'Elige con qué proveedor se consultan las listas restrictivas',
    resumen:
      'La administradora revisa con qué proveedor consulta la empresa las listas restrictivas: la cuenta de la plataforma, si está disponible, y la cuota mensual de búsquedas de la empresa, con su consumo del mes.',
    precondiciones: ['Un grupo con Constructora Norte con la cuenta de Infolaft de la plataforma disponible y sin cuota propia.'],
    relacionados: ['cumplimiento/matriz-de-riesgo-configuracion'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, 'listas-restrictivas');
    await expect(ana.getByRole('heading', { level: 1, name: 'Listas restrictivas' })).toBeVisible();
    await expect(ana.getByText('Proveedor de la empresa', { exact: true }).first()).toBeVisible();

    await paso(paginaAna, {
      titulo: 'Abre la configuración de listas restrictivas',
      explicacion:
        'Aquí se ve con qué proveedor consulta la empresa las listas restrictivas (por ejemplo, Infolaft) y cuánto ha consumido este mes. La mayoría de las empresas no necesita usuario ni clave: Validasoft contrata el proveedor para todas las empresas.',
      resaltar: ana.getByText('Proveedor de la empresa', { exact: true }).first(),
      configuracion: {
        ajuste: 'Proveedor de listas restrictivas',
        ruta: 'Configuración › Listas restrictivas (/listas-restrictivas)',
        efecto: 'Las consultas de cada estudio de cumplimiento se hacen con el proveedor que quede activo.',
      },
      variantes: [
        { cuando: 'La cuenta de la plataforma no está disponible', entonces: 'Aparece «No disponible» y las consultas quedan pendientes hasta configurarla.' },
        { cuando: 'Prefieres tu propia cuenta', entonces: 'Puedes guardar usuario y clave propios; la clave no se vuelve a mostrar.' },
      ],
    });

    await paso(paginaAna, {
      titulo: 'Revisa la cuota mensual y el consumo',
      explicacion:
        'La cuota mensual de la empresa dice cuántas búsquedas puede hacer en el mes; al llegar a ese tope no se consulta más hasta el mes siguiente. «Sin tope» significa que no hay límite propio. El consumo del mes muestra cuántas búsquedas se hicieron.',
      resaltar: ana.getByText('Cuota mensual de la empresa').or(ana.getByText('Sin tope')).first(),
    });
  },
);
