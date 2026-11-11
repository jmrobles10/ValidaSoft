import { expect, test } from '@playwright/test';
import { ClienteValidasoft, validarWebhook } from '@validasoft/sdk';
import { API, llamar, nitAleatorio } from '../soporte/api';
import { abrirEmbebidoCon, sesionDeUsuario } from '../soporte/embebido';
import { Receptor } from '../soporte/receptor';
import { crearEscenario, type Empresa, type Escenario } from '../soporte/semilla';

/**
 * Integración de punta a punta con el SDK TypeScript, **en el sandbox** (SB-1…SB-7, docs/08 §2.2 y docs/integracion.md): el sistema
 * integrado emite su llave de prueba, invita a un tercero, recibe los webhooks firmados, consulta la certificación (200, 304 y
 * bloqueado) y embebe la pantalla de la empresa; todo sin enviar un correo real, sin tocar producción y con la posibilidad de
 * reiniciarlo. Es el recorrido que sigue un integrador externo leyendo la guía.
 */

const MAILPIT = process.env.E2E_MAILPIT ?? 'http://localhost:8025';

/** Lo que necesita un sistema como el SRM en el sandbox (la llave de prueba lleva los mismos permisos que llevaría en producción). */
const SCOPES = [
  'configuracion:leer', 'flujos:leer', 'vinculos:leer', 'vinculos:escribir', 'vinculos:bloquear', 'expedientes:leer', 'expedientes:administrar',
  'certificados:leer', 'consultas:leer', 'eventos:leer', 'webhooks:administrar', 'sandbox:administrar', 'documentos:leer', 'terceros:leer',
];

interface ApiKey {
  id: string;
  valor: string | null;
}

/** Producción: un flujo de EMPLEADO con un área automática (abrir el expediente lo aprueba). El sandbox lo hereda al crearse. */
async function flujoAutomatico(e: Escenario, empresa: Empresa): Promise<void> {
  const sufijo = e.sufijo.slice(-5).toUpperCase();
  const area = await llamar<{ id: string }>('POST', '/api/v1/areas', { llave: e.admin, empresa: empresa.id, cuerpo: { codigo: `AUTO_${sufijo}`, nombre: 'Validación automática' } });
  const flujo = await llamar<{ id: string }>('POST', '/api/v1/flujos', {
    llave: e.admin,
    empresa: empresa.id,
    cuerpo: {
      codigo: `EMPLEADOS_${sufijo}`,
      nombre: 'Empleados',
      tipoVinculo: 'EMPLEADO',
      etapas: [{ orden: 1, areas: [{ areaId: area.id, aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: true, slaHoras: null }] }],
    },
  });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: e.admin, empresa: empresa.id, cuerpo: {} });
}

