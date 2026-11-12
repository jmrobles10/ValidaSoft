/**
 * Textos es-CO del recordatorio de observaciones pendientes (NT-2, NT-3): desde el expediente, en lote desde la bandeja y el
 * recordatorio automático en Plantillas. Se integran en `es-CO.ts` como `recordatorios` (y sus errores en `errores`).
 */

export const recordatorios = {
  recordar: 'Recordar al tercero',
  recordarTitulo: 'Recordar al tercero sus observaciones pendientes',
  yaHoy: 'Ya se le recordó hoy',
  ultimo: 'Último recordatorio: {{fecha}}.',
  ultimoVarios: 'Último recordatorio: {{fecha}} ({{total}} en total).',
  confirmarTitulo: '¿Recordarle sus observaciones al tercero?',
  confirmarSubtitulo: '{{tercero}} · {{folio}}',
  confirmarTexto:
    'Le enviamos un correo con lo que falta corregir y un enlace para responder, y le avisamos en las notificaciones de su portal. Solo se puede una vez al día.',
  confirmar: 'Enviar recordatorio',
  enviado: 'Le recordamos al tercero sus observaciones.',
  lote: {
    recordarSeleccionados_one: 'Recordar ({{count}})',
    recordarSeleccionados_other: 'Recordar ({{count}})',
    recordarTodos: 'Recordar a los observados',
    seleccionarObservados: 'Seleccionar observados',
    quitarSeleccion: 'Quitar selección',
    seleccionar: 'Seleccionar el expediente {{folio}} de {{tercero}}',
    confirmarTituloSeleccion_one: '¿Recordar las observaciones de {{count}} expediente?',
    confirmarTituloSeleccion_other: '¿Recordar las observaciones de {{count}} expedientes?',
    confirmarTituloTodos: '¿Recordar a todos los terceros con observaciones pendientes?',
    confirmarTextoTodos: 'Se le recuerda a cada tercero con un expediente devuelto en {{empresa}}, uno por expediente.',
    confirmarTextoTodosArea: 'Se le recuerda a cada tercero con un expediente devuelto que pasa por el área {{area}}, uno por expediente.',
    confirmarTextoComun:
      'Cada uno recibe un correo con lo que falta corregir y un aviso en su portal. A quien ya se le recordó hoy no se le vuelve a enviar.',
    confirmarSeleccion: 'Enviar recordatorios',
    ninguno: 'No hay terceros por recordar: nadie tiene observaciones pendientes o ya se les recordó hoy.',
    enviados_one: 'Recordamos a {{count}} tercero.',
    enviados_other: 'Recordamos a {{count}} terceros.',
    omitidos_one: '{{count}} omitido: {{motivos}}.',
    omitidos_other: '{{count}} omitidos: {{motivos}}.',
    motivos: {
      YaRecordadoHoy: 'ya se les recordó hoy',
      NoObservado: 'ya no están devueltos al tercero',
      NoEncontrado: 'no se encontraron',
    },
    errores: {
      'lote-vacio': 'Elige al menos un expediente observado.',
      'lote-demasiado-grande': 'Se recuerdan hasta 200 expedientes a la vez: elige menos o usa «Recordar a los observados».',
    },
  },
  automatico: {
    titulo: 'Recordatorio automático de observaciones',
    ayuda:
      'Mientras un expediente siga devuelto al tercero, Validasoft le recuerda sus observaciones por correo y en su portal cada cierto número de días, a lo sumo una vez al día. Se detiene cuando el tercero responde.',
    porFlujo: 'Cada flujo puede sobrescribirlo: en sus datos generales se elige seguir lo de la empresa, apagarlo o fijar sus propios días.',
    activar: 'Enviar recordatorios automáticos',
    cada: 'Cada (días)',
    cadaAyuda: 'Entre 1 y 60 días desde que se devolvió o desde el último recordatorio.',
    cadaInvalido: 'Escribe un número entero de 1 a 60.',
    guardado: 'Guardamos el recordatorio automático.',
    errorCarga: 'No pudimos cargar el recordatorio automático.',
    soloLectura: 'Solo quien administra la configuración puede cambiarlo.',
  },
} as const;

export const erroresRecordatorios = {
  'expediente-no-observado': 'El expediente ya no está devuelto al tercero, así que no hay observaciones que recordar. Recarga la página.',
  'recordatorio-ya-enviado-hoy': 'Ya se le recordó hoy al tercero. Puedes volver a hacerlo mañana.',
} as const;

/** Recordatorio automático de observaciones por flujo (OP-19), en «Datos generales» del editor de flujos. */
export const flujoRecordatorio = {
  titulo: 'Recordatorio de observaciones',
  ayuda: 'Cada cuánto se le recuerda al tercero lo que le falta corregir mientras el expediente siga devuelto, sin que nadie tenga que hacerlo.',
  modo: 'Recordatorio automático',
  modos: {
    Empresa: { titulo: 'Lo que diga la empresa', ayuda: 'Se usa lo que la empresa configuró en Plantillas de correo.' },
    Apagado: { titulo: 'Nunca en este flujo', ayuda: 'No se le recuerda por su cuenta, aunque la empresa lo tenga encendido.' },
    CadaDias: { titulo: 'Cada cierto número de días', ayuda: 'Se le recuerda con estos días, aunque la empresa lo tenga apagado.' },
  },
  dias: 'Cada (días)',
  diasAyuda: 'Entre 1 y 60 días.',
} as const;
