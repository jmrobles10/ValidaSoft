/**
 * PDF de una página con un texto, válido para la bóveda (magic bytes `%PDF-`, tabla xref correcta). Cada texto da un
 * archivo distinto (otra huella), así que una «versión nueva» del mismo documento no se deduplica.
 */
export function pdf(texto: string): { name: string; mimeType: string; buffer: Buffer } {
  const contenido = `BT /F1 18 Tf 72 720 Td (${texto.replace(/[()\\]/g, '')}) Tj ET`;
  const objetos = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${String(Buffer.byteLength(contenido, 'latin1'))} >>\nstream\n${contenido}\nendstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ];
  let cuerpo = '%PDF-1.4\n';
  const posiciones: number[] = [];
  objetos.forEach((objeto, i) => {
    posiciones.push(Buffer.byteLength(cuerpo, 'latin1'));
    cuerpo += `${String(i + 1)} 0 obj\n${objeto}\nendobj\n`;
  });
  const inicioXref = Buffer.byteLength(cuerpo, 'latin1');
  cuerpo += `xref\n0 ${String(objetos.length + 1)}\n0000000000 65535 f \n`;
  cuerpo += posiciones.map((p) => `${String(p).padStart(10, '0')} 00000 n \n`).join('');
  cuerpo += `trailer\n<< /Size ${String(objetos.length + 1)} /Root 1 0 R >>\nstartxref\n${String(inicioXref)}\n%%EOF\n`;
  const nombre = `${texto.toLowerCase().normalize('NFD').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')}.pdf`;
  return { name: nombre, mimeType: 'application/pdf', buffer: Buffer.from(cuerpo, 'latin1') };
}
