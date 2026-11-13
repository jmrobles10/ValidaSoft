import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { Receptor } from '../../soporte/receptor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'integracion',
    actor: 'Administrador de empresa',
    titulo: 'Revisa una entrega fallida y la reenvía cuando el sistema vuelve',
    resumen:
      'Tu sistema responde con error y Validasoft deja la entrega en reintento, con su próximo intento. Cuando el sistema se recupera, la administradora reenvía la entrega desde la pantalla y llega firmada otra vez.',
    precondiciones: [
      'Un grupo con dos empresas y un webhook de la empresa Sur que pide el evento de vínculo creado.',
      'Un sistema integrado de prueba que responde 500 hasta que se lo indiquemos.',
    ],
    relacionados: ['integracion/webhooks'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const receptor = await Receptor.iniciar();
    try {
      receptor.fallar = true;
      const [sistema] = await llamar<{ id: string }[]>('GET', '/api/v1/sistemas', { llave: e.admin });
      await llamar('POST', '/api/v1/webhooks', {
        llave: e.admin,
        cuerpo: { url: receptor.url, tipos: ['vinculo.creado'], empresas: [e.sur.id], sistemaId: sistema?.id, descripcion: 'ERP Sur' },
      });
      const app = await abrirEmbebido(page, e, e.ana, e.sur, 'webhooks');
      await expect(app.getByRole('heading', { level: 1, name: 'Webhooks' })).toBeVisible();

      const invitacion = await invitar(e, e.sur, nuevoTercero('Vidrios Templados S.A.S.'));
      await receptor.esperar((x) => x.tipo === 'vinculo.creado' && x.datos.vinculoId === invitacion.vinculoId);

      await app.getByRole('table', { name: 'Webhooks' }).getByRole('row').nth(1).click();
      const fila = app.getByRole('table', { name: 'Entregas recientes' }).getByRole('row', { name: /^Ver intentos de vinculo\.creado/ });
      await expect(fila).toContainText('Reintentando', { timeout: 30_000 });
      await expect(fila).toContainText('Próximo intento:');

      await paso(page, {
        titulo: 'Encuentra la entrega que no llegó',
        explicacion:
          'Tu sistema respondió con un error, así que la entrega queda en «Reintentando»: Validasoft cuenta el intento y muestra la hora del próximo. ' +
          'Si tu sistema sigue fallando, Validasoft sigue reintentando. Si fallan 20 entregas seguidas, el webhook se desactiva por fallos y los eventos de ese tiempo no se reenvían solos.',
        resaltar: [app.getByRole('table', { name: 'Entregas recientes' }), fila],
        variantes: [
          { cuando: 'La entrega dice «Reintentando»', entonces: 'Validasoft lo volverá a intentar en la hora que muestra.' },
          { cuando: 'La entrega dice «Fallida»', entonces: 'Ya no se reintenta sola: solo puedes reenviarla a mano.' },
        ],
      });

      await fila.getByRole('button', { name: /Ver intentos/ }).click();
      await paso(page, {
        titulo: 'Mira qué respondió tu sistema',
        explicacion:
          'Cada intento muestra su resultado (fallido aquí), la hora, el detalle de la respuesta y el tiempo que tardó. ' +
          'Con eso ves si el problema es de tu sistema (un 500 o un tiempo de espera de más de 5 segundos) o de la dirección registrada.',
        resaltar: fila,
      });

      receptor.fallar = false;
      const antes = receptor.entregas.filter((x) => x.tipo === 'vinculo.creado').length;
      await fila.getByRole('button', { name: 'Reenviar' }).click();
      await expect(app.getByText('Reenvío en curso: lo intentamos ahora.')).toBeVisible();
      await paso(page, {
        titulo: 'Reenvía la entrega cuando tu sistema se recupere',
        explicacion:
          'Cuando tu sistema vuelve a responder, pulsa «Reenviar» en la entrega. Validasoft la intenta en ese momento y el aviso confirma que el reenvío está en curso.',
        resaltar: fila.getByRole('button', { name: 'Reenviar' }),
        avisos: true,
      });

      await expect.poll(() => receptor.entregas.filter((x) => x.tipo === 'vinculo.creado').length, { timeout: 30_000 }).toBeGreaterThan(antes);
      await expect(fila).toContainText('Entregada', { timeout: 30_000 });
      await paso(page, {
        titulo: 'Confirma que la entrega quedó entregada',
        explicacion:
          'La fila pasa a «Entregada» y el intento exitoso queda marcado como manual, porque lo disparaste tú. ' +
          'La entrega llega firmada con el secreto vigente del webhook, así que tu sistema puede verificarla igual que las anteriores.',
        resaltar: fila,
      });

    } finally {
      await receptor.cerrar();
    }
  },
);
