import { expect, test } from '@playwright/test';
import { ClienteValidasoft, ErrorValidasoft, validarWebhook, type EventoPublicoDto } from '@validasoft/sdk';
import { API, llamar, nitAleatorio } from '../soporte/api';
import { abrirEmbebido } from '../soporte/embebido';
import { Receptor } from '../soporte/receptor';
import { crearEscenario, type Empresa, type Escenario } from '../soporte/semilla';

/**
 * H9 (CN-1…CN-6): lo que necesitan el SRM y Marco para hablar con Validasoft, con el SDK TypeScript (@validasoft/sdk)
 * contra la API real: consulta de certificación (200, 304, bloqueado), eventos por pull con el mismo sobre que el webhook
 * firmado, y rotación de API keys desde la pantalla (la anterior vale hasta la fecha y luego responde 401).
 */

interface ApiKey {
  id: string;
  nombre: string;
  valor: string | null;
  rotadaDe?: string | null;
}

function cliente(llave: string, empresa?: Empresa): ClienteValidasoft {
  return new ClienteValidasoft({ urlBase: API, apiKey: llave, empresaId: empresa?.id, reintentos: 0 });
}

async function crearLlave(e: Escenario, nombre: string, scopes: string[], empresa: Empresa): Promise<ApiKey> {
  return llamar<ApiKey>('POST', '/api/v1/api-keys', { llave: e.admin, cuerpo: { nombre, scopes, empresas: [empresa.id], expiraEn: null } });
}

/**
 * Flujo de EMPLEADO con un área automática: abrir el expediente lo aprueba y emite el certificado sin documentos. El
 * SRM (llave del sistema E2E, con el SDK) crea el vínculo y abre el expediente.
 */
async function vinculoCertificado(e: Escenario, empresa: Empresa): Promise<{ vinculoId: string; nit: string }> {
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

  const identificacion = nitAleatorio();
  const correo = `empleado.${identificacion.numero}@e2e.validasoft.co`;
  const srm = cliente(e.llave, empresa);
  const vinculo = await srm.vinculos.crear(
    {
      tercero: { identificacion, tipoPersona: 'Juridica', nombre: `Servicios ${identificacion.numero} S.A.S.`, contactos: [{ nombre: 'Ana', correo, rol: 'Comercial' }] },
      tipoVinculo: 'EMPLEADO',
      correoInvitacion: correo,
      referenciaExterna: { sistema: 'E2E', id: `EMP-${identificacion.numero}` },
    },
    `e2e-${identificacion.numero}`,
  );
  expect((await srm.expedientes.abrir(vinculo.id)).estado).toBe('Aprobado');
  return { vinculoId: vinculo.id, nit: identificacion.numero };
}

test('Marco consulta la certificación con el SDK: 200, 304 sin cambios y bloqueado al bloquear el vínculo', async () => {
  const e = await crearEscenario();
  const { vinculoId, nit } = await vinculoCertificado(e, e.norte);
  const marco = cliente((await crearLlave(e, 'Marco consultas', ['consultas:leer'], e.norte)).valor ?? '', e.norte);
  const consulta = { tipoIdentificacion: 'NIT', numero: nit, tipoVinculo: 'EMPLEADO' };

  const vigente = await marco.consultas.certificacion(consulta);
  expect(vigente.noModificado).toBe(false);
  expect(vigente.etag).toMatch(/^"c-[0-9a-f]+"$/);
  expect(vigente.datos).toMatchObject({ certificado: true, estado: 'Vigente', estadoVinculo: 'Activo', vinculoId, bloqueado: false, motivoBloqueo: null, fuenteRespaldo: false });
  expect(vigente.datos?.folio).toMatch(new RegExp(`^${e.norte.prefijo}-C-`));

  // Sin cambios: 304 (el ERP sigue con su copia).
  expect(await marco.consultas.certificacion(consulta, vigente.etag)).toEqual({ datos: null, etag: vigente.etag, noModificado: true });

  // El SRM bloquea el vínculo: la misma consulta trae otra versión, bloqueada.
  await cliente(e.llave, e.norte).vinculos.bloquear(vinculoId, { motivo: 'Coincidencia en lista restrictiva' });
  const bloqueado = await marco.consultas.certificacion(consulta, vigente.etag);
  expect(bloqueado.noModificado).toBe(false);
  expect(bloqueado.etag).not.toBe(vigente.etag);
  expect(bloqueado.datos).toMatchObject({ bloqueado: true, motivoBloqueo: 'Bloqueo', estado: 'Suspendido', estadoVinculo: 'Bloqueado' });

  // Otra empresa del grupo no ve el vínculo de Norte; un tercero desconocido tampoco existe para Marco.
  expect((await cliente(e.admin, e.sur).consultas.certificacion(consulta)).datos).toMatchObject({ certificado: false, estadoVinculo: 'SinVinculo', terceroId: null });
  expect((await marco.consultas.certificacion({ ...consulta, numero: '811222333' })).datos?.estadoVinculo).toBe('SinVinculo');
  // Sin el permiso propio (la llave del SRM no tiene consultas:leer): 403.
  await expect(cliente(e.llave, e.norte).consultas.certificacion(consulta)).rejects.toMatchObject({ estado: 403 });
});

