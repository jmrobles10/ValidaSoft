/** Fecha ISO `aaaa-mm-dd` ⇄ texto `dd/mm/aaaa` del campo de fecha transitorio (ver `camposTransitorios.tsx`). */

const ISO = /^(\d{4})-(\d{2})-(\d{2})/;

/** `aaaa-mm-dd` → `dd/mm/aaaa`; lo que no es una fecha ISO es `''`. */
export function textoDeIso(iso: string | null | undefined): string {
  const partes = ISO.exec(iso ?? '');
  return partes ? `${partes[3] ?? ''}/${partes[2] ?? ''}/${partes[1] ?? ''}` : '';
}

/** `dd/mm/aaaa` → `aaaa-mm-dd`, o `''` si está incompleta o no es una fecha real (`31/02/2026`). */
export function isoDeTexto(texto: string): string {
  const partes = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(texto);
  if (!partes) return '';
  const [dia, mes, anio] = [Number(partes[1]), Number(partes[2]), Number(partes[3])];
  const fecha = new Date(Date.UTC(anio, mes - 1, dia));
  if (fecha.getUTCFullYear() !== anio || fecha.getUTCMonth() !== mes - 1 || fecha.getUTCDate() !== dia) return '';
  return `${partes[3] ?? ''}-${partes[2] ?? ''}-${partes[1] ?? ''}`;
}

/** Solo dígitos, con las barras en su lugar mientras se escribe: `1503` → `15/03`. */
export function mascaraDeFecha(texto: string): string {
  const d = texto.replace(/\D/g, '').slice(0, 8);
  if (d.length <= 2) return d;
  if (d.length <= 4) return `${d.slice(0, 2)}/${d.slice(2)}`;
  return `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
}
