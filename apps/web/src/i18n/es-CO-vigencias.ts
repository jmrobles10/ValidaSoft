// Vigencia periódica del vínculo, plazo de revalidación y renovación (VV-1…VV-6).

/** Editor de flujos: vigencia del vínculo. */
export const flujoVigencia = {
  titulo: 'Vigencia del vínculo',
  ayuda:
    'Cada cuántos días vence la validación del tercero con esta empresa, contados desde su última aprobación. Desde el primer aviso (pre-vencimiento) el tercero ratifica (confirma que sigue igual) o actualiza las secciones de su información que pide este flujo; si se aprueba a tiempo, no vence. 0 = no vence por tiempo.',
  dias: 'Días',
  diasAyuda: '0 = sin vencimiento periódico.',
  avisos: 'Avisos (días antes de vencer)',
  avisosAyuda: 'Separados por coma; el primero abre el pre-vencimiento: el vínculo queda «Por vencer» y se abre la renovación del tercero.',
  gracia: 'Gracia (días)',
  graciaAyuda: 'Días que sigue valiendo después de la fecha.',
} as const;

/** Detalle de la vigencia en el vínculo (pantalla de la empresa). */
export const vigenciaVinculo = {
  vigenteHasta: 'Vigente hasta',
  sinVigencia: 'Sin vencimiento periódico',
  porVencerTitulo: 'La validación de este tercero está en pre-vencimiento',
  porVencerTexto: 'Vence el {{fecha}}. Su renovación está abierta: si ratifica o actualiza su información y la apruebas antes de esa fecha, no vence.',
  porVencerSinRenovacion: 'Vence el {{fecha}}. No tiene una renovación abierta.',
  vencidoTitulo: 'La validación de este tercero venció',
  vencidoTexto: 'Venció el {{fecha}}. Su renovación sigue abierta para ratificar o actualizar su información.',
  vencidoSinRenovacion: 'Venció el {{fecha}}. Aún no se abre su renovación.',
} as const;

/** Mensajes de los códigos 422 de la API de vigencias. */
export const erroresVigencias = {
  'vigencia-vinculo-invalida': 'La vigencia del vínculo va de 0 (sin vencimiento) a 3650 días.',
  'aviso-vigencia-invalido': 'Cada aviso es un número de días antes de vencer, menor que la vigencia (hasta 5 avisos).',
  'gracia-vigencia-invalida': 'La gracia va de 0 a 365 días.',
  'vigencia-sin-secciones': 'Con vigencia del vínculo, el flujo debe pedir al menos una sección de la información del tercero: es lo que ratifica al vencer.',
  'vigencia-seccion-invalida': 'La vigencia de una sección va de 1 a 3650 días.',
  'dias-revalidacion-invalidos': 'El plazo de revalidación va de 1 a 365 días.',
  'vigencia-no-aplica': 'La vigencia y el plazo de revalidación de un documento se configuran en su tipo; en el flujo solo valen para las secciones.',
  'ratificacion-no-permitida': 'Esto no se puede ratificar: la empresa aún no había aceptado esa información.',
  'seccion-incompleta': 'La sección está incompleta. Complétala en «Mi información» antes de ratificar.',
} as const;
