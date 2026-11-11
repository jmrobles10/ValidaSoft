/**
 * Textos es-CO de la ficha aceptada de un vínculo (FD-3): lo que la empresa aceptó del tercero, sección por sección, con su versión
 * y su soporte, separado de lo que el tercero cambió después. Un solo término por cosa: «aceptada» es lo que decidió la empresa y
 * «versión nueva pendiente de revisión» (el mismo de Tercero 360) lo que el tercero guardó después. Se integra en `es-CO.ts` como
 * `fichaAceptada`.
 */
export const fichaAceptada = {
  titulo: 'Ficha aceptada',
  subtitulo: 'Lo que tu empresa aceptó de este tercero, sección por sección, con su soporte.',
  volver: 'Volver al vínculo',
  noEncontrado: 'No encontramos ese vínculo',
  noEncontradoAyuda: 'Puede que ya no exista o que no sea de esta empresa.',
  errorCarga: 'No pudimos cargar la ficha aceptada.',
  vacia: {
    titulo: 'Aún no hay nada aceptado',
    texto: 'Cuando tu empresa apruebe una sección del tercero en un expediente, aparecerá aquí con su versión y su soporte.',
  },
  aviso: {
    titulo_one: '{{count}} sección tiene una versión nueva pendiente de revisión',
    titulo_other: '{{count}} secciones tienen una versión nueva pendiente de revisión',
    texto: 'Lo que ves abajo sigue siendo lo que aceptaste: lo que el tercero guardó después solo vale cuando lo apruebes en su revalidación.',
  },
  resumen: {
    titulo: 'Resumen',
    aceptadas_one: '{{count}} sección aceptada',
    aceptadas_other: '{{count}} secciones aceptadas',
    pendientes_one: '{{count}} con versión nueva pendiente de revisión',
    pendientes_other: '{{count}} con versión nueva pendiente de revisión',
    alDia: 'Nada pendiente de revisión.',
    ayuda: 'Cada empresa acepta por su cuenta: lo que aceptaron otras empresas del grupo no aparece aquí.',
    verTercero: 'Ver la información completa del tercero',
  },
  aceptada: {
    chip: 'Aceptada · versión {{n}}',
    version: 'Versión {{n}} · aceptada el {{fecha}}',
    expediente: 'Expediente {{folio}}',
    verExpediente: 'Ver el expediente {{folio}}',
    importada: 'Importada del sistema de origen',
    soportes: 'Soporte con que se aceptó',
    sinSoportes: 'Sin soporte.',
    datos: 'Datos aceptados',
    verDatos: 'Ver los datos aceptados',
    ocultarDatos: 'Ocultar los datos aceptados',
  },
  posterior: {
    titulo: 'Versión nueva del tercero, pendiente de revisión',
    version: 'Versión {{n}} · guardada el {{fecha}}',
    cambios: 'Qué cambió frente a lo aceptado',
    soportes: 'Soporte de la versión nueva',
    sinSoportes: 'Sin soporte.',
  },
  soporte: {
    etiqueta: '{{nombre}} · versión {{n}}',
    retirado: 'Retirado por el tercero',
  },
  tarjeta: {
    titulo: 'Ficha aceptada',
    ver: 'Ver la ficha aceptada',
    verAccesible: 'Ver la ficha aceptada del vínculo {{tipo}}',
    sinPermiso: 'No tienes permiso para ver la información del tercero.',
    errorCarga: 'No pudimos cargar la ficha aceptada.',
    nada: 'Aún no hay secciones aceptadas.',
  },
} as const;
