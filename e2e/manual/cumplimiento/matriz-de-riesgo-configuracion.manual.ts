import { crearEscenario } from '../../soporte/semilla';
import { abrirEmbebido } from '../../soporte/embebido';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Configura la matriz de riesgo SAGRILAFT de la empresa',
    resumen:
      'El oficial de cumplimiento ajusta la matriz de riesgo: cuánto pesa cada factor, cómo se clasifica el riesgo y qué debida diligencia exige cada nivel. Al guardarla, los próximos estudios de cumplimiento la usan; los ya registrados conservan su cálculo.',
    precondiciones: ['Un grupo con Constructora Norte, sin matriz propia, con la plantilla de la plataforma como base.'],
    relacionados: ['cumplimiento/listas-restrictivas-de-la-empresa'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, 'matriz-riesgo');
    await expect(ana.getByRole('heading', { level: 1, name: 'Matriz de riesgo' })).toBeVisible();
    await expect(ana.getByText('Aún no tienes una matriz propia')).toBeVisible();

    const sector = ana.getByRole('article', { name: 'Sector o actividad económica' });
    await paso(paginaAna, {
      titulo: 'Abre la matriz de riesgo',
      explicacion:
        'La matriz de riesgo SAGRILAFT tiene factores, como el sector o actividad económica y la jurisdicción del tercero. Cada factor dice si se calcula de forma «Manual» o «Automático». Mientras no guardes una matriz propia, la empresa usa la plantilla de la plataforma.',
      resaltar: sector,
      configuracion: {
        ajuste: 'Matriz de riesgo',
        ruta: 'Configuración › Matriz de riesgo (/matriz-riesgo)',
        efecto: 'Define el nivel de riesgo y la debida diligencia que recibe cada tercero en su estudio.',
      },
    });

    await sector.getByRole('textbox', { name: 'Peso' }).fill('25');
    await ana.getByRole('switch', { name: 'Calcular el riesgo con esta matriz' }).click();
    await expect(ana.getByText('Hay cambios sin guardar.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Ajusta el peso de un factor',
      explicacion:
        'El peso de cada factor dice cuánto influye en el riesgo del tercero. Puedes cambiarlo y, con el interruptor «Calcular el riesgo con esta matriz», decidir si el cálculo usa la matriz propia. Mientras haya cambios sin guardar, la pantalla lo indica.',
      resaltar: [sector.getByRole('textbox', { name: 'Peso' }), ana.getByText('Hay cambios sin guardar.')],
      variantes: [{ cuando: 'Quitas el cálculo con esta matriz', entonces: 'Los estudios nuevos no usan la matriz propia y siguen la plantilla de la plataforma.' }],
    });

    await ana.getByRole('button', { name: 'Guardar matriz' }).click();
    const confirmacion = ana.getByRole('dialog', { name: 'Guardar la matriz' });
    await expect(confirmacion).toContainText('Los próximos estudios usarán esta matriz; los ya registrados conservan su cálculo.');
    await paso(paginaAna, {
      titulo: 'Confirma el cambio de la matriz',
      explicacion:
        'El aviso dice que la matriz queda activa y que los próximos estudios la usarán. Los estudios que ya se registraron conservan el cálculo con el que se hicieron, para que el historial no cambie.',
      resaltar: confirmacion,
      completa: false,
    });
    await confirmacion.getByRole('button', { name: 'Guardar matriz' }).click();
    await expect(ana.getByText('Guardamos la matriz. Los próximos estudios la usan.')).toBeVisible();
    await expect(ana.getByText(/^Versión 1 · guardada el /)).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Comprueba la versión guardada',
      explicacion:
        'La matriz queda guardada como «Versión 1», con la fecha en que se guardó. Los cambios posteriores quedan como versiones nuevas.',
      resaltar: ana.getByText(/^Versión 1 · guardada el /),
      avisos: true,
    });
  },
);
