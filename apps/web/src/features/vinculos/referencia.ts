/** "SRM · PRV-002", "SRM: PRV-002" o solo "PRV-002" (con el sistema de la sesión). */
export function interpretarReferencia(texto: string, sistemaSesion: string | null): { sistema: string; id: string } | null {
  const conSistema = /^\s*([A-Za-z0-9_-]+)\s*[·:|/]\s*(\S.*?)\s*$/.exec(texto);
  if (conSistema?.[1] && conSistema[2]) return { sistema: conSistema[1].toUpperCase(), id: conSistema[2] };
  const id = texto.trim();
  return id && sistemaSesion ? { sistema: sistemaSesion, id } : null;
}
