import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import {
  abrirSeccion,
  agregarPersona,
  guardarSeccionCompleta,
  llenarContactoYDomicilio,
  llenarEspecialidades,
  llenarFinanciera,
  llenarSst,
  llenarTributaria,
  REPRESENTANTE,
  SOCIO,
} from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Consulta la ficha aceptada de un vínculo',
    resumen:
      'La administradora abre la ficha aceptada de un proveedor cuya ficha completa ya fue aprobada por Constructora Sur. Ve, sección por sección, lo que la empresa aceptó, con su versión y el soporte con el que se aceptó.',
    precondiciones: [
      'Un grupo con Constructora Sur, con un flujo de proveedor que pide el RUT, la Cámara de comercio y las seis secciones de la ficha.',
      'Un proveedor cuya ficha quedó completa y su área de Revisión la aprobó.',
    ],
    relacionados: ['tercero/ficha-secciones-con-soporte', 'vinculos/detalle-del-vinculo'],
  },
  async ({ page, browser }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Aceros del Sur S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');

    // Siembra: el proveedor completa su lado en otra ventana (no se documenta aquí).
    const paginaTercero = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(paginaTercero, invitacion);
    await aceptarInvitacion(paginaTercero, invitacion);
    await subirDocumento(paginaTercero, RUT, pdf('RUT Ficha aceptada'));
    await subirDocumento(paginaTercero, CAMARA, pdf('Camara Ficha aceptada'));
    await abrirSeccion(paginaTercero, 'general', 'Información general');
    await llenarContactoYDomicilio(paginaTercero, '601 555 1234', 'Calle 45 # 12-08');
    await paginaTercero.getByRole('checkbox', { name: /^Registro Único Tributario · versión 1/ }).check();
    await guardarSeccionCompleta(paginaTercero, 'Guardaste Información general: está completa.');
    await llenarTributaria(paginaTercero);
    await abrirSeccion(paginaTercero, 'legal', 'Información legal');
    await agregarPersona(paginaTercero, REPRESENTANTE);
    await agregarPersona(paginaTercero, SOCIO);
    await guardarSeccionCompleta(paginaTercero, 'Guardaste Información legal: está completa.');
    await llenarFinanciera(paginaTercero);
    await llenarSst(paginaTercero);
    await llenarEspecialidades(paginaTercero);
    await paginaTercero.goto('/empresas');
    await abrirEmpresa(paginaTercero, e.sur.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(paginaTercero, 'EnRevision');
    const paginaSofia = await (await browser.newContext()).newPage();
    const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Proveedor');
    await aprobarArea(sofia, 'Revisión');

    const app = await abrirEmbebido(page, e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(app.getByRole('link', { name: /^Ver la ficha aceptada/ })).toBeVisible();

    await paso(page, {
      titulo: 'Abre la ficha aceptada desde el vínculo',
      explicacion:
        'Cuando tu empresa aprueba secciones de la ficha de un tercero, el detalle del vínculo muestra la tarjeta «Ficha aceptada». Haz clic en «Ver la ficha aceptada» para ver exactamente lo que se aprobó.',
      resaltar: app.getByRole('link', { name: /^Ver la ficha aceptada/ }),
      configuracion: {
        ajuste: 'Ficha del tercero y flujo',
        ruta: 'Configuración › Flujos (/flujos)',
        efecto: 'Las secciones que aparecen en la ficha aceptada son las que el flujo del vínculo pide al tercero.',
      },
    });

    await app.getByRole('link', { name: /^Ver la ficha aceptada/ }).click();
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(app.getByText('Nada pendiente de revisión.')).toBeVisible();
    await paso(page, {
      titulo: 'Lee el resumen de la ficha aceptada',
      explicacion:
        'El resumen cuenta las secciones aceptadas y las que tienen una versión nueva pendiente de revisión. Cada empresa acepta por su cuenta: lo que aceptó otra empresa del grupo no aparece aquí.',
      resaltar: app.getByText('Nada pendiente de revisión.'),
      variantes: [
        {
          cuando: 'El tercero guardó una versión nueva de una sección aceptada',
          entonces: 'El resumen lo indica y la sección muestra lo aceptado y, aparte, lo que cambió. Lo nuevo solo vale cuando lo apruebes en la revalidación.',
        },
      ],
    });

    const general = app.getByRole('region', { name: 'Información general' });
    await expect(general).toContainText('Aceptada · versión 1');
    await paso(page, {
      titulo: 'Revisa una sección aceptada y su versión',
      explicacion:
        'Cada sección muestra la versión aceptada, la fecha de aceptación y el expediente donde se aprobó. Con «Ver los datos aceptados» abres los valores tal como se aprobaron.',
      resaltar: general,
    });

    await expect(general.getByRole('region', { name: 'Soporte con que se aceptó' })).toContainText(`${RUT} · versión 1`);
    await paso(page, {
      titulo: 'Revisa el soporte con que se aceptó',
      explicacion:
        'Dentro de la sección aparece el soporte con que se aceptó, con su nombre y su versión. Si una sección se aceptó sin soporte, dice «Sin soporte».',
      resaltar: general.getByRole('region', { name: 'Soporte con que se aceptó' }),
    });
  },
);
