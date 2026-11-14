/** SHA-256 en hexadecimal: se manda al iniciar una carga (detecta el duplicado antes de subir) y el servidor lo verifica al confirmar. */
export async function sha256Hex(archivo: Blob): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await archivo.arrayBuffer());
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');
}
