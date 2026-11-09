/** Textos es-CO de la bandeja de revalidaciones (RV-5, se integran en `es-CO.ts`). */

export const revalidaciones = {
  titulo: 'Revalidaciones',
  subtitulo:
    'Documentos que el tercero actualizó después de que {{empresa}} los aprobó. Si no se resuelven en {{dias}} días, el certificado queda bloqueado.',
  columnas: {
    seleccion: 'Seleccionar',
    tercero: 'Tercero',
    documento: 'Documento',
    versiones: 'Versión',
    comparacion: 'Comparación',
    plazo: 'Plazo',
    folio: 'Expediente',
  },
  versiones: 'v{{antes}} → v{{actual}}',
  versionesAccesible: 'Aceptada la versión {{antes}}, llegó la {{actual}}',
  soloActual: 'v{{actual}}',
  comparaciones: {
    SinAnterior: 'Sin anterior',
    Identica: 'Idéntica',
    Distinta: 'Distinta',
    SinCambios: 'Sin cambios',
    CambiosDetectados: 'Con cambios',
  },
  comparacionAyuda: {
    Distinta: 'El archivo es otro. Sin la revisión automática no se sabe qué cambió: revísalo en su expediente.',
    SinCambios: 'La revisión automática no encontró cambios en los campos clave.',
    Identica: 'Es el mismo archivo que la empresa ya aprobó.',
    CambiosDetectados: 'La revisión automática encontró cambios en los campos clave.',
    SinAnterior: 'No hay una versión anterior con qué comparar.',
  },
  quedan_one: 'Queda {{count}} día',
  quedan_other: 'Quedan {{count}} días',
  hasta: 'Hasta el {{fecha}}',
  bloquea: 'Bloquea el certificado',
  seleccionar: 'Seleccionar {{documento}} de {{tercero}}',
  seleccionarTodas: 'Seleccionar las sin cambios',
  quitarSeleccion: 'Quitar selección',
  aprobar_one: 'Aprobar {{count}} sin cambios',
  aprobar_other: 'Aprobar {{count}} sin cambios',
  confirmarTitulo_one: '¿Aprobar {{count}} revalidación?',
  confirmarTitulo_other: '¿Aprobar {{count}} revalidaciones?',
  confirmarTexto:
    'Quedan aprobadas en tus áreas como si las aprobaras una a una, cada una con su propio registro en el historial del expediente.',
  aprobadas_one: 'Aprobaste {{count}} revalidación.',
  aprobadas_other: 'Aprobaste {{count}} revalidaciones.',
  fallidasTitulo_one: '{{count}} no se aprobó',
  fallidasTitulo_other: '{{count}} no se aprobaron',
  sinLote:
    'Ninguna se puede aprobar en lote: sin la revisión automática Validasoft solo sabe que el archivo cambió, así que cada versión nueva se revisa en su expediente.',
  abrir: 'Abrir {{folio}}: {{documento}} de {{tercero}}',
  vacio: 'No tienes revalidaciones pendientes.',
  vacioAyuda: 'Cuando un tercero actualice un documento que ya aprobaste, aparecerá aquí.',
  vacioFiltros: 'No hay revalidaciones con ese filtro.',
  errorLista: 'No se pudieron cargar las revalidaciones.',
  filtros: { area: 'Área', limpiar: 'Limpiar filtros' },
} as const;

export const erroresRevalidaciones = {
  'revalidacion-con-cambios': 'La versión nueva cambió frente a la aceptada: revísala en su expediente.',
  'no-es-revalidacion': 'Esa revalidación ya se cerró.',
  'lote-vacio': 'Elige al menos una revalidación.',
  'lote-demasiado-grande': 'Se aprueban hasta 100 revalidaciones por lote.',
} as const;