test('el SRM detrás de un firewall lee por pull los mismos eventos que llegan firmados por webhook', async () => {
  const e = await crearEscenario();
  const receptor = await Receptor.iniciar();
  try {
    const [sistema] = await llamar<{ id: string }[]>('GET', '/api/v1/sistemas', { llave: e.admin });
    const webhook = await llamar<{ secreto: string }>('POST', '/api/v1/webhooks', {
      llave: e.admin,
      cuerpo: { url: receptor.url, tipos: ['certificado.emitido', 'vinculo.bloqueado'], empresas: [e.norte.id], sistemaId: sistema?.id, descripcion: 'SRM E2E' },
    });
    const { vinculoId } = await vinculoCertificado(e, e.norte);
    await cliente(e.llave, e.norte).vinculos.bloquear(vinculoId, { motivo: 'Documento adulterado' });
    const lector = cliente((await crearLlave(e, 'SRM pull', ['eventos:leer'], e.norte)).valor ?? '');

    // Pull filtrado por tipo, recorriendo páginas de a 2 hasta encontrar los dos eventos del vínculo.
    const delVinculo = (eventos: EventoPublicoDto[]) => eventos.filter((x) => x.datos.vinculoId === vinculoId);
    let pull: EventoPublicoDto[] = [];
    await expect
      .poll(async () => {
        pull = [];
        for await (const evento of lector.eventos.recorrer({ tipos: ['certificado.emitido', 'vinculo.bloqueado'], limite: 2 })) pull.push(evento);
        return delVinculo(pull).map((x) => x.tipo);
      }, { timeout: 45_000 })
      .toEqual(['certificado.emitido', 'vinculo.bloqueado']);
    expect(pull.every((x) => x.empresaId === e.norte.id)).toBe(true);
    const cursores = pull.map((x) => Number(x.cursor));
    expect(cursores).toEqual([...cursores].sort((a, b) => a - b));
    // Desde el último cursor no hay nada nuevo de esos tipos (sí otros, como certificado.suspendido).
    const ultimo = pull.at(-1)?.cursor ?? '0';
    const tipos = ['certificado.emitido', 'vinculo.bloqueado'];
    expect(await lector.eventos.listar({ desde: ultimo, tipos })).toEqual({ datos: [], cursor: ultimo, hayMas: false });
    expect((await lector.eventos.listar({ desde: ultimo })).datos.map((x) => x.tipo)).toContain('certificado.suspendido');

    // El webhook del mismo evento se verifica con el SDK y trae exactamente el mismo sobre.
    for (const evento of delVinculo(pull)) {
      const entrega = await receptor.esperar((x) => x.id === evento.id);
      const verificado = await validarWebhook({ secretos: webhook.secreto, firma: entrega.firma, cuerpo: entrega.cuerpo });
      expect(verificado).toEqual(evento);
    }
    await expect(validarWebhook({ secretos: 'whsec_otro', firma: receptor.entregas[0]?.firma, cuerpo: receptor.entregas[0]?.cuerpo ?? '' })).rejects.toMatchObject({
      motivo: 'firma-no-coincide',
    });
  } finally {
    await receptor.cerrar();
  }
});

