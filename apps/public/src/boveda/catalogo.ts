/**
 * Tipos que el tercero puede subir por su cuenta a su bóveda: los canónicos **globales** de clase `Archivo` del Anexo A
 * de docs/03 (`CatalogoCanonico` en el dominio). Los `PorVinculo` (póliza, paz y salvo…) los pedirá cada empresa con sus
 * requisitos. La API pública aún no expone el catálogo (`GET /api/v1/catalogos/tipos-documento` no acepta la sesión del
 * tercero): ver "Pedidos al backend" en apps/public/README.md.
 */
export interface TipoSubible {
  codigo: string;
  nombre: string;
  ayuda: string;
}

export const TIPOS_SUBIBLES: readonly TipoSubible[] = [
  { codigo: 'RUT', nombre: 'RUT', ayuda: 'El PDF que descargas de la DIAN, con fecha de expedición reciente.' },
  {
    codigo: 'CAMARA_COMERCIO',
    nombre: 'Cámara de comercio',
    ayuda: 'Certificado de existencia y representación legal expedido por tu Cámara de comercio.',
  },
  { codigo: 'CEDULA', nombre: 'Cédula de ciudadanía', ayuda: 'Las dos caras, legibles, en un solo PDF o en una foto.' },
  { codigo: 'ESTADOS_FINANCIEROS', nombre: 'Estados financieros', ayuda: 'Los del último cierre, firmados por el contador.' },
  { codigo: 'CERTIFICACION_BANCARIA', nombre: 'Certificación bancaria', ayuda: 'La que expide tu banco, a nombre del titular de la cuenta.' },
  { codigo: 'DECLARACION_RENTA', nombre: 'Declaración de renta', ayuda: 'La del último año gravable.' },
  { codigo: 'SG_SST', nombre: 'Evaluación SG-SST', ayuda: 'La evaluación de estándares mínimos del sistema de seguridad y salud en el trabajo.' },
  { codigo: 'PASAPORTE', nombre: 'Pasaporte', ayuda: 'La página de datos, legible.' },
  { codigo: 'CERTIFICADO_ANTECEDENTES', nombre: 'Certificado de antecedentes', ayuda: 'Expedido por la entidad oficial.' },
  { codigo: 'HOJA_DE_VIDA', nombre: 'Hoja de vida', ayuda: 'En PDF.' },
  { codigo: 'TITULO_ACADEMICO', nombre: 'Título académico', ayuda: 'Diploma o acta de grado.' },
];

export function tipoSubible(codigo: string | null | undefined): TipoSubible | null {
  return TIPOS_SUBIBLES.find((t) => t.codigo === codigo) ?? null;
}