test('el integrador prueba todo en el sandbox con el SDK: invitación, webhooks, certificación y pantalla embebida, sin tocar producción', async ({ page }) => {
  const e = await crearEscenario();
  await flujoAutomatico(e, e.norte);

  // 1. El administrador emite la llave de prueba del sistema (como cualquier llave, con «sandbox»): eso crea el sandbox del grupo.
  const sistema = (await llamar<{ id: string; codigo: string }[]>('GET', '/api/v1/sistemas', { llave: e.admin })).find((s) => s.codigo === 'E2E');
  expect(sistema).toBeDefined();
  const prueba = await llamar<ApiKey>('POST', '/api/v1/api-keys', {
    llave: e.admin,
    cuerpo: { nombre: 'E2E sandbox', scopes: SCOPES, empresas: null, expiraEn: null, sandbox: true, sistemaId: sistema?.id },
  });
  expect(prueba.valor).toMatch(/^vsk_test_/);

  // 2. El SDK pregunta por el sandbox: otro tenant, con una copia de pruebas de cada empresa (y el id para X-Empresa).
  const base = new ClienteValidasoft({ urlBase: API, apiKey: prueba.valor ?? '', reintentos: 0 });
  const estado = await base.sandbox.obtener();
  expect(estado).toMatchObject({ existe: true, entorno: 'sandbox', sistemas: ['E2E'] });
  expect(estado.tenantId).not.toBe(e.tenantId);
  const norte = estado.empresas.find((x) => x.produccionId === e.norte.id);
  expect(norte).toMatchObject({ nombre: 'Constructora Norte S.A.S. (sandbox)', prefijoFolio: `T${e.norte.prefijo}`, activa: true });
  if (!norte) throw new Error('El sandbox no copió Norte.');
  const sandbox = base.paraEmpresa(norte.id);

  const receptor = await Receptor.iniciar();
  try {
    // 3. El webhook del sistema apunta a su receptor de pruebas (en el sandbox, sus propias suscripciones).
    const webhook = await llamar<{ secreto: string }>('POST', '/api/v1/webhooks', {
      llave: prueba.valor ?? '',
      cuerpo: { url: receptor.url, tipos: ['certificado.emitido', 'vinculo.bloqueado'], empresas: [norte.id], sistemaId: sistema?.id, descripcion: 'SRM (sandbox)' },
    });
    expect(webhook.secreto).toMatch(/^whsec_/);

    // 4. Invitación: el sistema crea el vínculo; el correo no sale, queda en el buzón con el enlace del tercero.
    const identificacion = nitAleatorio();
    const correo = `empleado.${identificacion.numero}@e2e.validasoft.co`;
    const vinculo = await sandbox.vinculos.crear(
      {
        tercero: { identificacion, tipoPersona: 'Juridica', nombre: `Servicios ${identificacion.numero} S.A.S.`, contactos: [{ nombre: 'Ana', correo, rol: 'Comercial' }] },
        tipoVinculo: 'EMPLEADO',
        correoInvitacion: correo,
        referenciaExterna: { sistema: 'E2E', id: `EMP-${identificacion.numero}` },
      },
      `sbx-${identificacion.numero}`,
    );
    expect(vinculo.empresaId).toBe(norte.id);
    const invitacion = await base.sandbox.esperarCorreo(correo, { asunto: 'te invita' });
    expect(invitacion.asunto).toContain('Constructora Norte S.A.S. (sandbox)');
    const enlace = invitacion.enlaces.find((x) => x.includes('/e/'));
    expect(enlace).toBeDefined();
    const enMailpit = (await (await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:"${correo}"`)}`)).json()) as { messages_count?: number; total?: number };
    expect(enMailpit.messages_count ?? enMailpit.total ?? 0).toBe(0); // nada salió a un buzón real

    // El enlace del buzón abre la invitación del tercero de pruebas, con la empresa del sandbox.
    await page.goto(enlace ?? '');
    await expect(page.getByRole('heading', { level: 1, name: 'Constructora Norte S.A.S. (sandbox) te invita a validar tu información' })).toBeVisible();

    // 5. El sistema abre el expediente: el área automática lo aprueba y se emite el certificado.
    expect((await sandbox.expedientes.abrir(vinculo.id, {}, `sbx-exp-${identificacion.numero}`)).estado).toBe('Aprobado');
    const certificado = (await sandbox.certificados.delVinculo(vinculo.id)).actual;
    expect(certificado?.folio).toMatch(new RegExp(`^T${e.norte.prefijo}-C-`));

    // 6. El webhook llega firmado y su sobre es el mismo que el del pull; es del tenant del sandbox y de su empresa.
    const entrega = await receptor.esperar((x) => x.tipo === 'certificado.emitido' && x.datos.vinculoId === vinculo.id);
    const evento = await validarWebhook({ secretos: webhook.secreto, firma: entrega.firma, cuerpo: entrega.cuerpo });
    expect(evento).toMatchObject({ tipo: 'certificado.emitido', tenantId: estado.tenantId, empresaId: norte.id });
    const delPull = (await sandbox.eventos.listar({ tipos: ['certificado.emitido'] })).datos.find((x) => x.id === evento.id);
    expect(delPull).toEqual(evento);

    // 7. Consulta de certificación (Marco): 200 con ETag, 304 sin cambios y bloqueado al bloquear el vínculo.
    const consulta = { tipoIdentificacion: 'NIT', numero: identificacion.numero, tipoVinculo: 'EMPLEADO' };
    const vigente = await sandbox.consultas.certificacion(consulta);
    expect(vigente.datos).toMatchObject({ certificado: true, estado: 'Vigente', vinculoId: vinculo.id, bloqueado: false });
    expect(await sandbox.consultas.certificacion(consulta, vigente.etag)).toEqual({ datos: null, etag: vigente.etag, noModificado: true });
    await sandbox.vinculos.bloquear(vinculo.id, { motivo: 'Prueba de bloqueo en el sandbox' });
    await receptor.esperar((x) => x.tipo === 'vinculo.bloqueado' && x.datos.vinculoId === vinculo.id);
    expect((await sandbox.consultas.certificacion(consulta, vigente.etag)).datos).toMatchObject({ bloqueado: true, motivoBloqueo: 'Bloqueo' });

    // 8. Pantalla embebida: el backend del sistema canjea la identidad de Ana con la llave de prueba y la app muestra el sandbox.
    const app = await abrirEmbebidoCon(page, {
      empresaId: norte.id,
      rutaInicial: 'certificados',
      sesion: () => sesionDeUsuario(prueba.valor ?? '', e.delegado(e.ana)),
    });
    await expect(app.getByRole('heading', { level: 1, name: 'Certificados' })).toBeVisible();
    await expect(app.getByRole('row').filter({ hasText: `Servicios ${identificacion.numero} S.A.S.` })).toContainText(certificado?.folio ?? '');

    // 9. Nada de esto existe en producción: ni el vínculo, ni el certificado verificable, ni el correo, ni la persona.
    const enProduccion = new ClienteValidasoft({ urlBase: API, apiKey: e.admin, empresaId: e.norte.id, reintentos: 0 });
    expect((await enProduccion.consultas.certificacion(consulta)).datos).toMatchObject({ certificado: false, estadoVinculo: 'SinVinculo', terceroId: null });
    expect((await fetch(`${API}/public/v1/verificar/${certificado?.folio ?? ''}`)).status).toBe(404);
    const usuarios = await llamar<{ correo: string }[]>('GET', '/api/v1/usuarios', { llave: e.admin });
    expect(usuarios.map((u) => u.correo)).toContain(e.ana.correo); // la de producción, creada por el administrador
    expect((await enProduccion.sandbox.obtener()).entorno).toBe('produccion');

    // 10. Reiniciar: vuelve a empezar (mismas empresas y llave, sin vínculo ni correos) y producción no se entera.
    const reiniciado = await base.sandbox.reiniciar();
    expect(reiniciado.empresas.map((x) => x.id)).toEqual(estado.empresas.map((x) => x.id));
    expect(reiniciado.correos).toBe(0);
    await expect(sandbox.vinculos.obtener(vinculo.id)).rejects.toMatchObject({ estado: 404 });
    expect((await sandbox.consultas.certificacion(consulta)).datos?.estadoVinculo).toBe('SinVinculo');
  } finally {
    await receptor.cerrar();
  }
});