test('la administradora rota una API key: la nueva funciona y la anterior vale hasta la fecha y luego responde 401', async ({ page }) => {
  const e = await crearEscenario();
  const vieja = await crearLlave(e, `Marco producción ${e.sufijo}`, ['consultas:leer'], e.norte);
  const consulta = { tipoIdentificacion: 'NIT', numero: '811222333', tipoVinculo: 'EMPLEADO' };
  expect((await cliente(vieja.valor ?? '', e.norte).consultas.certificacion(consulta)).datos?.estadoVinculo).toBe('SinVinculo');

  // Configuración › API keys: rotar con una hora de solapamiento.
  const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'api-keys');
  await ana.getByRole('button', { name: `Rotar ${vieja.nombre}` }).click();
  const dialogo = ana.getByRole('dialog', { name: `Rotar ${vieja.nombre}` });
  await dialogo.getByRole('radio', { name: 'Una hora más' }).check();
  await dialogo.getByRole('button', { name: 'Rotar clave' }).click();
  const creada = ana.getByRole('dialog', { name: `Clave nueva de «${vieja.nombre}»` });
  const nueva = (await creada.locator('code').innerText()).trim();
  expect(nueva).toMatch(/^vsk_/);
  expect(nueva).not.toBe(vieja.valor);
  await expect(creada).toContainText('La clave anterior vale hasta');
  await creada.getByRole('button', { name: 'Listo, ya la guardé' }).click();
  // La anterior queda «Reemplazada» y ya no se puede volver a rotar.
  const fila = ana.getByRole('row').filter({ has: ana.getByRole('button', { name: `Revocar ${vieja.nombre}` }) });
  await expect(fila.filter({ hasText: 'Reemplazada' })).toHaveCount(1);
  await expect(ana.getByRole('button', { name: `Rotar ${vieja.nombre}` })).toHaveCount(1);

  // Las dos valen durante el solapamiento.
  for (const llave of [vieja.valor ?? '', nueva]) {
    expect((await cliente(llave, e.norte).consultas.certificacion(consulta)).datos?.estadoVinculo).toBe('SinVinculo');
  }

  // Rotar la nueva con 4 segundos de solapamiento: sigue valiendo hasta esa hora y después responde 401.
  const llaves = await llamar<ApiKey[]>('GET', '/api/v1/api-keys', { llave: e.admin });
  const nuevaId = llaves.find((k) => k.rotadaDe === vieja.id)?.id ?? '';
  const tercera = await llamar<ApiKey>('POST', `/api/v1/api-keys/${nuevaId}/rotar`, {
    llave: e.admin,
    cuerpo: { anteriorValidaHasta: new Date(Date.now() + 4_000).toISOString() },
  });
  expect((await cliente(nueva, e.norte).consultas.certificacion(consulta)).datos?.estadoVinculo).toBe('SinVinculo');
  await expect
    .poll(async () => cliente(nueva, e.norte).consultas.certificacion(consulta).then(() => 200, (error: unknown) => (error instanceof ErrorValidasoft ? error.estado : 0)), {
      timeout: 15_000,
      intervals: [1_000],
    })
    .toBe(401);
  expect((await cliente(tercera.valor ?? '', e.norte).consultas.certificacion(consulta)).datos?.estadoVinculo).toBe('SinVinculo');
  // Rotar otra vez una ya rotada: 409.
  await expect(llamar('POST', `/api/v1/api-keys/${nuevaId}/rotar`, { llave: e.admin, cuerpo: {} })).rejects.toMatchObject({ estado: 409, codigo: 'api-key-ya-rotada' });
});
