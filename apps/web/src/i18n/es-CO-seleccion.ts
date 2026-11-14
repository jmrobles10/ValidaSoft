/** Selección automática del flujo (FR-1): sección del editor de flujos y explicación de por qué un expediente tiene su flujo. */
export const flujoSeleccion = {
  titulo: 'Selección automática',
  ayuda:
    'Cuando se abre un expediente sin indicar el flujo, el sistema revisa los flujos publicados de este tipo de vínculo por prioridad (la menor primero) y usa el primero cuya condición cumpla el tercero. Si ninguno la cumple, usa el flujo por defecto. La condición queda congelada con la versión publicada.',
  seEscogeCuando: 'Se escoge cuando',
  atajos: {
    siempre: 'Sin condición',
    juridica: 'El tercero es persona jurídica',
    natural: 'El tercero es persona natural',
    personalizada: 'Condición personalizada',
  },
  prioridad: 'Prioridad',
  prioridadAyuda: '1 a 999; la menor se evalúa primero y no se repite en el tipo.',
  sinCondicion: 'Sin condición, este flujo solo se usa si lo eligen al abrir el expediente o si es el flujo por defecto del tipo de vínculo.',
  personalizada: 'Se escoge cuando {{condicion}}.',
  enLista: 'Regla {{prioridad}}',
} as const;

/** Por qué el expediente tiene su flujo (detalle del expediente y del vínculo): explícito, por regla o por defecto. */
export const origenDelFlujo = {
  Explicito: 'Se eligió el flujo al abrir el expediente o al crear el vínculo.',
  Regla: 'Se escogió por regla: {{condicion}} (prioridad {{prioridad}}). Gana la primera que se cumple; esta fue la {{evaluadas}}.ª que se evaluó.',
  ReglaSinDetalle: 'Se escogió por una regla de selección.',
  PorDefecto: 'Ninguna regla de selección se cumplió, así que se usó el flujo por defecto del tipo de vínculo.',
} as const;
