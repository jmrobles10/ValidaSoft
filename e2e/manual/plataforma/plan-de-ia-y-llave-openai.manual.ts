import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

// Llave de prueba obvia (no es una llave real de OpenAI): el analizador simulado de este entorno la acepta.
const LLAVE_DE_PRUEBA = 'sk-proj-e2e-prueba-manual-0001';
const ULTIMOS = LLAVE_DE_PRUEBA.slice(-4);

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Configura el plan de IA de una empresa y su llave de OpenAI',
    resumen:
      'El operador de Plataforma define el plan de IA de una empresa que aún no tiene uno: cuántas validaciones van incluidas, si se permiten adicionales y con qué tope, y la llave de OpenAI con la que se paga el análisis. La llave se guarda cifrada y no vuelve a verse en claro.',
    precondiciones: ['Un grupo con una empresa sin plan de IA.', 'Un operador de Plataforma con la consola abierta.'],
    relacionados: ['plataforma/crear-grupo-y-empresa', 'plataforma/consumo-y-tarifa-de-ia'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const grupo = `Grupo E2E ${e.sufijo}`;

    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(grupo);
    await expect(consola.getByRole('region', { name: grupo })).toBeVisible();

    await paso(page, {
      titulo: 'Abre el plan de IA de la empresa',
      explicacion:
        'En la tabla del grupo, cada empresa tiene su «Plan de IA». Una empresa sin plan revisa sus documentos a mano; con plan, los analiza con IA. ' +
        'El plan es de la empresa, no del grupo: las demás empresas del mismo grupo conservan el suyo.',
      resaltar: consola.getByRole('link', { name: 'Plan de IA de Constructora Norte S.A.S.' }),
      configuracion: {
        ajuste: 'Plan de IA por empresa',
        ruta: 'Plataforma › Grupos y empresas › Plan de IA',
        efecto: 'Define si la empresa usa IA para revisar documentos, cuánto va incluido y con qué llave se paga.',
      },
    });
    await consola.getByRole('link', { name: 'Plan de IA de Constructora Norte S.A.S.' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Plan de IA' })).toBeVisible();
    await expect(consola.getByText('Esta empresa aún no tiene plan', { exact: false })).toBeVisible();

    await consola.getByRole('textbox', { name: 'Validaciones incluidas por mes' }).fill('300');
    await consola.getByLabel('Permitir validaciones adicionales').check();
    await consola.getByRole('textbox', { name: 'Tope de adicionales por mes' }).fill('100');
    await paso(page, {
      titulo: 'Define el cupo mensual',
      explicacion:
        'Las validaciones incluidas son los análisis de IA que van en el plan cada mes. Al agotarlas, la empresa puede seguir validando con IA solo si permites adicionales; esos se cobran aparte. ' +
        'El tope limita cuántos adicionales puede consumir en el mes. Vacío significa sin tope.',
      resaltar: [
        consola.getByRole('textbox', { name: 'Validaciones incluidas por mes' }),
        consola.getByLabel('Permitir validaciones adicionales'),
        consola.getByRole('textbox', { name: 'Tope de adicionales por mes' }),
      ],
      variantes: [
        { cuando: 'No permites adicionales', entonces: 'El resumen del plan dice «No se permiten» y no hay validaciones con IA fuera de las incluidas.' },
        { cuando: 'Permites adicionales sin tope', entonces: 'El campo de tope se deja vacío y el resumen dice «Permitidos, sin tope».' },
      ],
      configuracion: {
        ajuste: 'Validaciones incluidas y adicionales',
        ruta: 'Plataforma › Grupos y empresas › Plan de IA',
        efecto: 'Fija el cupo mensual de análisis con IA de la empresa y lo que se cobra fuera del plan.',
      },
    });

    const campoLlave = consola.getByLabel('Nueva API key de OpenAI');
    await expect(campoLlave).toHaveAttribute('type', 'password');
    await campoLlave.fill(LLAVE_DE_PRUEBA);
    await consola.getByRole('button', { name: 'Probar la nueva llave' }).click();
    await expect(consola.getByText(`OpenAI aceptó la llave ••••${ULTIMOS}.`)).toBeVisible();
    await paso(page, {
      titulo: 'Pega la llave de OpenAI y pruébala',
      explicacion:
        'La llave es la de la organización de OpenAI de esta empresa: con ella se paga el análisis. El campo la oculta mientras escribes y, al guardar, se cifra: no se vuelve a mostrar, ni a ti ni a nadie. ' +
        '«Probar la nueva llave» consulta a OpenAI sin consumir tokens, solo cuando lo pides, y te dice si la acepta o si no tiene cuota.',
      resaltar: [consola.getByLabel('Nueva API key de OpenAI'), consola.getByRole('button', { name: 'Probar la nueva llave' })],
      variantes: [
        { cuando: 'OpenAI rechaza la llave', entonces: 'La pantalla dice «OpenAI rechazó la llave» con sus últimos cuatro caracteres.' },
        { cuando: 'La llave no tiene cuota', entonces: 'La pantalla dice que la llave no tiene cuota o superó su límite.' },
      ],
      configuracion: {
        ajuste: 'API key de OpenAI',
        efecto: 'Es la llave con la que la empresa paga sus análisis; si no hay llave, no se analiza con IA.',
      },
    });

    await consola.getByRole('button', { name: 'Guardar plan' }).click();
    await expect(consola.getByText(`Configurada · ••••${ULTIMOS}`)).toBeVisible();
    await expect(consola.getByLabel('Nueva API key de OpenAI')).toHaveCount(0);
    await paso(page, {
      titulo: 'Guarda el plan',
      explicacion:
        'Al guardar, el plan queda activo y la llave aparece como «Configurada» con sus últimos cuatro caracteres. El campo para escribir una llave nueva desaparece: la llave guardada no se muestra completa en ningún lugar de la consola. ' +
        'Para reemplazarla, usa «Reemplazar»; para quitarla, «Quitar», y la empresa deja de pagar análisis con IA.',
      resaltar: consola.getByText(`Configurada · ••••${ULTIMOS}`),
      variantes: [
        { cuando: 'El plan está inactivo', entonces: 'La empresa no paga análisis con su llave y sus documentos se revisan manualmente.' },
      ],
      configuracion: {
        ajuste: 'Plan activo',
        efecto: 'Inactivo, la empresa no paga análisis con su llave aunque la tenga guardada.',
      },
    });

    await consola.getByRole('button', { name: 'Probar llave' }).click();
    await expect(consola.getByText(`OpenAI aceptó la llave ••••${ULTIMOS}.`)).toBeVisible();
    await paso(page, {
      titulo: 'Prueba la llave guardada',
      explicacion:
        'Puedes probar la llave que ya está guardada sin volver a escribirla. Validasoft la prueba contra OpenAI y muestra el resultado con sus últimos cuatro caracteres. ' +
        'La prueba no consume tokens y solo se hace cuando la pides.',
      resaltar: consola.getByRole('button', { name: 'Probar llave' }),
    });
  },
);
