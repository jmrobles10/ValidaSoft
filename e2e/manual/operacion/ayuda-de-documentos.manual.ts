import { abrirEmbebido } from '../../soporte/embebido';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const AYUDA = 'Trae el RUT con fecha de este año y las responsabilidades marcadas.';

recorrido(
  {
    dominio: 'operacion',
    actor: 'Administrador de empresa',
    titulo: 'Escribe la ayuda que ve el tercero al subir un documento',
    resumen:
      'La administradora escribe, para su empresa, la ayuda que el tercero lee al entregar el RUT: qué debe traer y de qué fecha. Si no escribe nada, el tercero ve la ayuda del grupo.',
    precondiciones: ['Un grupo con Constructora Norte, el tipo de documento RUT y una ayuda del grupo para ese documento.'],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, 'ayuda-de-documentos');
    await expect(ana.getByRole('heading', { level: 1, name: 'Ayuda de documentos' })).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Abre la ayuda de documentos',
      explicacion:
        'La tabla lista cada tipo de documento con la ayuda que ve el tercero y de quién es: «Propia» si la escribió esta empresa, «Del grupo» si viene de todo el grupo o «Sin ayuda» si nadie la escribió. Quien no puede cambiarla ve la lista en solo lectura.',
      resaltar: ana.getByRole('table'),
      configuracion: {
        ajuste: 'Ayuda de documentos',
        ruta: 'Configuración › Ayuda de documentos (/ayuda-de-documentos)',
        efecto: 'La ayuda aparece al tercero cuando entrega ese documento en un expediente de esta empresa.',
      },
      variantes: [{ cuando: 'Eres revisor sin permiso de configuración', entonces: 'Ves las ayudas, pero no puedes cambiarlas.' }],
    });

    await ana.getByRole('button', { name: 'Editar la ayuda de Registro Único Tributario' }).click();
    const panel = ana.getByRole('textbox', { name: /Ayuda de Constructora Norte/ });
    await panel.fill(AYUDA);
    await paso(paginaAna, {
      titulo: 'Escribe la ayuda de la empresa',
      explicacion:
        'Al editar el documento, escribe lo que el tercero necesita saber: qué debe traer, de qué fecha y firmado por quién. El texto de la empresa reemplaza al del grupo para esta empresa. Si lo dejas vacío, el tercero vuelve a ver la ayuda del grupo.',
      resaltar: panel,
      variantes: [{ cuando: 'Dejas vacío el texto y guardas', entonces: 'El tercero ve la ayuda del grupo, o ninguna si el grupo no escribió una.' }],
    });

    await ana.getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(ana.getByText('Ayuda guardada: el tercero la ve desde ahora.')).toBeVisible();
    await expect(ana.getByRole('row').filter({ hasText: 'Registro Único Tributario' }).getByText('Propia', { exact: true })).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Comprueba que la ayuda es de la empresa',
      explicacion:
        'La fila del RUT pasa a «Propia». Desde ese momento, los terceros de la empresa que entreguen el RUT ven este texto al lado del documento.',
      resaltar: ana.getByRole('row').filter({ hasText: 'Registro Único Tributario' }),
      avisos: true,
    });

    const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros de la Ayuda S.A.S.'));
    const tercero = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(tercero, invitacion);
    await aceptarInvitacion(tercero, invitacion);
    await expect(tercero.getByText(AYUDA).first()).toBeVisible();
    await paso(tercero, {
      titulo: 'Lee la ayuda al entregar el documento',
      explicacion:
        'Cuando el tercero abre el expediente, la ayuda aparece junto al documento que tiene que entregar, en su portal. Así sabe qué traer antes de subir el archivo, sin tener que escribirle a la empresa.',
      resaltar: tercero.getByText(AYUDA).first(),
    });
  },
);
