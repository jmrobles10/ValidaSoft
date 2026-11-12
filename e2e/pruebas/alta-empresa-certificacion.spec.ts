import { expect, test, type FrameLocator } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { asercion, CLAVE_PLATAFORMA, clavesDeSistema, llamar, nitAleatorio } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { abrirEmbebidoCon, sesionDeUsuario } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { abrirConsolaDePlataforma } from '../soporte/plataforma';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../soporte/proveedor';
import { ORIGEN_SISTEMA, type Invitacion } from '../soporte/semilla';

/**
 * Una empresa nueva, de la alta a la primera certificación (docs/16 §1, criterio de terminado): un operador de Plataforma crea el grupo
 * desde la consola; su administradora deja la configuración mínima (aceptación legal, un área con ella de revisora y un flujo que pide el
 * RUT); el sistema de la empresa invita a un proveedor; el proveedor entra por el correo, acepta y carga su RUT; la administradora lo revisa
 * y lo aprueba desde Validasoft embebido; y cualquiera verifica el certificado en la página pública. Nada de lo de la empresa nueva se
 * toma de un escenario sembrado: todo sale de la consola.
 */

const RUT = 'Registro Único Tributario';

// Flujos largos de punta a punta: con la máquina cargada el tope de 120 s por omisión queda justo.
test.setTimeout(180_000);

