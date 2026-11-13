import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, observar } from '../../soporte/empresa';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, fraseDeEstado, responder, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Corrige lo que la empresa observó y responde',
    resumen:
      'La empresa observa un documento y el tercero recibe un correo con los motivos. Entra, ve qué debe corregir en cada documento, sube la versión corregida y envía su respuesta; el expediente vuelve a revisión.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud en el trabajo) y un área «Revisión» con una persona como miembro.',
      'Un tercero que ya subió sus documentos, cuyo expediente fue observado por el área Revisión en el RUT.',
    ],
    relacionados: ['tercero/subir-version-nueva', 'tercero/expediente-aprobado'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero}`));
    await subirDocumento(page, SST.nombre, pdf(`SST ${tercero.identificacion.numero}`), {
      campos: { 'Fecha de expedición': new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10) },
    });
    await esperarEstadoDelTercero(page, 'EnRevision');

    // La empresa observa el RUT con un motivo.
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
    await observar(sofia, 'Revisión', [{ documento: `${RUT} · v1`, motivos: ['Ilegible'] }], 'No se lee el NIT.');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(page, 'Observado');

    // 1. La frase y el motivo por documento.
    await paso(page, {
      titulo: 'Lee lo que te pide corregir la empresa',
      explicacion:
        'La frase de arriba dice que la empresa te pide corregir algo. En la tarjeta del documento observado ves el motivo que marcó el área (aquí, que el documento no se puede leer con claridad) y la nota que escribió para ti. ' +
        'Los documentos que no tienen observación siguen en revisión sin que tengas que hacer nada con ellos. Te llegó un único correo con todos los motivos.',
      resaltar: [fraseDeEstado(page, 'Observado'), tarjeta(page, RUT)],
    });

    // 2. Sube la versión corregida.
    await subirDocumento(page, RUT, pdf(`RUT corregido ${tercero.identificacion.numero}`), { version: 2 });

    // 3. Escribe la respuesta y envíala.
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await paso(page, {
      titulo: 'Escribe tu respuesta y envíala',
      explicacion:
        'Escribe en «Tu respuesta» qué corregiste; es opcional, pero ayuda a la empresa a revisar más rápido. Luego pulsa «Enviar respuesta»: la revisión vuelve a «En revisión» y la empresa recibe tu versión corregida.',
      resaltar: page.getByRole('textbox', { name: 'Tu respuesta (opcional)' }),
    });
    await responder(page, 'Corregí el RUT: ahora se lee el NIT en la primera página.');

    // 4. Vuelve a revisión.
    await paso(page, {
      titulo: 'Espera la nueva revisión',
      explicacion:
        'Tu expediente vuelve a «En revisión»: la empresa revisa de nuevo lo que corregiste y decide por cada área. Mientras tanto no tienes que hacer nada más.',
      resaltar: fraseDeEstado(page, 'EnRevision'),
    });
  },
);
