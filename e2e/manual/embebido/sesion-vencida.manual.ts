import { abrirEmbebidoCon, sesionDeUsuario } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'embebido',
    actor: 'Revisor/área',
    titulo: 'Renueva una sesión que venció sin perder lo que estabas haciendo',
    resumen:
      'La sesión de la app dura 30 minutos. Dos minutos antes de que venza, la app pide una sesión nueva a tu sistema; si el sistema no responde a tiempo, la app avisa «Tu sesión venció» ' +
      'y vuelve a pedirla. Lo que Rubén estaba mirando no se pierde.',
    precondiciones: [
      'Un grupo con Constructora Norte y Rubén como miembro del área Compras, abierto dentro de la página del sistema integrado.',
      'El reloj de la prueba se adelanta para no esperar 30 minutos reales.',
    ],
    relacionados: ['embebido/app-de-la-empresa-en-un-iframe', 'integracion/identidad-delegada'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await page.clock.install();

    // Las pedidas 1 y 2 responden al momento; desde la 3 el sistema «tarda» hasta que la prueba lo libera, para ver la pantalla de espera.
    let pedidas = 0;
    let liberar: () => void = () => undefined;
    const retenidas = new Promise<void>((resolver) => {
      liberar = resolver;
    });
    const sesion = async () => {
      pedidas += 1;
      if (pedidas >= 3) await retenidas;
      return sesionDeUsuario(e.llave, e.delegado(e.ruben));
    };

    const app = await abrirEmbebidoCon(page, { empresaId: e.norte.id, rutaInicial: 'bandeja', sesion });
    const titulo = app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' });
    await expect(titulo).toBeVisible();

    await page.clock.fastForward('28:30');
    await expect(titulo).toBeVisible();
    await paso(page, {
      titulo: 'Deja que la app renueve la sesión antes de vencer',
      explicacion:
        'La sesión dura 30 minutos. Dos minutos antes de que venza, la app pide una sesión nueva a tu sistema, y el sistema la entrega sin que Rubén haga nada. ' +
        'Por eso la pantalla sigue igual: el cambio es invisible mientras el sistema responda.',
      resaltar: titulo,
    });

    await page.clock.fastForward('28:30');
    await page.clock.fastForward('02:00');
    const aviso = app.getByText('Tu sesión venció', { exact: true });
    await expect(aviso).toBeVisible();
    await paso(page, {
      titulo: 'Avisa que la sesión venció',
      explicacion:
        'Si la sesión vence y el sistema aún no entrega una nueva, la app muestra «Tu sesión venció». La explicación indica que ya se pidió una sesión nueva al sistema y que, si no llega, hay que volver a abrir Validasoft desde allí. ' +
        'No se pierde lo que se guardó: al entregar una sesión nueva, la app continúa donde estaba.',
      resaltar: aviso,
      variantes: [
        { cuando: 'El sistema tarda más de 10 segundos en entregar la sesión', entonces: 'La app sigue en espera y pide volver a abrirla desde el sistema.' },
        { cuando: 'El sistema rechaza la sesión', entonces: 'La app muestra «No pudimos validar tu sesión» en lugar de renovarla.' },
      ],
    });

    // El token nuevo lo firma la API con la hora real: el reloj de la página vuelve a la hora real (los temporizadores siguen corriendo).
    await page.clock.setFixedTime(new Date());
    liberar();
    await expect(titulo).toBeVisible();
    await expect(app.getByText('Tu sesión venció', { exact: true })).toHaveCount(0);
    await paso(page, {
      titulo: 'Vuelve a la pantalla con la sesión nueva',
      explicacion:
        'Cuando el sistema entrega la sesión nueva, el aviso desaparece y la app sigue en la misma pantalla. Rubén no tiene que ingresar de nuevo ni volver a abrir el expediente.',
      resaltar: titulo,
    });
  },
);
