import { ClienteValidasoft } from '@validasoft/sdk';
import { API, llamar, nitAleatorio } from '../../soporte/api';
import { crearEscenario, type Empresa, type Escenario } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

/** Un proveedor certificado por la vía de un flujo con un área automática: abrir el expediente lo aprueba y emite el certificado. */
async function vinculoCertificado(e: Escenario, empresa: Empresa): Promise<{ vinculoId: string; numero: string }> {
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
  const correo = `proveedor.${identificacion.numero}@e2e.validasoft.co`;
  const srm = new ClienteValidasoft({ urlBase: API, apiKey: e.llave, empresaId: empresa.id, reintentos: 0 });
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
  return { vinculoId: vinculo.id, numero: identificacion.numero };
}

recorrido(
  {
    dominio: 'integracion',
    actor: 'Sistema integrador',
    titulo: 'Consulta la certificación de un proveedor con ETag',
    resumen:
      'Un ERP consulta si un proveedor tiene certificado vigente. Guarda el ETag de la respuesta y la próxima vez lo manda en If-None-Match: si nada cambió, recibe 304 y se ahorra el cuerpo; si el vínculo se bloqueó, la respuesta trae la versión nueva.',
    precondiciones: [
      'Un grupo con Constructora Norte, un vínculo de proveedor ya certificado y una llave con permiso para consultar la certificación.',
    ],
    relacionados: ['integracion/eventos-pull-y-en-vivo', 'integracion/api-keys'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const { vinculoId, numero } = await vinculoCertificado(e, e.norte);
    const marco = await llamar<{ valor: string | null }>('POST', '/api/v1/api-keys', {
      llave: e.admin,
      cuerpo: { nombre: 'ERP consultas', scopes: ['consultas:leer'], empresas: [e.norte.id], expiraEn: null },
    });
    const llave = marco.valor ?? '';
    const consulta = `tipoIdentificacion=NIT&numero=${numero}&tipoVinculo=EMPLEADO`;
    const ruta = `/api/v1/consultas/certificacion?${consulta}`;

    const primera = await fetch(`${API}${ruta}`, { headers: { 'X-Api-Key': llave, 'X-Empresa': e.norte.id, Accept: 'application/json' } });
    const etag = primera.headers.get('etag') ?? '';
    expect(primera.status).toBe(200);
    expect(etag).toMatch(/^"c-[0-9a-f]+"$/);
    const cuerpoPrimera = (await primera.json()) as Record<string, unknown>;
    await mostrarCodigo(page, {
      titulo: 'Consulta la certificación de un proveedor',
      peticion: `GET ${ruta}`,
      cabeceras: { 'X-Api-Key': '<llave de consultas>', 'X-Empresa': e.norte.id, Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: { ETag: etag, ...cuerpoPrimera } },
    });
    await paso(page, {
      titulo: 'Pide la certificación de un proveedor',
      explicacion:
        'Con el tipo y número de identificación y el tipo de vínculo, la respuesta dice si el proveedor tiene certificado, su folio, su estado y si el vínculo está bloqueado. ' +
        'Además trae un ETag: una huella de esa versión que tu sistema guarda junto con la respuesta.',
      resaltar: page.locator('#respuesta'),
      variantes: [
        { cuando: 'El proveedor no tiene vínculo con la empresa', entonces: 'La respuesta dice «SinVinculo» y no inventa datos; no es un error.' },
      ],
    });

    const sinCambios = await fetch(`${API}${ruta}`, { headers: { 'X-Api-Key': llave, 'X-Empresa': e.norte.id, 'If-None-Match': etag } });
    expect(sinCambios.status).toBe(304);
    await mostrarCodigo(page, {
      titulo: 'Vuelve a consultar sin cambios',
      peticion: `GET ${ruta}`,
      cabeceras: { 'X-Api-Key': '<llave de consultas>', 'X-Empresa': e.norte.id, 'If-None-Match': etag },
      respuesta: { estado: 304 },
    });
    await paso(page, {
      titulo: 'Usa If-None-Match cuando no haya cambios',
      explicacion:
        'Si mandas el ETag anterior en If-None-Match y la certificación no cambió, la API responde 304 sin cuerpo. Tu sistema conserva su copia y no transfiere datos de nuevo. ' +
        'Un 304 no significa que el proveedor no exista: significa que la versión que tienes sigue siendo la vigente.',
      resaltar: page.locator('#respuesta'),
    });

    await new ClienteValidasoft({ urlBase: API, apiKey: e.llave, empresaId: e.norte.id, reintentos: 0 }).vinculos.bloquear(vinculoId, {
      motivo: 'Coincidencia en lista restrictiva',
    });
    const bloqueada = await fetch(`${API}${ruta}`, { headers: { 'X-Api-Key': llave, 'X-Empresa': e.norte.id, 'If-None-Match': etag } });
    expect(bloqueada.status).toBe(200);
    const cuerpoBloqueada = (await bloqueada.json()) as Record<string, unknown>;
    expect(cuerpoBloqueada).toMatchObject({ bloqueado: true });
    await mostrarCodigo(page, {
      titulo: 'Consulta después de bloquear el vínculo',
      peticion: `GET ${ruta}`,
      cabeceras: { 'X-Api-Key': '<llave de consultas>', 'X-Empresa': e.norte.id, 'If-None-Match': etag },
      respuesta: { estado: 200, cuerpo: { ...cuerpoBloqueada } },
    });
    await paso(page, {
      titulo: 'Recibe la versión nueva cuando algo cambió',
      explicacion:
        'Cuando el vínculo se bloquea, la certificación cambia: la misma consulta, con el ETag anterior, responde 200 con los datos nuevos. ' +
        'Ahí aparece «bloqueado» en verdadero con su motivo, y el estado del vínculo pasa a «Bloqueado». Tu sistema debe reemplazar su copia y no seguir usando la anterior.',
      resaltar: page.locator('#respuesta'),
      variantes: [
        { cuando: 'El vínculo está bloqueado', entonces: 'La respuesta no sirve para considerar al proveedor apto hasta que se desbloquee.' },
      ],
    });
  },
);
