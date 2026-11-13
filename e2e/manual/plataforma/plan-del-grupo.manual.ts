import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Fija el plan y los límites de un grupo',
    resumen:
      'El operador de Plataforma define el plan de un grupo: su código, los límites de empresas, usuarios y demás recursos, y si al llegar a un límite el grupo se bloquea o solo recibe un aviso. Al guardar, la pantalla muestra el uso actual frente a cada límite.',
    precondiciones: ['Un grupo sin plan, con dos empresas.', 'Un operador de Plataforma con la consola abierta.'],
    relacionados: ['plataforma/lista-de-grupos', 'plataforma/consumo-y-tarifa-de-ia'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const grupo = `Grupo E2E ${e.sufijo}`;

    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(grupo);
    await expect(consola.getByRole('region', { name: grupo })).toBeVisible();

    await paso(page, {
      titulo: 'Abre el plan del grupo',
      explicacion:
        'En la tarjeta del grupo, el enlace «Plan y límites» abre la pantalla del plan. Un grupo sin plan no tiene límites: solo se mide su uso. ' +
        'Desde aquí se fija el plan del grupo entero; el plan de IA se configura aparte, empresa por empresa.',
      resaltar: consola.getByRole('link', { name: `Plan y límites de ${grupo}` }),
      configuracion: {
        ajuste: 'Plan del grupo',
        ruta: 'Plataforma › Grupos y empresas › Plan y límites',
        efecto: 'Define los límites del grupo y si al llegar a uno se bloquea.',
      },
    });
    await consola.getByRole('link', { name: `Plan y límites de ${grupo}` }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Plan y límites' })).toBeVisible();
    await expect(consola.getByText('Este grupo aún no tiene plan', { exact: false })).toBeVisible();

    await consola.getByRole('textbox', { name: 'Código del plan' }).fill('empresarial');
    await consola.getByRole('textbox', { name: 'Empresas' }).fill('2');
    await consola.getByRole('textbox', { name: 'Usuarios' }).fill('5');
    await paso(page, {
      titulo: 'Define el código y los límites',
      explicacion:
        'El código del plan es un nombre corto, como «estándar» o «empresarial», que el grupo ve en su pantalla de plan. Cada límite cuenta lo que ya existe: empresas activas, personas activas, terceros vinculados, documentos recibidos, almacenamiento y llamadas a la API por mes. ' +
        'Deja un campo vacío para no poner límite a ese recurso. Validasoft avisa al 80 % y al 100 % de cada uno.',
      resaltar: [
        consola.getByRole('textbox', { name: 'Código del plan' }),
        consola.getByRole('textbox', { name: 'Empresas' }),
        consola.getByRole('textbox', { name: 'Usuarios' }),
      ],
      variantes: [
        { cuando: 'Un campo queda vacío', entonces: 'Ese recurso no tiene límite y se muestra como «Sin límite».' },
        {
          cuando: 'El límite de llamadas a la API se agota',
          entonces: 'Las escrituras con API key se rechazan; las lecturas siguen funcionando.',
        },
      ],
    });

    await paso(page, {
      titulo: 'Elige qué pasa al llegar a un límite',
      explicacion:
        'El interruptor «Bloquear al llegar al límite» decide el comportamiento. Apagado, al llegar a un límite solo se avisa a los administradores del grupo y el grupo puede seguir creando. ' +
        'Encendido, el grupo no puede crear más de ese recurso hasta que amplíes el plan; lo que ya existe sigue funcionando.',
      resaltar: consola.getByRole('switch', { name: 'Bloquear al llegar al límite' }),
      variantes: [
        { cuando: 'Interruptor apagado', entonces: 'Se avisa, pero no se bloquea nada.' },
        { cuando: 'Interruptor encendido', entonces: 'Al llegar al límite, las altas de ese recurso se rechazan con un mensaje que dice por qué.' },
      ],
      configuracion: {
        ajuste: 'Bloquear al llegar al límite',
        ruta: 'Plataforma › Grupos y empresas › Plan y límites',
        efecto: 'Decide si un límite alcanzado impide crear más o solo avisa.',
      },
    });
    await consola.getByRole('switch', { name: 'Bloquear al llegar al límite' }).check();
    await expect(consola.getByText('Al llegar a un límite, el grupo no podrá crear más de eso', { exact: false })).toBeVisible();

    await consola.getByRole('button', { name: 'Guardar el plan' }).click();
    await expect(consola.getByText('2 de 2', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda el plan y revisa el uso',
      explicacion:
        'Al guardar, el plan queda vigente para el grupo y la pantalla muestra su uso actual frente a cada límite. Aquí, el grupo ya tiene 2 de 2 empresas y aparece «En el límite»: con el bloqueo encendido, la siguiente empresa no se puede crear. ' +
        'Para ampliar el plan basta con subir el límite y guardar de nuevo.',
      resaltar: consola.getByText('En el límite', { exact: true }).first(),
      variantes: [
        { cuando: 'El uso supera el 80 % de un límite', entonces: 'Se avisa a los administradores del grupo antes de llegar al tope.' },
      ],
      configuracion: {
        ajuste: 'Uso de los límites',
        efecto: 'Se mide cada 15 minutos; los avisos llegan a los administradores del grupo.',
      },
    });
  },
);
