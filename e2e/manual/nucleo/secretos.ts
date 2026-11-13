/**
 * Qué cuenta como secreto en un pantallazo del manual. Antes de capturar, `captura.ts` cubre con un recuadro «oculto» todo texto (y todo campo) que
 * coincida con estos patrones o que el producto marque con `data-secreto`: el manual se publica, y una llave o un código de recuperación de la
 * base de pruebas no debe verse en él aunque ya esté revocado.
 *
 * Cada patrón es una expresión regular (`origen` + banderas) para poder llevarla a la página con `evaluate`; `grupo` es el grupo de captura que se oculta
 * (0 = todo lo que coincide).
 */
export interface PatronDeSecreto {
  nombre: string;
  origen: string;
  banderas: string;
  grupo: number;
}

/** Alfabeto de los códigos de recuperación (sin I, L, O, 0 ni 1; `Recuperacion.cs`): `ABCDE-FGHJK`. */
const CODIGO_DE_RECUPERACION = '\\b[A-HJ-KM-NP-Z2-9]{5}-[A-HJ-KM-NP-Z2-9]{5}\\b';

export const PATRONES_DE_SECRETOS: readonly PatronDeSecreto[] = [
  // API keys: `vsk_{dev|test|live}_{slug}_{secreto}` (Seguridad.cs).
  { nombre: 'llave de API', origen: '\\bvsk_[A-Za-z0-9]+_[A-Za-z0-9_-]{8,}', banderas: 'g', grupo: 0 },
  // Secreto de firma de un webhook (`whsec_…`).
  { nombre: 'secreto de webhook', origen: '\\bwhsec_[A-Za-z0-9_-]{8,}', banderas: 'g', grupo: 0 },
  // Llave de OpenAI (`sk-…`, `sk-proj-…`).
  { nombre: 'llave de OpenAI', origen: '\\bsk-[A-Za-z0-9_-]{12,}', banderas: 'g', grupo: 0 },
  // Token firmado (JWT).
  { nombre: 'token', origen: '\\beyJ[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}', banderas: 'g', grupo: 0 },
  // Cabecera `Authorization: Bearer …`: se oculta solo el token.
  { nombre: 'token de portador', origen: '\\bBearer\\s+([A-Za-z0-9._~+/=-]{20,})', banderas: 'g', grupo: 1 },
  { nombre: 'código de recuperación', origen: CODIGO_DE_RECUPERACION, banderas: 'g', grupo: 0 },
  // Llave privada en PEM (las públicas y los certificados no son secretos).
  { nombre: 'llave privada', origen: '-----BEGIN [A-Z ]*PRIVATE KEY-----[\\s\\S]*?(?:-----END [A-Z ]*PRIVATE KEY-----|$)', banderas: 'g', grupo: 0 },
  // Enlace de un solo uso en una dirección (`/…/{token}`, `?t={token}`: 43 caracteres en base64url, con mayúsculas, minúsculas y dígitos). Las huellas (SHA-256, solo hexadecimal en minúscula) NO entran:
  // son públicas por diseño (la verificación del certificado las muestra para compararlas).
  {
    nombre: 'token opaco',
    // Hasta 4 separadores: un nombre largo con guiones («Certificado-de-existencia-…») no es un token.
    origen: '(?<=[/=#])(?=[A-Za-z0-9_-]*[A-Z])(?=[A-Za-z0-9_-]*[a-z])(?=[A-Za-z0-9_-]*\\d)(?=(?:[A-Za-z0-9]*[-_]){0,4}[A-Za-z0-9]*(?![A-Za-z0-9_-]))[A-Za-z0-9_-]{32,}',
    banderas: 'g',
    grupo: 0,
  },
];

/** Los fragmentos de `texto` que son secretos (`inicio` y `fin` en caracteres); útil para probar los patrones sin navegador. */
export function secretosEnTexto(texto: string, patrones: readonly PatronDeSecreto[] = PATRONES_DE_SECRETOS): { inicio: number; fin: number; nombre: string }[] {
  const hallados: { inicio: number; fin: number; nombre: string }[] = [];
  for (const patron of patrones) {
    for (const coincidencia of texto.matchAll(new RegExp(patron.origen, patron.banderas))) {
      const valor = coincidencia[patron.grupo];
      if (!valor) continue;
      const inicio = coincidencia.index + (patron.grupo === 0 ? 0 : coincidencia[0].indexOf(valor));
      hallados.push({ inicio, fin: inicio + valor.length, nombre: patron.nombre });
    }
  }
  // Lo que varios patrones reconocen a la vez (una llave «sk-…» también parece un token opaco) cuenta una sola vez.
  const unicos: { inicio: number; fin: number; nombre: string }[] = [];
  for (const h of hallados.sort((a, b) => a.inicio - b.inicio || b.fin - a.fin)) {
    const anterior = unicos[unicos.length - 1];
    if (anterior && h.inicio < anterior.fin) anterior.fin = Math.max(anterior.fin, h.fin);
    else unicos.push({ ...h });
  }
  return unicos;
}
