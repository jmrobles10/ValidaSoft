/**
 * Textos es-CO del cruce sección ↔ documento (F5, FM-8, FM-13): el panel del requisito de sección, el del documento usado como
 * soporte, la justificación al aprobar con discrepancias y la opción «Usar como soporte» del editor de flujos. Se integran en
 * `es-CO.ts` como `cruce`.
 */
export const cruce = {
  titulo: 'Cruce con el documento',
  tituloCon: 'Cruce con {{documento}} · versión {{n}}',
  confianza: 'Confianza de la lectura: {{valor}}',
  ilegible: 'El documento no se lee con seguridad: nada se pudo verificar.',
  resumen: '{{coinciden}} coinciden · {{difieren}} difieren · {{noVerificables}} sin verificar',
  campo: 'Campo',
  declarado: 'Lo declarado',
  documento: 'Lo que dice el documento',
  resultado: 'Resultado',
  critico: 'Crítico',
  vacio: '—',
  resultados: { Coincide: 'Coincide', Discrepa: 'Difiere', NoVerificable: 'Sin verificar' },
  discrepanciaCritica:
    'Hay diferencias en campos críticos: no se aprueba en lote. Observa el documento, observa la sección o aprueba con una justificación.',
  manual: 'Sin cruce automático: tu empresa no recibió la lectura de {{documentos}}. Revísalo manualmente.',
  sinDocumento: 'Ningún documento de la bóveda respalda esta sección todavía.',
  evidencia: 'Es evidencia para la revisión: el cruce no aprueba ni observa nada por sí mismo.',
  soporte: {
    titulo: 'Respaldo de la información',
    sinAnalisis: 'Tu empresa no recibió la lectura automática de esta versión: compara a mano con las secciones.',
    seccionSinDiligenciar: '{{seccion}}: sin diligenciar.',
    seccionSinCruce: '{{seccion}} (versión {{n}}): sin cruce.',
    seccion: '{{seccion}} (versión {{n}})',
  },
  justificacion: {
    etiqueta: 'Justificación de la aprobación',
    ayuda: 'El cruce con el documento difiere en campos críticos de {{requisitos}}. Explica por qué apruebas (al menos 10 caracteres).',
  },
  editor: {
    usarComoSoporte: 'Usar como soporte de las secciones',
    ayuda: 'El documento se cruza con {{secciones}} y el revisor ve las diferencias.',
  },
} as const;

export const erroresCruce = {
  'justificacion-por-discrepancias': 'El cruce con el documento difiere en campos críticos: escribe una justificación para aprobar.',
  'cruce-con-discrepancias': 'Difiere de la información del tercero en campos críticos: revísalo en su expediente.',
  'soporte-no-aplica': 'Ese documento no respalda ninguna sección de la información.',
} as const;
