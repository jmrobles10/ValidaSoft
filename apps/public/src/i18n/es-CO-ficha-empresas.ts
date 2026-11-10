/**
 * Textos de las secciones de «Mi información» que piden las empresas (F2, FM-25..FM-28): la sección como requisito del
 * expediente, la observación de una empresa en el wizard y las etiquetas de los campos señalados (rutas snake_case).
 * Se integran en `es-CO.ts` como `expediente.seccion`, `ficha.observada` y `ficha.campos`.
 */

export const seccionEnExpediente = {
  version: 'Versión {{n}} de tu información · {{fecha}}',
  sinVersion: 'Aún no la diligencias',
  faltanTitulo: 'Lo que le falta para {{empresa}}',
  completar: 'Completar en Mi información',
  corregir: 'Corregir en Mi información',
  ayuda: 'Esta información no se sube aquí: la diligencias una sola vez en «Mi información» y le llega a cada empresa.',
} as const;

export const fichaObservada = {
  titulo: '{{empresa}} pidió corregir esta sección',
  campos: 'Datos por corregir: {{campos}}',
  campoSenalado: 'Tu empresa pidió corregir este dato.',
} as const;

/** Etiquetas de los campos de las secciones por su clave snake_case (lo que señala una empresa al observar). */
export const camposFicha = {
  soportes: 'Documento que respalda esta información',
  razon_social: 'Razón social',
  nombres: 'Nombres',
  apellidos: 'Apellidos',
  correo: 'Correo electrónico',
  telefono: 'Teléfono',
  pais: 'País',
  ciudad: 'Ciudad',
  direccion: 'Dirección',
  direccion_estructurada: 'Dirección por partes',
  ciiu_principal: 'Actividad económica principal (CIIU)',
  ciiu_secundarias: 'Actividades secundarias',
  pagina_web: 'Página web',
  certificado_iso: 'Certificado ISO',
  anio_inicio: 'Año de inicio de actividades',
  responsabilidades: 'Responsabilidades (casilla 53 del RUT)',
  resolucion_gran_contribuyente: 'Resolución de gran contribuyente',
  resolucion_autorretenedor: 'Resolución de autorretenedor',
  responsable_ica: 'Contribuyente de ICA',
  autorretenedor_ica: 'Autorretenedor de ICA',
  ica: 'Municipios donde declaras ICA',
  fecha_corte: 'Fecha de corte',
  activos: 'Activos totales',
  pasivos: 'Pasivos totales',
  patrimonio: 'Patrimonio',
  ingresos_brutos: 'Ingresos brutos',
  ingresos_netos: 'Ingresos netos',
  gastos_operacionales: 'Gastos operacionales',
  gastos_no_operacionales: 'Gastos no operacionales',
  otros_ingresos: 'Otros ingresos',
  moneda_extranjera: 'Operaciones en moneda extranjera',
  personas: 'Personas relacionadas',
  revisor_fiscal_firma: 'Firma de revisoría fiscal',
  programa_salud_ocupacional: 'Programa de salud ocupacional (SG-SST)',
  identifica_factores_de_riesgo: 'Identificación y control de factores de riesgo',
  comite_paritario: 'Comité paritario (COPASST) o vigía',
  programa_higiene_y_seguridad: 'Programa de higiene y seguridad industrial',
  gestion_ambiental: 'Gestión ambiental',
  reglamento_interno: 'Reglamento interno de trabajo',
  politica_hseq: 'Política HSEQ',
  licencias_y_permisos: 'Licencias y permisos',
  especialidades: 'Especialidades',
} as const;
