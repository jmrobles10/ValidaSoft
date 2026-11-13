import { abrirEmbebido } from '../../soporte/embebido';
import { Receptor } from '../../soporte/receptor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

const DESCRIPCION = 'ERP producción';

recorrido(
  {
    dominio: 'integracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea un webhook, guarda su secreto y revisa las entregas firmadas',
    resumen:
      'La administradora registra la dirección de su sistema para que Validasoft le envíe cada evento con un POST firmado. Guarda el secreto una sola vez, y luego revisa en la pantalla las entregas, con sus intentos.',
    precondiciones: [
      'Un grupo empresarial con dos empresas y una administradora que puede administrar webhooks.',
      'Una dirección local que recibe las entregas (el «sistema integrado» de prueba, en su propio puerto).',
    ],
    relacionados: ['integracion/webhook-entrega-fallida-y-reenvio', 'integracion/eventos-pull-y-en-vivo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const receptor = await Receptor.iniciar();
    try {
      const app = await abrirEmbebido(page, e, e.ana, e.norte, 'webhooks');
      await expect(app.getByRole('heading', { level: 1, name: 'Webhooks' })).toBeVisible();

      await paso(page, {
        titulo: 'Abre Configuración › Webhooks',
        explicacion:
          'Un webhook es la dirección de tu sistema a la que Validasoft envía un aviso cada vez que pasa algo: un vínculo activo, un expediente aprobado, un certificado emitido. ' +
          'La tabla lista los webhooks del grupo con su destino, cuántos eventos reciben, su estado y la hora de la última entrega. Para registrar uno, pulsa «Nuevo webhook».',
        resaltar: [app.getByRole('table', { name: 'Webhooks' }), app.getByRole('button', { name: 'Nuevo webhook' })],
        configuracion: {
          ajuste: 'Webhooks',
          ruta: 'Configuración › Webhooks (/webhooks)',
          efecto: 'Cada webhook recibe solo los eventos y las empresas que marques.',
        },
      });

      await app.getByRole('button', { name: 'Nuevo webhook' }).click();
      const drawer = app.locator('[aria-labelledby="titulo-webhook"]');
      await drawer.getByRole('textbox', { name: /^Dirección/ }).fill(receptor.url);
      await drawer.getByRole('textbox', { name: /^Descripción/ }).fill(DESCRIPCION);
      await paso(page, {
        titulo: 'Escribe la dirección que recibe los eventos',
        explicacion:
          'La dirección debe ser https y responder con 2xx en menos de 5 segundos; si tu sistema tarda, procesa después y responde rápido. ' +
          'La descripción es opcional y sirve para reconocerlo en la lista, por ejemplo «ERP producción». En desarrollo se admite http solo hacia localhost.',
        resaltar: [drawer.getByRole('textbox', { name: /^Dirección/ }), drawer.getByRole('textbox', { name: /^Descripción/ })],
        variantes: [
          { cuando: 'La dirección es http hacia otro sitio', entonces: 'Se rechaza: usa https.' },
          { cuando: 'Dejas la descripción vacía', entonces: 'El webhook se identifica por su dirección.' },
        ],
      });

      await paso(page, {
        titulo: 'Elige qué eventos recibe y de qué empresas',
        explicacion:
          'Marca los eventos que quieres recibir, agrupados por vínculos, expedientes, documentos, certificados, información del tercero e invitaciones. Si marcas «Todos los eventos», también recibirá los que se agreguen después. ' +
          'Luego elige si el webhook sigue a todas las empresas del grupo o solo a algunas; un webhook sin evento ni empresa no se puede guardar.',
        resaltar: [drawer.getByRole('group', { name: /Qué eventos/ }), drawer.getByRole('radiogroup', { name: /De qué empresas/ })],
        variantes: [
          { cuando: 'Marcas «Todos los eventos»', entonces: 'Recibirá también los eventos nuevos que Validasoft agregue más adelante.' },
          { cuando: 'Eliges solo algunos eventos', entonces: 'Los demás no se envían; un evento no marcado no llega nunca a esa dirección.' },
        ],
      });

      await drawer.getByRole('button', { name: 'Crear webhook' }).click();
      const secreto = app.getByRole('dialog', { name: 'Secreto de firma' });
      await expect(secreto).toBeVisible();
      await paso(page, {
        titulo: 'Copia el secreto de firma: se muestra una vez',
        explicacion:
          'Al crear el webhook aparece su secreto de firma. Cópialo con «Copiar secreto» y guárdalo en el gestor de secretos de tu sistema: es la única vez que se muestra. ' +
          'Con él, tu sistema comprueba que cada entrega viene de Validasoft. Si lo pierdes, rota el secreto desde el webhook.',
        resaltar: secreto.getByText(/es la única vez que lo mostramos/),
      });

      await secreto.getByRole('button', { name: 'Ya lo guardé' }).click();
      const fila = app.getByRole('row', { name: new RegExp(`Ver ${receptor.url.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`) });
      await expect(fila).toBeVisible();

      // Un evento real: el alta de un proveedor en la empresa (la invitación crea el vínculo y su evento).
      const proveedor = nuevoTercero('Aceros del Norte S.A.S.');
      await invitar(e, e.norte, proveedor);
      await receptor.esperar((x) => x.tipo.length > 0);
      await fila.click();
      const entregas = app.getByRole('table', { name: 'Entregas recientes' });
      await expect(entregas.getByText('Entregada').first()).toBeVisible({ timeout: 45_000 });
      await paso(page, {
        titulo: 'Revisa las entregas del webhook',
        explicacion:
          'La sección «Entregas recientes» lista cada evento enviado, con su estado (pendiente, entregada, reintentando, fallida o cancelada), el número de intentos y la fecha. ' +
          'Puedes filtrarlas por estado. Una entrega que no tuvo éxito muestra cuándo será el próximo intento, y desde ahí puedes reenviarla a mano.',
        resaltar: entregas,
        avisos: false,
      });

      await entregas.getByRole('button', { name: /Ver intentos de/ }).first().click();
      await paso(page, {
        titulo: 'Mira los intentos de una entrega',
        explicacion:
          'Al abrir una entrega ves cada intento: su resultado (exitoso o fallido), la hora, el detalle y el tiempo de respuesta de tu sistema. ' +
          'Si un intento fue reenviado a mano, lo marca como manual.',
        resaltar: entregas.getByRole('button', { name: /Ver intentos de/ }).first(),
      });

      const entrega = receptor.entregas[0];
      if (!entrega) throw new Error('El receptor no recibió ninguna entrega.');
      const cuerpo = JSON.parse(entrega.cuerpo) as unknown;
      const pagina = await mostrarCodigo(page, {
        titulo: 'Así llega cada entrega a tu sistema',
        peticion: `POST ${receptor.url}`,
        cabeceras: { 'Content-Type': 'application/json', 'Validasoft-Signature': entrega.firma },
        cuerpo,
        respuesta: { estado: 200 },
      });
      await paso(page, {
        titulo: 'Verifica la firma de cada entrega',
        explicacion:
          'Cada entrega trae la cabecera Validasoft-Signature con una hora (t) y una firma (v1): el HMAC-SHA256 de la hora y el cuerpo con tu secreto. ' +
          'Recalcúlala en tu sistema y rechaza las firmas que no coincidan o que tengan más de 5 minutos. Tu sistema responde 2xx y procesa el evento después.',
        resaltar: [pagina.peticion, pagina.respuesta],
        celular: false,
      });
    } finally {
      await receptor.cerrar();
    }
  },
);
