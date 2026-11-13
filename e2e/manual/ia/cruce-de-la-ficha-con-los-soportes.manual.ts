import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { llenarGeneralJuridica } from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'ia',
    actor: 'Revisor/área',
    titulo: 'Compara la información del tercero con su RUT',
    resumen:
      'Cuando el cliente llena su información general y sube el RUT, la IA lee el documento y cruza sus datos, campo por campo, con lo que el tercero declaró. Sofía, de Revisión, ve el cruce y los soportes antes de aprobar el área.',
    precondiciones: [
      'Un grupo con Constructora Sur, la IA activa y un flujo de Cliente que pide el RUT y la información general.',
    ],
    relacionados: ['ia/analisis-de-un-documento', 'ia/revision-automatica-aprueba-sola'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_CRUCE', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }]);
    const cliente = nuevoTercero('Aceros del Cruce S.A.S.');
    const invitacion = await invitar(e, e.sur, cliente, 'CLIENTE');

    const paginaCliente = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(paginaCliente, invitacion);
    await aceptarInvitacion(paginaCliente, invitacion);
    await paginaCliente.getByRole('link', { name: /^Completar/ }).first().click();
    await llenarGeneralJuridica(paginaCliente, cliente.nombre);
    await paginaCliente.goto('/empresas');
    await abrirEmpresa(paginaCliente, e.sur.nombre, 'CLIENTE');
    const nit = cliente.identificacion.numero;
    await subirDocumento(
      paginaCliente,
      RUT,
      pdf(`RUT v1 [[nit=${nit}]][[razonSocial=${cliente.nombre}]][[direccion=Calle 45 # 12-08]][[codigoMunicipio=11001]][[actividadEconomicaPrincipal=4663]]`),
    );
    await esperarEstadoDelTercero(paginaCliente, 'EnRevision');

    const paginaSofia = await (await browser.newContext()).newPage();
    const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'bandeja');
    const cruce = sofia.getByRole('region', { name: 'Cruce con el documento' });
    await expect(async () => {
      await abrirDesdeBandeja(sofia, cliente.nombre, 'Revisión', 'Cliente');
      await expect(cruce).toContainText('Confianza de la lectura', { timeout: 1500 });
    }).toPass({ timeout: 60_000 });
    await expect(cruce.getByRole('table', { name: /^Cruce con Registro Único Tributario · versión 1/ })).toBeVisible();

    await paso(paginaSofia, {
      titulo: 'Revisa el cruce con el documento',
      explicacion:
        'La tabla compara cada dato que declaró el tercero en su información general con el que leyó la IA en el RUT: razón social, NIT, dirección, ciudad, actividad económica, entre otros. Cada fila dice «Coincide», «Difiere» o «Sin verificar», si el documento no trae ese dato. El resumen cuenta coincidencias y diferencias, y la confianza de la lectura va arriba. El cruce no aprueba ni observa nada por sí mismo: la decisión es de quien revisa.',
      resaltar: cruce,
    });

    const soporte = sofia.getByRole('region', { name: 'Soporte de la información' });
    await expect(soporte).toContainText('Información general (versión 1)');
    await paso(paginaSofia, {
      titulo: 'Confirma qué soporta la información',
      explicacion:
        'En «Soporte de la información» se ve con qué versión de la información general y de cada documento se decidió. Así queda claro que el cruce se hizo contra los datos del tercero que están vigentes.',
      resaltar: soporte,
    });

    await aprobarArea(sofia, 'Revisión');
    await esperarEstadoDelTercero(paginaCliente, 'Aprobado');
  },
);
