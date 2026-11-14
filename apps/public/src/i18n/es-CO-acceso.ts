/** Textos es-CO del acceso aislado y de «Mis sesiones» (se integran en `es-CO.ts`). */

export const accesoAislado = {
  /** Lo que ve quien entra con un correo que aún no es contacto del tercero (acceso aislado). */
  aviso: {
    titulo: 'Tu acceso está pendiente de aprobación',
    texto:
      'Tu acceso está pendiente de que un contacto de {{tercero}} lo apruebe. Mientras tanto solo ves y subes lo de tu relación con la empresa que te invitó: ' +
      'no ves la bóveda ni la información de {{tercero}}, y lo que subas solo lo recibe esa empresa. Cuando lo aprueben, cierra sesión y vuelve a entrar para verlo todo.',
    sinNombre: 'este tercero',
    enInvitacion: 'Aceptar esta invitación vale para tu relación con {{empresa}}; no comparte nada de la bóveda del tercero con ella.',
    enSubida: 'Lo que subas queda guardado solo para {{empresa}} hasta que aprueben tu acceso.',
  },
  /** Aviso a quien ya es contacto: hay correos por decidir. */
  pendientes: {
    titulo_one: 'Tienes {{count}} acceso por aprobar',
    titulo_other: 'Tienes {{count}} accesos por aprobar',
    texto: 'Una empresa invitó a un correo que no está entre tus contactos. Mientras nadie lo apruebe, esa persona solo ve y sube lo de su relación con la empresa.',
    revisar: 'Revisar',
  },
  pagina: {
    titulo: 'Accesos por aprobar',
    sub: 'Una empresa invitó a un correo que no está entre tus contactos. Mientras nadie lo apruebe, esa persona entra aislada: solo ve y sube lo de su relación con la empresa, y no ve tu bóveda ni tu información.',
    vacio: 'No hay accesos por decidir',
    vacioTexto: 'Cuando una empresa invite a un correo que no es de tus contactos, te avisamos aquí, en tus notificaciones y por correo.',
    porDecidir: 'Por decidir',
    decididos: 'Decididos',
    errorCarga: 'No pudimos cargar las solicitudes de acceso.',
  },
  tarjeta: {
    invitadoPor: 'Invitado por {{empresas}}',
    desde: 'Desde el {{fecha}}',
    decididoPor: 'Lo decidió {{correo}} el {{fecha}}',
    motivo: 'Motivo: {{motivo}}',
    estados: {
      Pendiente: 'Por decidir',
      Aprobada: 'Aprobado',
      Rechazada: 'Rechazado',
    },
    aprobar: 'Aprobar acceso',
    rechazar: 'Rechazar',
    aprobarEste: 'Aprobar el acceso de {{correo}}',
    rechazarEste: 'Rechazar el acceso de {{correo}}',
    reconsiderar: 'Aprobarlo de todos modos',
    desplazado: 'Estaba como contacto sin verificar y otro contacto de tu empresa se verificó: el verificado prevalece.',
    laPlataformaDecide: 'La plataforma revisa esta solicitud porque ningún contacto de tu empresa está verificado.',
    soloVerificados: 'Solo un contacto verificado puede decidir esta solicitud.',
    decididoPorLaPlataforma: 'Lo decidió la plataforma el {{fecha}}',
    decididoPorVerificacion: 'Se aprobó sola el {{fecha}}: el correo quedó verificado',
  },
  /** Contactos del tercero y su verificación (decisión AV): quién puede decidir los accesos de otros correos. */
  contactos: {
    titulo: 'Contactos de tu empresa',
    texto:
      'Estos correos tienen acceso completo a la información de tu empresa. Solo un contacto verificado aprueba o rechaza los accesos de otros correos: un correo se verifica ' +
      'cuando es el del RUT o el de la Cámara de comercio, cuando dos empresas distintas lo invitaron por su cuenta, cuando lo aprueba un contacto ya verificado o cuando lo verifica la plataforma.',
    tuCorreo: 'Tu correo',
    verificado: 'Verificado',
    sinVerificar: 'Sin verificar',
    desplazado: 'En acceso aislado',
    medios: {
      CorreoDelRut: 'Correo del RUT',
      CorreoDeLaCamara: 'Correo de la Cámara de comercio',
      Corroborado: 'Invitado por dos empresas distintas',
      Plataforma: 'Verificado por la plataforma',
      Importacion: 'Traído del sistema de origen',
      AprobadoPorContacto: 'Aprobado por un contacto verificado',
    },
    avisoSinVerificar: 'Tu correo aún no está verificado: ves las solicitudes, pero solo un contacto verificado las aprueba o rechaza.',
    avisoLaPlataforma: 'Mientras ningún contacto esté verificado, la plataforma decide los accesos de otros correos.',
    avisoDesplazado: 'Tu correo quedó en acceso aislado porque otro contacto de tu empresa se verificó: un contacto verificado debe aprobarlo de nuevo.',
    errorCarga: 'No pudimos cargar los contactos de tu empresa.',
    historial: 'Historial de los contactos',
    entradas: {
      registrado: '{{correo}} entró como contacto ({{detalle}})',
      verificado: '{{correo}} quedó verificado ({{detalle}})',
      paso_a_aislado: '{{correo}} pasó a acceso aislado: {{detalle}}',
    },
  },
  dialogo: {
    aprobarTitulo: 'Aprobar el acceso de {{correo}}',
    aprobarTexto:
      'Ese correo pasará a ser contacto verificado de tu empresa: verá tu bóveda y tu información, podrá subir y retirar documentos y aprobar a otros correos. ' +
      'Lo que subió para {{empresas}} pasará a tu bóveda. Apruébalo solo si conoces a esa persona.',
    aprobarConfirmar: 'Sí, aprobar',
    rechazarTitulo: 'Rechazar el acceso de {{correo}}',
    rechazarTexto:
      'Sus sesiones se cierran al instante y no vuelve a entrar a tu cuenta con ese correo. {{empresas}} verá que se rechazó y podrá invitar a otro correo.',
    rechazarConfirmar: 'Sí, rechazar',
    motivo: 'Motivo (opcional)',
    motivoAyuda: 'Solo lo ven los contactos de tu empresa: ni la empresa ni esa persona.',
    cancelar: 'Cancelar',
  },
  exito: {
    aprobado: 'Aprobaste el acceso de {{correo}}.',
    rechazado: 'Rechazaste el acceso de {{correo}}.',
  },
} as const;

export const sesiones = {
  titulo: 'Tus sesiones abiertas',
  texto: 'Cada navegador donde entraste con tu correo es una sesión. Si no reconoces una, ciérrala: deja de funcionar al instante.',
  esta: 'Esta sesión',
  cerrar: 'Cerrar sesión',
  cerrarEsta: 'Cerrar esta sesión',
  cerrarEnDispositivo: 'Cerrar la sesión de {{dispositivo}} ({{correo}})',
  abierta: 'Abierta el {{fecha}}',
  ultimoUso: 'Último uso {{fecha}}',
  desconocido: 'Navegador desconocido',
  cerrada: 'Cerraste la sesión.',
  errorCarga: 'No pudimos cargar tus sesiones.',
  errorCerrar: 'No pudimos cerrar la sesión. Inténtalo de nuevo.',
  vacio: 'No hay sesiones abiertas',
  dispositivo: '{{navegador}} en {{sistema}}',
} as const;
