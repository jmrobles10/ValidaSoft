import type { CertificadoPublicoDto, ClavesDeCertificadosDto, JsonElement } from '@validasoft/api-client';
import { aHex, sha256 } from '../verificacion/cripto';
import { bytesCanonicos } from '../verificacion/jcs';

/**
 * Certificados de prueba para la verificación pública. `REAL_*` es una emisión tal como la devolvió la API local
 * (sellada por el backend en C# con la clave de desarrollo `vs-desarrollo-solo-local`): prueba que el JCS y el SHA-256
 * de aquí producen los mismos bytes que los del servidor.
 */
export const CERTIFICADO_REAL: CertificadoPublicoDto = {
  folio: 'VERIF-C-2026-000001-1',
  emision: 1,
  estado: 'Vigente',
  bloqueante: false,
  titular: { nombre: 'Cliente de Verificación S.A.S.', identificacionEnmascarada: 'NIT 90*****67' },
  emisor: { empresa: 'Constructora Verificación S.A.S.', logoUrl: null },
  flujo: { codigo: 'VERIFICACION', nombre: 'Flujo de verificación', version: 1 },
  emitidoEn: '2026-11-09T00:02:30.7897957+00:00',
  vigenteHasta: '2026-12-08',
  huella: '26b4·5bd2·c48a',
  hashContenido: '26b45bd2c48a439eb52e5eaceaddc9601463c62735536897f0b025683fd6c8f7',
  contenidoCanonico: {
    areas: [
      { area: 'Compras', codigo: 'COMPRAS', fecha: '2026-11-09T00:02:30Z', origen: 'Humano' },
      { area: 'Cumplimiento', codigo: 'CUMPLIMIENTO', fecha: '2026-11-09T00:02:30Z', origen: 'Humano' },
    ],
    emision: 1,
    emitidoEn: '2026-11-09T00:02:30Z',
    empresa: {
      id: '01a1bce5-2f49-73d2-b110-c1ad7f3986ad',
      identificacion: { digitoVerificacion: '1', numero: '899999068', pais: 'CO', tipo: 'NIT' },
      nombre: 'Constructora Verificación S.A.S.',
    },
    esquema: 'validasoft.certificado.v1',
    flujo: { codigo: 'VERIFICACION', nombre: 'Flujo de verificación', version: 1 },
    folio: 'VERIF-C-2026-000001-1',
    requisitos: [
      {
        codigo: 'CAMARA_COMERCIO',
        eximido: false,
        nombre: 'Certificado de existencia y representación legal',
        sha256: 'b0746c1974830b2b40d6a97c9dd185eba9734fe761fcf372f6fa80cfc5eb97be',
        versionN: 1,
        vigenteHasta: '2026-12-08',
      },
      {
        codigo: 'CERTIFICACION_BANCARIA',
        eximido: false,
        nombre: 'Certificación bancaria',
        sha256: '977b7258ac6186f2d7279428bb0c9c3f29de9352861d72087c1ce5f2a63ed935',
        versionN: 1,
        vigenteHasta: '2027-02-06',
      },
      {
        codigo: 'ESTADOS_FINANCIEROS',
        eximido: false,
        nombre: 'Estados financieros',
        sha256: '02bc63293864c507af12619d27adaee66edc9af845bcb40a914f80d922f8424f',
        versionN: 1,
      },
      {
        codigo: 'RUT',
        eximido: false,
        nombre: 'Registro Único Tributario',
        sha256: '4e35689d3e87e1c7bbd2325e17f7925326ce4fcc0557e1f9b9a1b65aceb702cb',
        versionN: 1,
        vigenteHasta: '2027-11-08',
      },
    ],
    tercero: { identificacionEnmascarada: 'NIT 90*****67', nombre: 'Cliente de Verificación S.A.S.', pais: 'CO', tipoPersona: 'Juridica' },
    tipoVinculo: { codigo: 'CLIENTE', nombre: 'Cliente' },
    verificaciones: [],
    vigenteHasta: '2026-12-08',
  },
  urlVerificacion: 'http://localhost:5181/verificar/VERIF-C-2026-000001-1?huella=26b45bd2c48a',
  pendientes: [],
  firma: {
    algoritmo: 'Ed25519',
    kid: 'vs-desarrollo-solo-local',
    clavePublicaUrl: 'http://localhost:5181/.well-known/validasoft-certificados.json',
    valor: 'zipYPq6fX8JG1zKqaloWcwkegPL6BFprRwKZ+vzr0SXV0kWLbVdvJoXiPqOrsTzRpFXPHMirE0osamab24U0AA==',
  },
  estadoDesde: '2026-11-09T00:02:30.7897957+00:00',
  reemplazadoPor: null,
  pdfDescargable: true,
};

export const CLAVES_REAL: ClavesDeCertificadosDto = {
  emisor: 'Validasoft',
  algoritmo: 'Ed25519',
  mensajeFirmado: 'Ed25519 sobre los 32 bytes del SHA-256 (hashContenido en hexadecimal) del contenidoCanonico canonicalizado con RFC 8785 (JCS).',
  esquemas: ['validasoft.certificado.v1'],
  claves: [
    {
      kid: 'vs-desarrollo-solo-local',
      algoritmo: 'Ed25519',
      clavePublica: 'mi2TRx5jMSmbEY01mZwdTQQl5OcaaVvL07PjrVI5RZ0=',
      jwk: { kty: 'OKP', crv: 'Ed25519', x: 'mi2TRx5jMSmbEY01mZwdTQQl5OcaaVvL07PjrVI5RZ0', kid: 'vs-desarrollo-solo-local', alg: 'EdDSA', use: 'sig' },
      vigente: true,
    },
  ],
};

const aBase64 = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes));

/**
 * Sella `contenidoCanonico` con un par Ed25519 generado aquí (WebCrypto de Node) y arma el certificado y el documento
 * de claves coherentes con él. `cambios` se aplica antes de sellar (estado, reemplazo…).
 */
export async function certificadoSellado(
  cambios: Partial<CertificadoPublicoDto> = {},
  kid = 'vs-prueba',
): Promise<{ certificado: CertificadoPublicoDto; claves: ClavesDeCertificadosDto }> {
  const par = await crypto.subtle.generateKey({ name: 'Ed25519' }, true, ['sign', 'verify']);
  const jwk = await crypto.subtle.exportKey('jwk', par.publicKey);
  const base = { ...CERTIFICADO_REAL, ...cambios };
  const contenido: JsonElement = base.contenidoCanonico;
  const hash = await sha256(bytesCanonicos(contenido));
  const firma = new Uint8Array(await crypto.subtle.sign({ name: 'Ed25519' }, par.privateKey, hash));
  const hex = aHex(hash);
  const certificado: CertificadoPublicoDto = {
    ...base,
    hashContenido: hex,
    huella: `${hex.slice(0, 4)}·${hex.slice(4, 8)}·${hex.slice(8, 12)}`,
    firma: { ...base.firma, kid, valor: aBase64(firma) },
  };
  const x = jwk.x ?? '';
  return {
    certificado,
    claves: { ...CLAVES_REAL, claves: [{ kid, algoritmo: 'Ed25519', clavePublica: '', jwk: { kty: 'OKP', crv: 'Ed25519', x, kid, alg: 'EdDSA', use: 'sig' }, vigente: true }] },
  };
}
