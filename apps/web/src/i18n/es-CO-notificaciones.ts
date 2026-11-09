/** Textos es-CO de la campana de notificaciones y las preferencias de correo (se integran en `es-CO.ts`). */

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
  vacioAyuda: 'Aquí te avisamos cuando llegue un expediente a tus áreas, cuando un tercero responda o cuando se decida una validación.',
  hayMas: 'Mostramos las 30 más recientes.',
  errorCarga: 'No pudimos cargar tus notificaciones.',
  errorMarcar: 'No pudimos marcarla como leída. Intenta de nuevo.',
  sinConexion: 'Sin conexión en tiempo real: revisamos cada 30 segundos.',
  preferencias: {
    abrir: 'Preferencias de correo',
    titulo: 'Preferencias de correo',
    ayuda:
      'En la campana siempre ves todas tus notificaciones. Aquí eliges cuáles te llegan además por correo, a la dirección de tu usuario.',
    correo: 'Recibir por correo',
    sinCambios: 'No has cambiado nada.',
    guardadas: 'Guardamos tus preferencias de correo.',
    errorCarga: 'No pudimos cargar tus preferencias.',
  },
  tipos: {
    nuevoEnArea: {
      titulo: 'Nuevo expediente en tu área',
      explicacion: 'Cuando un expediente pasa a revisión en un área de la que eres miembro.',
    },
    terceroRespondio: {
      titulo: 'El tercero respondió',
      explicacion: 'Cuando el tercero corrige lo observado y el expediente vuelve a tu área.',
    },
    aprobado: {
      titulo: 'Expediente aprobado',
      explicacion: 'Cuando se aprueba la validación de un expediente de tus áreas.',
    },
    rechazado: {
      titulo: 'Expediente rechazado',
      explicacion: 'Cuando se rechaza la validación de un expediente de tus áreas.',
    },
    porVencer: {
      titulo: 'Documento por vencer',
      explicacion: 'Cuando un documento de un vínculo de tus áreas está cerca de vencer.',
    },
    webhookDeshabilitado: {
      titulo: 'Webhook deshabilitado',
      explicacion: 'A los administradores, cuando una suscripción de webhooks se deshabilita por entregas fallidas seguidas.',
    },
  },
} as const;
