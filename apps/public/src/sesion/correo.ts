/** Validación mínima en el navegador (la API valida de nuevo): algo@dominio.tld, sin espacios. */
export function correoValido(correo: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo.trim());
}
