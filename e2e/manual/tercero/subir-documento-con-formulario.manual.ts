import { pdf } from '../../soporte/archivos';
import { escribirFecha } from '../../soporte/fechas';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, tarjeta } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

/** Fecha `aaaa-mm-dd` de hace tres días: el certificado vence a los 30 días de su expedición, así que sigue vigente. */
const hace3Dias = new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10);

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Sube un documento que pide una fecha de expedición',
    resumen:
      'La empresa pide el certificado de seguridad y salud en el trabajo. Como tiene fecha de expedición y vigencia, el tercero sube el archivo y escribe la fecha; Validasoft calcula cuándo vence y avisa antes de que pase.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y su flujo de Proveedor, que pide el RUT y el certificado de seguridad y salud en el trabajo (vence a los 30 días de su expedición, avisa 10 días antes y da 5 días de gracia).',
      'Un tercero persona jurídica invitado a ese flujo, que ya aceptó la invitación.',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica', 'tercero/documento-por-vencer-y-vencido'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');

    // 1. El documento en la lista de requisitos.
    await expect(tarjeta(page, SST.nombre)).toBeVisible();
    await paso(page, {
      titulo: 'Encuentra el certificado en «Lo que te pide»',
      explicacion:
        'En «Lo que te pide» aparece el certificado de seguridad y salud en el trabajo, con las reglas de su tipo: formato PDF, JPG, PNG o WebP y tamaño máximo. ' +
        'Como este documento tiene fecha de expedición, la pantalla de carga te pedirá esa fecha además del archivo. Pulsa «Subir documento» para abrirlo.',
      resaltar: tarjeta(page, SST.nombre),
      variantes: [
        { cuando: 'El documento no tiene campos propios', entonces: 'La pantalla de carga solo pide el archivo.' },
        { cuando: 'Ya subiste una versión', entonces: 'El botón dice «Subir otra versión» y la nueva versión reemplaza la vigente en revisión.' },
      ],
      configuracion: {
        ajuste: 'Campos del formulario, vigencia y aviso del tipo de documento',
        ruta: 'Configuración › Tipos de documento (/tipos-documento)',
        efecto: 'Define la fecha de expedición que se pide, el plazo de vigencia y cuántos días antes se avisa.',
      },
    });

    // 2. La pantalla de carga con fecha y archivo.
    await tarjeta(page, SST.nombre).getByRole('link', { name: /^Subir/ }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Subir ${SST.nombre}` })).toBeVisible();
    await paso(page, {
      titulo: 'Escribe la fecha de expedición y elige el archivo',
      explicacion:
        'Escribe la fecha de expedición del certificado en el selector de fecha (día, mes y año) y luego pulsa «Elegir archivo». ' +
        'Validasoft usa esa fecha para calcular cuándo vence: el certificado vale 30 días desde su expedición. Si el archivo no cumple el formato o el tamaño, la pantalla te lo dice y no guarda nada.',
      resaltar: [page.getByRole('button', { name: 'Elegir archivo' })],
    });
    await escribirFecha(page, 'Fecha de expedición', hace3Dias);
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf(`SST ${tercero.identificacion.numero}`));

    // 3. Versión guardada y vigencia.
    await expect(page.getByText(`Es tu versión 1 de ${SST.nombre}`)).toBeVisible({ timeout: 30_000 });
    await paso(page, {
      titulo: 'Confirma la versión y su vigencia',
      explicacion:
        'Validasoft confirma «Es tu versión 1» del certificado y dice qué empresa ya lo tiene. El documento guarda su fecha de expedición y la fecha en que vence; cuando falten 10 días te avisamos por correo. ' +
        'Si el documento vence, tienes 5 días de gracia para subir uno nuevo antes de que la empresa lo marque como vencido.',
      resaltar: page.getByText(`Es tu versión 1 de ${SST.nombre}`),
      configuracion: {
        ajuste: 'Vigencia, aviso y gracia del certificado',
        ruta: 'Configuración › Tipos de documento (/tipos-documento)',
        efecto: 'Fija cuántos días vale el documento, cuándo se avisa y cuánta gracia tiene antes de vencerse.',
      },
    });
    await page.getByRole('link', { name: 'Volver a mi revisión' }).click();

    // 4. El documento en «Tu revisión».
    await expect(tarjeta(page, SST.nombre)).toBeVisible();
    await paso(page, {
      titulo: 'Revisa cómo quedó el documento en tu revisión',
      explicacion:
        'En «Lo que te pide» el certificado ya aparece con su versión y su vigencia («Vigente» o «Por vencer»), y muestra la fecha en que vence. ' +
        'Mientras tanto la empresa lo revisa; si lo observa, verás el motivo en esta misma tarjeta.',
      resaltar: tarjeta(page, SST.nombre),
    });
  },
);
