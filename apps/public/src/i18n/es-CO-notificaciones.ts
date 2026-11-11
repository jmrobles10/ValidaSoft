/** Textos es-CO de la campana del tercero y sus preferencias de correo (se integran en `es-CO.ts`). */

export const notificaciones = {
  titulo: 'Notificaciones',
  abrir: 'Notificaciones',
  abrirConContador_one: 'Notificaciones, {{count}} sin leer',
  abrirConContador_other: 'Notificaciones, {{count}} sin leer',
  marcarTodas: 'Marcar todas como leídas',
  marcadas_one: 'Marcamos {{count}} notificación como leída.',
  marcadas_other: 'Marcamos {{count}} notificaciones como leídas.',
  sinLeer: 'Sin leer',
  vacio: 'No tienes notificaciones',
  vacioAyuda: 'Aquí te avisamos cuando una empresa te pida corregir algo, cuando un documento esté por vencer o cuando se decida tu validación.',
  hayMas: 'Mostramos las 30 más recientes.',
  errorCarga: 'No pudimos cargar tus notificaciones.',
  errorMarcar: 'No pudimos marcarla como leída. Inténtalo de nuevo.',
  sinEnlace: 'Esta notificación no tiene una pantalla a la que ir.',
  preferencias: {
    abrir: 'Preferencias de correo',
    titulo: 'Preferencias de correo',
    ayuda:
      'En las notificaciones siempre ves todo lo que pasa con tus empresas. Aquí eliges cuáles te llegan además por correo, a la dirección de tu invitación.',
    sinCambios: 'No has cambiado nada.',
    guardar: 'Guardar',
    guardadas: 'Guardamos tus preferencias de correo.',
    errorCarga: 'No pudimos cargar tus preferencias.',
  },
  tipos: {
    observado: {
      titulo: 'Observaciones de una empresa',
      explicacion: 'Cuando una empresa revisa tu validación y te pide corregir algo.',
    },
    recordatorio: {
      titulo: 'Recordatorio de observaciones',
      explicacion: 'Cuando una empresa te recuerda lo que sigues teniendo por corregir.',
    },
    resultado: {
      titulo: 'Resultado de tu validación',
      explicacion: 'Cuando una empresa aprueba tu validación o la cierra sin aprobarla.',
    },
    documentoPorVencer: {
      titulo: 'Documento por vencer',
      explicacion: 'Cuando un documento que entregaste está cerca de vencer.',
    },
    documentoVencido: {
      titulo: 'Documento vencido',
      explicacion: 'Cuando un documento que entregaste venció y ya pasó su plazo de gracia.',
    },
    certificado: {
      titulo: 'Certificado revocado o suspendido',
      explicacion: 'Cuando una empresa revoca o suspende tu certificado.',
    },
    plazoDeCargue: {
      titulo: 'Plazo para completar tu validación',
      explicacion: 'Cuando se acerca el plazo, cuando caduca tu validación o cuando la empresa la reabre.',
    },
  },
} as const;
