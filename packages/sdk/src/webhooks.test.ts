import { createHmac } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import { CABECERA_FIRMA, comprobarFirma, firmarWebhook, leerCabecera, validarWebhook, verificarWebhook, WebhookInvalidoError } from './webhooks';

const SECRETO = 'whsec_secreto-actual-de-prueba-0123456789abcdef';
const ANTERIOR = 'whsec_secreto-anterior-de-prueba-0123456789abcd';
const AHORA = new Date('2026-11-09T15:00:00Z');
const T = Math.floor(AHORA.getTime() / 1000);

const CUERPO = JSON.stringify({
  id: '0195f1c2-7d3e-7a00-8000-0000000000aa',
  tipo: 'certificado.emitido',
  version: 1,
  ocurridoEn: '2026-11-09T14:59:57Z',
  tenantId: '0195f1c2-7d3e-7a00-8000-0000000000bb',
  empresaId: '0195f1c2-7d3e-7a00-8000-0000000000cc',
  cursor: '42',
  datos: { vinculoId: '0195f1c2-7d3e-7a00-8000-000000000001', certificado: { folio: 'N1-C-2026-000001-1' } },
});

/** Como firma el servidor (FirmaDeWebhook.Cabecera): HMAC-SHA256 hex de «t.cuerpo» con cada secreto vigente. */
function firmaDelServidor(secretos: string[], t: number, cuerpo: string): string {
  return `t=${String(t)},${secretos.map((s) => `v1=${createHmac('sha256', s).update(`${String(t)}.${cuerpo}`).digest('hex')}`).join(',')}`;
}

describe('verificador de webhooks', () => {
  it('acepta la firma del servidor y devuelve el evento', async () => {
    const firma = firmaDelServidor([SECRETO], T, CUERPO);
    expect(await verificarWebhook({ secretos: SECRETO, firma, cuerpo: CUERPO, ahora: AHORA })).toBe(true);
    const evento = await validarWebhook<{ vinculoId: string }>({ secretos: SECRETO, firma, cuerpo: CUERPO, ahora: AHORA });
    expect([evento.id, evento.tipo, evento.cursor, evento.datos.vinculoId]).toEqual([
      '0195f1c2-7d3e-7a00-8000-0000000000aa',
      'certificado.emitido',
      '42',
      '0195f1c2-7d3e-7a00-8000-000000000001',
    ]);
    // Firmar con el SDK da exactamente la cabecera del servidor; el cuerpo en bytes también verifica.
    expect(await firmarWebhook(SECRETO, T, CUERPO)).toBe(firma);
    expect(await verificarWebhook({ secretos: SECRETO, firma, cuerpo: new TextEncoder().encode(CUERPO), ahora: AHORA })).toBe(true);
  });

  it('durante la rotación del secreto vale con cualquiera de los dos', async () => {
    const firma = firmaDelServidor([SECRETO, ANTERIOR], T, CUERPO);
    expect(await verificarWebhook({ secretos: ANTERIOR, firma, cuerpo: CUERPO, ahora: AHORA })).toBe(true);
    expect(await verificarWebhook({ secretos: SECRETO, firma, cuerpo: CUERPO, ahora: AHORA })).toBe(true);
    const soloNueva = firmaDelServidor([SECRETO], T, CUERPO);
    expect(await verificarWebhook({ secretos: ANTERIOR, firma: soloNueva, cuerpo: CUERPO, ahora: AHORA })).toBe(false);
    expect(await verificarWebhook({ secretos: [ANTERIOR, SECRETO], firma: soloNueva, cuerpo: CUERPO, ahora: AHORA })).toBe(true);
  });

  it('rechaza otro secreto, un cuerpo alterado y cabeceras mal formadas', async () => {
    const firma = firmaDelServidor([SECRETO], T, CUERPO);
    expect(await comprobarFirma({ secretos: 'whsec_otro', firma, cuerpo: CUERPO, ahora: AHORA })).toBe('firma-no-coincide');
    expect(await comprobarFirma({ secretos: SECRETO, firma, cuerpo: CUERPO.replace('000001-1', '000001-2'), ahora: AHORA })).toBe('firma-no-coincide');
    // Volver a serializar el JSON cambia el texto y rompe la firma: hay que verificar lo recibido tal cual.
    const reserializado = JSON.stringify(JSON.parse(CUERPO), null, 2);
    expect(await verificarWebhook({ secretos: SECRETO, firma, cuerpo: reserializado, ahora: AHORA })).toBe(false);
    for (const mala of [undefined, '', 'v1=abc', `t=${String(T)}`, `t=hoy,v1=${'a'.repeat(64)}`]) {
      expect(await comprobarFirma({ secretos: SECRETO, firma: mala, cuerpo: CUERPO, ahora: AHORA })).toBe('cabecera-invalida');
    }
    await expect(validarWebhook({ secretos: 'whsec_otro', firma, cuerpo: CUERPO, ahora: AHORA })).rejects.toBeInstanceOf(WebhookInvalidoError);
  });

  it('rechaza firmas a más de cinco minutos (o la tolerancia configurada)', async () => {
    const vieja = firmaDelServidor([SECRETO], T - 6 * 60, CUERPO);
    const futura = firmaDelServidor([SECRETO], T + 6 * 60, CUERPO);
    expect(await comprobarFirma({ secretos: SECRETO, firma: vieja, cuerpo: CUERPO, ahora: AHORA })).toBe('fuera-de-tolerancia');
    expect(await comprobarFirma({ secretos: SECRETO, firma: futura, cuerpo: CUERPO, ahora: AHORA })).toBe('fuera-de-tolerancia');
    expect(await verificarWebhook({ secretos: SECRETO, firma: firmaDelServidor([SECRETO], T - 4 * 60, CUERPO), cuerpo: CUERPO, ahora: AHORA })).toBe(true);
    expect(await verificarWebhook({ secretos: SECRETO, firma: vieja, cuerpo: CUERPO, ahora: AHORA, toleranciaSegundos: 600 })).toBe(true);
  });

  it('un cuerpo firmado que no es un evento se rechaza como cuerpo inválido', async () => {
    const firma = await firmarWebhook(SECRETO, T, 'no es json');
    await expect(validarWebhook({ secretos: SECRETO, firma, cuerpo: 'no es json', ahora: AHORA })).rejects.toMatchObject({ motivo: 'cuerpo-invalido' });
  });

  it('lee la firma de cabeceras de Node (minúsculas, arreglos) y de fetch', () => {
    expect(leerCabecera({ 'validasoft-signature': 't=1,v1=x', 'content-type': 'application/json' }, 'Validasoft-Signature')).toBe('t=1,v1=x');
    expect(leerCabecera({ 'Validasoft-Signature': ['t=2,v1=y'] }, CABECERA_FIRMA)).toBe('t=2,v1=y');
    expect(leerCabecera(new Headers({ 'Validasoft-Signature': 't=3,v1=z' }), CABECERA_FIRMA)).toBe('t=3,v1=z');
    expect(leerCabecera({}, CABECERA_FIRMA)).toBeUndefined();
  });
});
