/** Textos es-CO de los plazos de revisión (OP-10…OP-12; se integran en `es-CO.ts` como `sla`). Un solo término: «plazo de revisión». */

export const sla = {
  titulo: 'Plazos de revisión',
  subtitulo: 'Cuánto tiene cada área para revisar un expediente de {{empresa}} y cuándo se avisa. El plazo que defines en un flujo manda sobre el de aquí.',
  errorCarga: 'No pudimos cargar los plazos de revisión.',
  soloLectura: 'Puedes ver los plazos, pero no cambiarlos: pide el permiso de configuración a un administrador.',
  guardar: 'Guardar cambios',
  guardado: 'Plazos de revisión guardados.',
  avisos: {
    titulo: 'Avisos',
    porcentaje: 'Avisar al consumir (%)',
    porcentajeAyuda: 'Entre 50 y 95. Quienes revisan el área reciben el aviso al consumirse ese porcentaje del plazo.',
    errores: { requerido: 'Escribe un porcentaje.', rango: 'Escribe un número entre 50 y 95.' },
    administradores: 'Avisar a los administradores cuando se incumple',
    administradoresAyuda: 'Les llega un aviso en la campana y por correo cuando vence el plazo de un área.',
  },
  areas: {
    titulo: 'Plazo por defecto de cada área',
    ayuda: 'En horas. Un área sin plazo no tiene avisos. Si el flujo define el suyo, ese es el que cuenta.',
    area: 'Área',
    horas: 'Plazo (horas)',
    horasDe: 'Plazo en horas de {{area}}',
    errorHoras: 'De 1 a 2160 horas (90 días).',
    vacio: 'Esta empresa aún no tiene áreas activas.',
    vacioAyuda: 'Crea o activa un área en Configuración y vuelve aquí para ponerle su plazo.',
  },
  /** Indicador de la bandeja (OP-12): una fila por expediente y área. */
  indicador: {
    columna: 'Plazo de revisión',
    estados: { EnTiempo: 'A tiempo', PorVencer: 'Por vencer', Vencido: 'Vencido' },
    vence: 'vence el {{fecha}}',
    vencio: 'venció el {{fecha}}',
    sinPlazo: 'Sin plazo',
  },
} as const;

export const erroresSla = {
  'porcentaje-invalido': 'El aviso previo sale cuando se consume entre el 50 y el 95 % del plazo.',
} as const;
