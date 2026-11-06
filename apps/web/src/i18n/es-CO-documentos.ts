/** Textos es-CO de los documentos del vínculo y el visor (se integran en `es-CO.ts`). */

export const documentos = {
  titulo: 'Documentos compartidos',
  vacio: 'Aún no hay documentos compartidos',
  vacioSinAceptar: 'El tercero comparte sus documentos al aceptar la invitación.',
  vacioAceptada: 'El tercero aceptó, pero aún no tiene en su bóveda los documentos que esta empresa exige.',
  version: 'Versión {{n}} · {{fecha}}',
  hash: 'SHA-256 {{hash}}',
  ver: 'Ver o descargar',
  verAccesible: 'Ver o descargar {{tipo}}, versión {{n}}',
  descargar: 'Descargar',
  cargando: 'Abriendo el documento…',
  sinVisor: 'Este tipo de archivo no se puede ver aquí. Descárgalo para abrirlo.',
  errorVisor: 'No pudimos mostrar el archivo aquí. Descárgalo para abrirlo.',
} as const;

export const erroresDocumentos = {
  'documento-no-encontrado': 'Este documento ya no está compartido con el vínculo. Recarga la página.',
  'version-no-encontrada': 'Esta versión ya no está compartida con el vínculo. Recarga la página.',
} as const;
