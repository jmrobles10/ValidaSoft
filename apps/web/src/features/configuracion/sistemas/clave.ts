/** Resumen de la clave pública para reconocerla sin mostrar el PEM completo. */
export function resumenClave(pem: string): string {
  const cuerpo = pem.replace(/-----(BEGIN|END) [A-Z ]+-----/g, '').replace(/\s+/g, '');
  return cuerpo.length > 16 ? `${cuerpo.slice(0, 8)}…${cuerpo.slice(-8)}` : cuerpo;
}
