import { expect, test, type Browser } from '@playwright/test';
import { llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../soporte/empresa';
import { completarInvitacion, tarjeta } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevaPersonaNatural, nuevoTercero, type Empresa, type Escenario, type Persona } from '../soporte/semilla';

/**
 * «Se pide a» (RQ-12): un mismo flujo de Norte pide la Cámara solo a personas jurídicas y la cédula solo a naturales.
 * Cada tercero ve y entrega únicamente lo suyo, y el área que solo revisaba lo que no aplica aprueba sola.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const CEDULA = 'Cédula de ciudadanía';

async function embebido(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa) {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa);
}

/** Flujo de proveedores de Norte con requisitos condicionados por tipo de persona, publicado y por defecto. */
async function publicarFlujoCondicionado(e: Escenario): Promise<void> {
  const norte = e.norte;
  const compras = norte.areas.compras!;
  const cumplimiento = norte.areas.cumplimiento!;
  const persona = (valor: string) => ({ tipo: 'Predicado', atributo: 'tercero.tipoPersona', operador: 'Igual', valores: [valor] });
  const area = (id: string) => ({ areaId: id, aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: false, slaHoras: null });
  const flujo = await llamar<{ id: string; tipoVinculo: { id: string } }>('POST', '/api/v1/flujos', {
    llave: e.admin,
    empresa: norte.id,
    cuerpo: {
      codigo: 'HAB_POR_PERSONA',
      nombre: 'Habilitación por tipo de persona',
      tipoVinculo: 'PROVEEDOR',
      etapas: [{ orden: 1, areas: [area(compras.id), area(cumplimiento.id)] }],
      requisitos: [
        { tipoDocumento: 'RUT', obligatorio: true, areasQueRevisan: [compras.id] },
        { tipoDocumento: 'CAMARA_COMERCIO', obligatorio: true, areasQueRevisan: [cumplimiento.id], condicion: persona('Juridica') },
        { tipoDocumento: 'CEDULA', obligatorio: true, areasQueRevisan: [compras.id], condicion: persona('Natural') },
      ],
    },
  });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: e.admin, empresa: norte.id });
  await llamar('PUT', '/api/v1/flujos/por-defecto', {
    llave: e.admin,
    empresa: norte.id,
    cuerpo: { tipoVinculoId: flujo.tipoVinculo.id, flujoId: flujo.id, empresaId: norte.id },
  });
}

test('A una persona natural no se le pide la Cámara: entrega su cédula y Cumplimiento, sin nada que revisar, aprueba solo', async ({ page, browser }) => {
  const e = await crearEscenario();
  await publicarFlujoCondicionado(e);
  const ana = nuevaPersonaNatural('Ana María Pérez');
  const invitacion = await invitar(e, e.norte, ana);

  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Ana') },
    { nombre: CEDULA, archivo: pdf('Cedula Ana') },
  ]);
  await expect(tarjeta(page, CAMARA)).toHaveCount(0);

  const ruben = await embebido(browser, e, e.ruben, e.norte);
  await abrirDesdeBandeja(ruben, ana.nombre, 'Compras');
  // Rubén solo ve el panel de su área; el historial cuenta que Cumplimiento aprobó sola, como Sistema y con el porqué.
  await expect(ruben.getByRole('main')).toContainText(/Cumplimiento aprobó\s*Sistema/);
  await expect(ruben.getByRole('main')).toContainText('A este tercero no se le pidió ningún documento que revise esta área.');
  await expect(regionDeArea(ruben, 'Compras')).toContainText(CEDULA);
  await expect(ruben.getByRole('main')).not.toContainText(CAMARA);

  // Compras aprueba lo suyo (RUT y cédula) y con eso se aprueba el expediente.
  await aprobarArea(ruben, 'Compras');
  await expect(ruben.getByText(/Todas las áreas aprobaron el /)).toBeVisible();
});

test('A una persona jurídica el mismo flujo le pide la Cámara y no la cédula', async ({ page, browser }) => {
  const e = await crearEscenario();
  await publicarFlujoCondicionado(e);
  const tercero = nuevoTercero('Concretos del Valle S.A.S.');
  const invitacion = await invitar(e, e.norte, tercero);

  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Concretos') },
    { nombre: CAMARA, archivo: pdf('Camara Concretos') },
  ]);
  await expect(tarjeta(page, CEDULA)).toHaveCount(0);

  // La Cámara la revisa Cumplimiento, que esta vez sí tiene trabajo.
  const olga = await embebido(browser, e, e.olga, e.norte);
  await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
  await expect(regionDeArea(olga, 'Cumplimiento')).not.toContainText('Aprobada');
  await expect(olga.getByRole('main')).toContainText(CAMARA);
  await expect(olga.getByRole('main')).not.toContainText(CEDULA);
});
