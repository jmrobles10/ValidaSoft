import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'ia',
    actor: 'Revisor/área',
    titulo: 'Lee el análisis automático de un documento',
    resumen:
      'Cada documento que sube el tercero se lee con IA: Validasoft muestra si es legible, qué tipo de documento detectó, los datos que extrajo y su confianza. Sofía, de Revisión, usa ese análisis para decidir, sin que la IA decida por ella.',
    precondiciones: ['Un grupo con Constructora Sur, la IA activa y un flujo de Cliente que pide el RUT al área Revisión.'],
    relacionados: ['ia/cruce-de-la-ficha-con-los-soportes', 'ia/revision-asistida-y-excepciones'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_ANALISIS', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }]);
    const cliente = nuevoTercero('Aceros Analizados S.A.S.');
    const paginaCliente = await (await browser.newContext()).newPage();
    await completarInvitacion(paginaCliente, await invitar(e, e.sur, cliente, 'CLIENTE'), [
      { nombre: RUT, archivo: pdf(`RUT ${cliente.nombre} [[nit=${cliente.identificacion.numero}]][[razonSocial=${cliente.nombre}]]`) },
    ]);

    const paginaSofia = await (await browser.newContext()).newPage();
    const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'bandeja');
    const panel = sofia.getByRole('region', { name: 'Revisión automática', exact: true }).first();
    await expect(async () => {
      await abrirDesdeBandeja(sofia, cliente.nombre, 'Revisión', 'Cliente');
      await expect(panel).toBeVisible({ timeout: 1500 });
    }).toPass({ timeout: 60_000 });
    await expect(panel).toContainText('Legible');
    await expect(panel).toContainText('Tipo detectado');

    await paso(paginaSofia, {
      titulo: 'Lee el análisis del documento',
      explicacion:
        'Debajo del documento aparece el bloque «Revisión automática» con la confianza de la lectura, la fecha y el modelo que lo analizó, si el archivo es legible y el tipo de documento detectado. Si el tipo no coincide con el que se pidió, un aviso lo señala para que revises que el tercero subió el documento correcto.',
      resaltar: panel,
      variantes: [
        { cuando: 'El archivo no es legible', entonces: 'El análisis dice «No: revisa el archivo a mano» y no se usa para decidir.' },
        { cuando: 'La confianza está bajo el umbral de la empresa', entonces: 'Conviene revisar los datos con más cuidado; el sistema no los sugiere ni aprueba.' },
      ],
    });

    const datos = panel.getByText(/Ver \d+ datos? más/).first();
    await paso(paginaSofia, {
      titulo: 'Revisa los datos que extrajo la IA',
      explicacion:
        'La sección «Datos extraídos» lista los campos que la IA leyó del documento, como el NIT, la razón social, el representante legal o el tipo de contribuyente. Con «Ver más datos» despliegas el resto. Compara estos datos con lo que declaró el tercero antes de aprobar el área.',
      resaltar: [panel.getByText('Datos extraídos', { exact: true }).first(), datos],
    });
  },
);
