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
    versionNueva: {
      titulo: 'Versión nueva de un documento',
      explicacion: 'Cuando el tercero sube una versión nueva de un documento que tu área está revisando: lo decidido sobre la anterior queda sin efecto.',
    },
    versionRetirada: {
      titulo: 'Versión retirada por el tercero',
      explicacion: 'Cuando el tercero retira una versión que tu área tenía aprobada o en revisión: el expediente vuelve a la versión anterior o se abre una actualización para revisarla.',
    },
    aprobado: {
      titulo: 'Expediente aprobado',
      explicacion: 'Cuando se aprueba la validación de un expediente de tus áreas.',
    },
    rechazado: {
      titulo: 'Expediente rechazado',
      explicacion: 'Cuando se rechaza la validación de un expediente de tus áreas.',
    },
    caducado: {
      titulo: 'Expediente caducado',
      explicacion: 'Cuando un tercero no completa su expediente de tus áreas en el plazo y caduca.',
    },
    loteTerminado: {
      titulo: 'Lote de invitaciones terminado',
      explicacion: 'Cuando termina un lote de invitación masiva que creaste, con cuántas filas se invitaron y cuántas fallaron.',
    },
    porVencer: {
      titulo: 'Documento por vencer',
      explicacion: 'Cuando un documento de un vínculo de tus áreas está cerca de vencer.',
    },
    vencido: {
      titulo: 'Documento vencido',
      explicacion: 'Cuando un documento de un vínculo de tus áreas venció y ya pasó su plazo de gracia.',
    },
    coincidenciaListas: {
      titulo: 'Coincidencia en listas restrictivas',
      explicacion: 'A los oficiales de cumplimiento, cuando una consulta de listas (o el monitoreo de un vínculo activo) encuentra coincidencias por analizar.',
    },
    estudioPendiente: {
      titulo: 'Estudio de cumplimiento pendiente',
      explicacion: 'A los oficiales de cumplimiento, cuando entra a revisión un expediente que necesita su estudio.',
    },
    verificacionExpirada: {
      titulo: 'Verificación vencida',
      explicacion: 'A los oficiales de cumplimiento, cuando vence una consulta de listas o un estudio con el expediente abierto.',
    },
    cupoIa: {
      titulo: 'Cupo de revisión automática',
      explicacion: 'A los administradores, cuando la empresa llega al 80 % y al 100 % de las validaciones con IA incluidas en su plan del mes.',
    },
    habeasSolicitud: {
      titulo: 'Solicitud de habeas data',
      explicacion: 'A quienes responden el habeas data, cuando un tercero pide consultar, corregir o suprimir sus datos: la ley da 10 o 15 días hábiles para responder.',
    },
    habeasPlazo: {
      titulo: 'Plazo de habeas data por vencer',
      explicacion: 'A quienes responden el habeas data, cuando a una solicitud le quedan dos días hábiles o menos y cuando vence.',
    },
    planLimite: {
      titulo: 'Límite del plan',
      explicacion: 'A los administradores, cuando el grupo llega al 80 % y al 100 % de un límite de su plan: usuarios, empresas, vínculos, almacenamiento o llamadas a la API.',
    },
    webhookDeshabilitado: {
      titulo: 'Webhook deshabilitado',
      explicacion: 'A los administradores, cuando una suscripción de webhooks se deshabilita por entregas fallidas seguidas.',
    },
  },
} as const;
