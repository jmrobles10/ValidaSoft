import { pdf } from '../../soporte/archivos';
import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja } from '../../soporte/empresa';
import { completarInvitacion, completarInvitacionYEsperarRevisionOAprobacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'ia',
    actor: 'Administrador de empresa',
    titulo: 'Atiende los documentos cuando se agota el cupo de IA',
    resumen:
      'La empresa tiene una sola validación incluida en el mes. El primer RUT la usa y se aprueba solo; el segundo ya no tiene análisis: llega a revisión humana, sin aprobarse solo, y la administradora ve el cupo agotado en «Plan y uso de IA».',
    precondiciones: [
      'Un grupo con Constructora Sur, la IA activa con 1 validación incluida al mes y sin adicional, y revisión automática en nivel Automático para el RUT.',
    ],
    relacionados: ['ia/plan-y-uso-de-ia', 'ia/revision-automatica-aprueba-sola'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur, 1);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_CUPO', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }]);
    await llamar('PUT', '/api/v1/configuracion/revision-automatica', {
      llave: e.admin,
      empresa: e.sur.id,
      cuerpo: {
        nivel: 'Automatico',
        umbralConfianza: 0.95,
        porcentajeMuestreo: 0,
        topeMensualAprobaciones: null,
        tiposAutorizados: ['RUT'],
        areasAutorizadas: [e.sur.areas.revision!.id],
        esperaIaMinutos: 10,
      },
    });

    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'uso-ia');
    await expect(ana.getByRole('heading', { level: 1, name: 'Plan y uso de IA' })).toBeVisible();
    await expect(ana.getByText('0 de 1', { exact: true })).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Revisa el cupo antes de usarlo',
      explicacion:
        'Antes de que lleguen documentos, «Validaciones incluidas» muestra «0 de 1» y «Quedan 1 este mes». Sin adicional, cuando se agote el cupo los documentos nuevos pasan a revisión manual: nada se bloquea.',
      resaltar: ana.getByText('0 de 1', { exact: true }),
    });

    const primero = nuevoTercero('Aceros Primero S.A.S.');
    await completarInvitacionYEsperarRevisionOAprobacion(await (await browser.newContext()).newPage(), await invitar(e, e.sur, primero, 'CLIENTE'), [
      { nombre: RUT, archivo: pdf(`RUT ${primero.nombre} [[nit=${primero.identificacion.numero}]][[razonSocial=${primero.nombre}]]`) },
    ]);

    const segundo = nuevoTercero('Aceros Segundo S.A.S.');
    const paginaSegundo = await (await browser.newContext()).newPage();
    await completarInvitacion(paginaSegundo, await invitar(e, e.sur, segundo, 'CLIENTE'), [
      { nombre: RUT, archivo: pdf(`RUT ${segundo.nombre} [[nit=${segundo.identificacion.numero}]][[razonSocial=${segundo.nombre}]]`) },
    ]);

    const paginaSofia = await (await browser.newContext()).newPage();
    const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'bandeja');
    await abrirDesdeBandeja(sofia, segundo.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('region', { name: 'Decisión automática' })).toContainText('Sin análisis de IA');
    await paso(paginaSofia, {
      titulo: 'Revisa el documento que quedó sin análisis',
      explicacion:
        'El segundo RUT llegó cuando el cupo ya estaba agotado. Su documento dice «Sin análisis automático: revísalo manualmente» y la decisión automática indica que requiere una persona por «Sin análisis de IA». Nada se aprueba solo: el área lo decide como cualquier documento.',
      resaltar: sofia.getByRole('region', { name: 'Decisión automática' }),
      variantes: [{ cuando: 'Se agota el cupo y la empresa tiene adicional', entonces: 'Los documentos se analizan con el adicional, y solo cuando este también se agota quedan sin análisis.' }],
    });

    await paginaAna.reload();
    await expect(ana.getByText('1 de 1', { exact: true })).toBeVisible();
    await expect(ana.getByText('Agotaste las incluidas de este mes.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Comprueba el cupo agotado',
      explicacion:
        'El plan muestra «1 de 1» y el aviso «Agotaste las incluidas de este mes». Los administradores reciben aviso al llegar al 80 % y al 100 % del cupo. El tercero no ve nada de esto: ni la confianza ni el modelo de IA.',
      resaltar: [ana.getByText('1 de 1', { exact: true }), ana.getByText('Agotaste las incluidas de este mes.')],
    });

    await esperarEstadoDelTercero(paginaSegundo, 'EnRevision');
  },
);