test('una empresa creada desde la consola certifica a su primer proveedor: invitación, carga, revisión y certificado verificable', async ({ page, browser }) => {
  const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);
  const nit = nitAleatorio();
  const grupo = `Grupo Cumbre ${sufijo}`;
  const empresa = `Edificaciones Cumbre ${sufijo} S.A.S.`;
  const prefijo = `CU${sufijo.slice(0, 4).toUpperCase()}`;
  const admin = { sub: 'valentina', nombre: 'Valentina Administradora', correo: `valentina.${sufijo}@cumbre.e2e` };
  const claves = clavesDeSistema();

  // 1. Un operador de Plataforma da de alta el grupo con su primera empresa, su administradora y su sistema integrado.
  const consola = await abrirConsolaDePlataforma(page);
  await expect(consola.getByRole('heading', { level: 1, name: 'Grupos y empresas' })).toBeVisible();
  await consola.getByRole('link', { name: 'Nuevo grupo' }).click();
  await consola.getByRole('textbox', { name: 'Nombre del grupo' }).fill(grupo);
  await consola.getByRole('textbox', { name: 'Número de identificación' }).fill(nit.numero);
  await consola.getByRole('textbox', { name: 'Razón social' }).fill(empresa);
  await consola.getByRole('textbox', { name: 'Prefijo de folio' }).fill(prefijo);
  await consola.getByRole('textbox', { name: 'Nombre', exact: true }).fill(admin.nombre);
  await consola.getByRole('textbox', { name: 'Correo' }).fill(admin.correo);
  await consola.getByRole('textbox', { name: 'Su usuario en el sistema (opcional)' }).fill(admin.sub);
  await consola.getByRole('textbox', { name: 'Código' }).fill('ERP');
  await consola.getByRole('textbox', { name: 'Nombre del sistema' }).fill('ERP de Cumbre');
  await consola.getByRole('textbox', { name: 'Clave pública (PEM)' }).fill(claves.publica);
  await consola.getByRole('textbox', { name: 'Sitios que pueden incrustar Validasoft (opcional)' }).fill(ORIGEN_SISTEMA);
  await consola.getByRole('button', { name: 'Crear grupo' }).click();
  await expect(consola.getByRole('heading', { level: 1, name: `Grupo ${grupo} creado` })).toBeVisible();
  const llave = (await consola.getByLabel('API key del sistema ERP', { exact: true }).textContent())?.trim() ?? '';
  expect(llave).toMatch(/^vsk_dev_/);

  // La empresa existe en la plataforma con el nombre y el prefijo que se dieron.
  const grupos = await llamar<{ datos: { empresas: { id: string; nombre: string; prefijoFolio: string }[] }[] }>('GET', `/api/v1/plataforma/grupos?q=${encodeURIComponent(empresa)}`, {
    llave: CLAVE_PLATAFORMA,
  });
  const creada = grupos.datos.flatMap((g) => g.empresas).find((x) => x.nombre === empresa);
  expect(creada?.prefijoFolio).toBe(prefijo);
  const empresaId = creada?.id ?? '';

  // 2. La administradora deja la configuración mínima, como el sistema de la empresa lo hace en nombre de ella.
  const delegado = () => asercion('ERP', claves.privada, admin);
  const comoAdmin = { llave, empresa: empresaId };
  for (const [tipo, contenido] of [
    ['autorizacion', `Autorizo a ${empresa} a tratar mis datos personales conforme a la Ley 1581 de 2012. (Texto de prueba.)`],
    ['terminos', `Términos y condiciones para terceros de ${empresa}. (Texto de prueba.)`],
  ] as const) {
    await llamar('PUT', `/api/v1/empresas/${empresaId}/textos-legales/${tipo}`, { ...comoAdmin, delegado: delegado(), cuerpo: { contenido } });
  }
  const usuarios = await llamar<{ id: string; correo: string }[]>('GET', '/api/v1/usuarios', { ...comoAdmin, delegado: delegado() });
  const administradora = usuarios.find((u) => u.correo === admin.correo);
  expect(administradora).toBeDefined();
  const area = await llamar<{ id: string }>('POST', '/api/v1/areas', { ...comoAdmin, delegado: delegado(), cuerpo: { codigo: 'compras', nombre: 'Compras' } });
  await llamar('PUT', `/api/v1/areas/${area.id}/miembros`, { ...comoAdmin, delegado: delegado(), cuerpo: { usuarios: [administradora?.id] } });
  const flujo = await llamar<{ id: string }>('POST', '/api/v1/flujos', {
    ...comoAdmin,
    delegado: delegado(),
    cuerpo: {
      codigo: 'HABILITACION',
      nombre: 'Habilitación de proveedores',
      tipoVinculo: 'PROVEEDOR',
      etapas: [{ orden: 1, areas: [{ areaId: area.id, aprobadoresRequeridos: 1, puedeRechazarExpediente: true, automatica: false, slaHoras: null }] }],
      politicaDevolucion: 'Inmediata',
      separacionDeFunciones: false,
      requisitos: [{ tipoDocumento: 'RUT', obligatorio: true, areasQueRevisan: [area.id], aprobadoresRequeridos: null }],
    },
  });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { ...comoAdmin, delegado: delegado() });

  // 3. El sistema de la empresa invita a su primer proveedor.
  const proveedor = `Aceros Cumbre ${sufijo} S.A.S.`;
  const correoProveedor = `compras.${sufijo}@aceros-cumbre.e2e`;
  const identificacion = nitAleatorio();
  const vinculo = await llamar<{ id: string; terceroId: string }>('POST', '/api/v1/vinculos', {
    ...comoAdmin,
    delegado: delegado(),
    cuerpo: {
      tercero: {
        identificacion,
        tipoPersona: 'Juridica',
        nombre: proveedor,
        nombreComercial: null,
        contactos: [{ nombre: 'Compras', correo: correoProveedor, telefono: null, rol: 'Comercial' }],
      },
      tipoVinculo: 'PROVEEDOR',
      atributos: {},
      correoInvitacion: correoProveedor,
      referenciaExterna: { sistema: 'ERP', id: `PRV-${sufijo}` },
      enviarInvitacion: true,
    },
  });
  const invitacion: Invitacion = {
    vinculoId: vinculo.id,
    terceroId: vinculo.terceroId,
    correo: correoProveedor,
    nombre: proveedor,
    empresa: { id: empresaId, nombre: empresa, prefijo, areas: {} },
    tipo: 'PROVEEDOR',
    referencia: `PRV-${sufijo}`,
  };

  // 4. El proveedor entra por el correo (con su código), acepta los textos de la empresa nueva y carga su RUT.
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await subirDocumento(page, RUT, pdf(`RUT ${identificacion.numero}`));
  await esperarEstadoDelTercero(page, 'EnRevision');

  // 5. La administradora, desde Validasoft embebido en el sistema de la empresa, revisa y aprueba.
  const ventana = await (await browser.newContext()).newPage();
  const app = await abrirEmbebidoCon(ventana, { empresaId, rutaInicial: 'bandeja', sesion: () => sesionDeUsuario(llave, delegado()) });
  await expect(async () => {
    await abrirDesdeBandeja(app, proveedor, 'Compras');
    await expect(app.getByRole('region', { name: 'Compras', exact: true }).first()).toContainText(`${RUT} · v1`, { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(app, 'Compras');
  await esperarEstadoDelTercero(page, 'Aprobado');

  // 6. El vínculo queda activo con su certificado, y cualquiera lo verifica en la página pública.
  const detalle = await abrirEmbebidoCon(await (await browser.newContext()).newPage(), {
    empresaId,
    rutaInicial: `vinculos/${vinculo.id}`,
    sesion: () => sesionDeUsuario(llave, delegado()),
  });
  await expect(detalle.getByRole('heading', { level: 1, name: proveedor })).toBeVisible();
  await expect(detalle.getByText(/^Activo$/).first()).toBeVisible();
  const publica = await (await browser.newContext()).newPage();
  await publica.goto(await enlaceDeVerificacion(detalle));
  await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();
  await expect(publica.getByRole('main')).toContainText(`${empresa} validó los documentos de ${proveedor}`);
  await expect(publica.getByRole('status').filter({ hasText: 'Auténtico: el contenido está íntegro y lo selló Validasoft.' })).toBeVisible();
  await expect(publica.getByRole('main')).not.toContainText(correoProveedor);
  expect(publica.url()).toContain(`/verificar/${prefijo}-C-`);
});

/** La dirección de la página de verificación que ofrece el vínculo (la del QR del certificado), sin el origen del iframe. */
async function enlaceDeVerificacion(detalle: FrameLocator): Promise<string> {
  const enlace = detalle.getByRole('link', { name: 'Página de verificación' });
  await expect(enlace).toBeVisible();
  const url = new URL((await enlace.getAttribute('href')) ?? '');
  return `${url.pathname}${url.search}`;
}
