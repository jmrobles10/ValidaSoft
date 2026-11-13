import { clavesDeSistema } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CODIGO = 'ERP_DEMO';
const NOMBRE = 'ERP Constructora Demo';
const ORIGEN = 'https://erp.cliente.co';

recorrido(
  {
    dominio: 'integracion',
    actor: 'Administrador de empresa',
    titulo: 'Registra un sistema integrado con su clave pública',
    resumen:
      'La administradora registra un sistema externo (un ERP, un portal) con su código, su nombre, su clave pública y los sitios desde los que se incrusta Validasoft. Desde ese momento las personas pueden entrar desde ese sistema.',
    precondiciones: [
      'Un grupo empresarial con dos empresas y una administradora que puede administrar sistemas integrados.',
      'El sistema E2E ya registrado en el grupo (así se ve la tabla con datos).',
    ],
    relacionados: ['integracion/api-keys', 'embebido/app-de-la-empresa-en-un-iframe'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const clave = clavesDeSistema().publica;
    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'sistemas');
    await expect(app.getByRole('heading', { level: 1, name: 'Sistemas integrados' })).toBeVisible();
    await expect(app.getByRole('row', { name: /Ver Sistema integrado E2E/ })).toBeVisible();

    await paso(page, {
      titulo: 'Abre Configuración › Sistemas integrados',
      explicacion:
        'La tabla lista los sistemas desde los que entran las personas del grupo y crean vínculos por API. Cada fila muestra el nombre y el código del sistema, si está activo, la huella de su clave pública, cuántos sitios pueden incrustar Validasoft y la fecha de registro. ' +
        'Validasoft solo guarda la clave pública: el sistema firma con su clave privada la identidad de sus usuarios, y esa clave privada nunca llega a Validasoft.',
      resaltar: [app.getByRole('table', { name: 'Sistemas integrados' }), app.getByRole('button', { name: 'Registrar sistema' })],
      configuracion: {
        ajuste: 'Sistemas integrados',
        ruta: 'Configuración › Sistemas integrados (/sistemas)',
        efecto: 'Un sistema registrado puede firmar identidades y, con sus API keys, llamar a la API del grupo.',
      },
    });

    await app.getByRole('button', { name: 'Registrar sistema' }).click();
    const dialogo = app.getByRole('dialog', { name: 'Registrar sistema' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('textbox', { name: /^Código/ }).fill(CODIGO);
    await dialogo.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE);
    await paso(page, {
      titulo: 'Escribe el código y el nombre del sistema',
      explicacion:
        'El código (iss) es el emisor de las aserciones que firma el sistema: Validasoft lo usa para saber de qué sistema llega cada identidad, así que no cambia después. ' +
        'Se escribe en mayúsculas, sin espacios, y el nombre es el que verás en la tabla. Mientras falte alguno, el botón «Registrar sistema» queda deshabilitado y te dice qué falta.',
      resaltar: [dialogo.getByRole('textbox', { name: /^Código/ }), dialogo.getByRole('textbox', { name: /^Nombre/ })],
    });

    await dialogo.getByRole('textbox', { name: /^Clave pública/ }).fill(clave);
    await paso(page, {
      titulo: 'Pega la clave pública del sistema',
      explicacion:
        'Pega la clave pública en formato PEM, la que empieza por «-----BEGIN PUBLIC KEY-----». Acepta RSA de 2048 bits o más, o EC P-256. ' +
        'Validasoft la verifica al guardar; una clave privada o un formato no válido se rechaza. Si el sistema debe rotarla luego, usa «Rotar clave» desde su ficha.',
      resaltar: dialogo.getByRole('textbox', { name: /^Clave pública/ }),
      variantes: [
        { cuando: 'La clave es RSA de 2048 bits o más, o EC P-256', entonces: 'Se acepta y el sistema queda listo para firmar.' },
        { cuando: 'La clave es menor o de otro tipo', entonces: 'Se rechaza con un mensaje; no se guarda nada.' },
      ],
    });

    await dialogo.getByRole('textbox', { name: /^Sitios que pueden incrustar/ }).fill(ORIGEN);
    await paso(page, {
      titulo: 'Indica desde qué sitios se incrusta Validasoft',
      explicacion:
        'Escribe un sitio por línea, con el protocolo y el dominio (y el puerto si aplica), sin ruta. Solo esos sitios pueden mostrar Validasoft dentro de un iframe y solo a ellos se les entrega la sesión. ' +
        'Es opcional: si lo dejas vacío, nadie puede incrustarlo todavía y puedes agregarlos después desde la ficha del sistema.',
      resaltar: dialogo.getByRole('textbox', { name: /^Sitios que pueden incrustar/ }),
      variantes: [
        { cuando: 'Escribes http://', entonces: 'Solo se admite para localhost en desarrollo; en producción se rechaza.' },
        { cuando: 'Escribes una ruta o un comodín', entonces: 'Se rechaza: el sitio es el origen, no una página.' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Registrar sistema' }).click();
    await expect(app.getByText('Sistema registrado.')).toBeVisible();
    await expect(app.getByRole('row', { name: new RegExp(`Ver ${NOMBRE}`) })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el sistema registrado',
      explicacion:
        'Al registrarlo, el sistema aparece en la tabla con su estado «Activo», la huella de su clave y el número de sitios. Su ficha muestra la clave pública completa, el emisor (iss) y la fecha de registro. ' +
        'Desde la ficha también puedes rotar la clave (la nueva vale solo para las aserciones firmadas con ella) o desactivar el sistema: sus API keys dejan de autenticar y las personas no pueden entrar desde él.',
      resaltar: [app.getByRole('row', { name: new RegExp(`Ver ${NOMBRE}`) }), app.getByRole('button', { name: 'Rotar clave' })],
      avisos: true,
      configuracion: { ajuste: 'Rotar clave y desactivar sistema', ruta: 'Configuración › Sistemas integrados (/sistemas)', efecto: 'Rotar exige coordinar el cambio con el equipo del sistema.' },
    });
  },
);
