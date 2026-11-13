// Datos de cada sección tal como viajan en `datos` (docs/14 §4, FM-19): claves en snake_case; `null` o ausente = sin
// diligenciar (distinto de 0 o false). Son los `record` de `Validasoft.Dominio/Ficha/DatosDeSecciones.cs`.

export interface DireccionEstructurada {
  tipo_via?: string | null;
  numero_via?: string | null;
  letra_via?: string | null;
  bis?: boolean | null;
  cuadrante?: string | null;
  numero_cruce?: string | null;
  letra_cruce?: string | null;
  placa?: string | null;
  complemento?: string | null;
}

// ---------- Declaración jurada PEP (dentro de la sección: `declaracion_pep`) ----------

export const RELACIONES_EMPRESA = ['Proveedor', 'Socio', 'Otro'] as const;
export type RelacionEmpresa = (typeof RELACIONES_EMPRESA)[number];

export const CALIDADES_SOCIEDAD = ['Socio', 'Administrador', 'Fideicomitente', 'Otra'] as const;
export type CalidadSociedad = (typeof CALIDADES_SOCIEDAD)[number];

export const FIRMAS_EN_CALIDAD = ['NombrePropio', 'RepresentanteLegal', 'Apoderado'] as const;
export type FirmaEnCalidad = (typeof FIRMAS_EN_CALIDAD)[number];

export interface ConyugePep {
  nombre?: string | null;
  tipo_identificacion?: string | null;
  numero?: string | null;
}

export interface FamiliarPep {
  nombre?: string | null;
  parentesco?: string | null;
  tipo_identificacion?: string | null;
  numero?: string | null;
}

export interface SociedadPep {
  nombre?: string | null;
  identificacion?: string | null;
  calidad?: CalidadSociedad | null;
  calidad_otra?: string | null;
}

export const TIPOS_VINCULO_PEP = ['Familiar', 'AsociadoCercano'] as const;
export type TipoVinculoPep = (typeof TIPOS_VINCULO_PEP)[number];

/** Parentescos de un familiar de la PEP (los textos canónicos de la HU: se guardan tal cual). */
export const PARENTESCOS_PEP = [
  'Padre / Madre · 1.° consanguinidad',
  'Hijo(a) · 1.° consanguinidad',
  'Hermano(a) · 2.° consanguinidad',
  'Abuelo(a) · 2.° consanguinidad',
  'Nieto(a) · 2.° consanguinidad',
  'Suegro(a) · 1.° afinidad',
  'Yerno / Nuera · 1.° afinidad',
  'Hijastro(a) · 1.° afinidad',
  'Cuñado(a) · 2.° afinidad',
  'Hijo(a) adoptivo(a) · 1.° civil',
  'Padre / Madre adoptante · 1.° civil',
] as const;
export const RELACION_CONYUGE = 'Cónyuge o compañero(a) permanente';
/** Relación con la PEP según el tipo de vínculo: un familiar es el cónyuge o un parentesco; un asociado cercano, un socio o asociado comercial. */
export const RELACIONES_PEP: Record<TipoVinculoPep, readonly string[]> = {
  Familiar: [RELACION_CONYUGE, ...PARENTESCOS_PEP],
  AsociadoCercano: ['Socio(a) o asociado(a) comercial'],
};

/** Una persona expuesta políticamente con la que el declarante tiene un vínculo (sección 3), con su cargo y periodo. */
export interface PepRelacionada {
  tipo?: TipoVinculoPep | null;
  relacion?: string | null;
  nombre?: string | null;
  cargo?: string | null;
  entidad?: string | null;
  /** ISO 3166-1 alfa-2. */
  pais?: string | null;
  fecha_vinculacion?: string | null;
  fecha_desvinculacion?: string | null;
  vigente?: boolean | null;
}

/** Sección 3: vínculo familiar o de asociado cercano con otras PEP; con «Sí», al menos una y hasta 30. */
export interface VinculoConPep {
  tiene?: boolean | null;
  vinculos?: PepRelacionada[] | null;
}

/**
 * Declaración jurada PEP de una persona (HU 49093). Los datos básicos (relación, nombre, identificación) y los sellos
 * (`firma_huella`, `fecha_firma`, `firma_ip`) los fija el servidor; lo que se envíe de ellos se ignora.
 */
export interface DeclaracionPep {
  relacion_empresa?: RelacionEmpresa | null;
  relacion_otra?: string | null;
  nombre?: string | null;
  tipo_identificacion?: string | null;
  numero?: string | null;
  nacionalidad?: string | null;
  direccion?: string | null;
  cargo?: string | null;
  institucion?: string | null;
  pais?: string | null;
  fecha_vinculacion?: string | null;
  vigente?: boolean | null;
  fecha_desvinculacion?: string | null;
  cuentas_exterior?: boolean | null;
  paises_cuentas_exterior?: string[] | null;
  conyuge?: ConyugePep | null;
  familiares?: FamiliarPep[] | null;
  sociedades?: SociedadPep[] | null;
  vinculo_con_pep?: VinculoConPep | null;
  acepta_juramento?: boolean | null;
  firma_en_calidad?: FirmaEnCalidad | null;
  firmante_nombre?: string | null;
  firmante_tipo_identificacion?: string | null;
  firmante_numero?: string | null;
  /** PNG en base64, sin el prefijo `data:`. */
  firma_png?: string | null;
  // Sellados por el servidor.
  firma_huella?: string | null;
  fecha_firma?: string | null;
  firma_ip?: string | null;
}

export interface DatosGenerales {
  razon_social?: string | null;
  nombres?: string | null;
  apellidos?: string | null;
  tipo_persona?: string | null;
  tipo_identificacion?: string | null;
  numero?: string | null;
  dv?: string | null;
  correo?: string | null;
  telefono?: string | null;
  pais?: string | null;
  ciudad?: string | null;
  direccion?: string | null;
  direccion_estructurada?: DireccionEstructurada | null;
  ciiu_principal?: string | null;
  ciiu_secundarias?: string[] | null;
  pagina_web?: string | null;
  certificado_iso?: boolean | null;
  anio_inicio?: number | null;
  // Solo persona natural (FD-8): lo propio de ella; la jurídica los declara por persona en la información legal.
  pep?: boolean | null;
  vinculo_pep?: boolean | null;
  fecha_expedicion?: string | null;
  lugar_expedicion?: string | null;
  /** Solo persona natural: se exige con `pep: true` y se conserva «inactiva» si luego responde No. */
  declaracion_pep?: DeclaracionPep | null;
}

export interface Resolucion {
  numero?: string | null;
  fecha?: string | null;
}

export interface FilaIca {
  municipio?: string | null;
  ciiu?: string | null;
  tarifa_por_mil?: number | null;
}

export interface DatosTributarios {
  responsabilidades?: string[] | null;
  resolucion_gran_contribuyente?: Resolucion | null;
  resolucion_autorretenedor?: Resolucion | null;
  responsable_ica?: boolean | null;
  autorretenedor_ica?: boolean | null;
  exento_rete_cree?: boolean | null;
  ica?: FilaIca[] | null;
}

export const TIPOS_OPERACION = ['Importaciones', 'Exportaciones', 'Inversiones', 'Prestamos', 'Giros', 'PagoDeServicios', 'Otras'] as const;
export type TipoOperacionExtranjera = (typeof TIPOS_OPERACION)[number];

export interface MonedaExtranjera {
  realiza?: boolean | null;
  tipos?: TipoOperacionExtranjera[] | null;
  paises?: string[] | null;
}

export interface DatosFinancieros {
  periodo?: number | null;
  fecha_corte?: string | null;
  activos?: number | null;
  pasivos?: number | null;
  patrimonio?: number | null;
  ingresos_brutos?: number | null;
  ingresos_netos?: number | null;
  gastos_operacionales?: number | null;
  gastos_no_operacionales?: number | null;
  otros_ingresos?: number | null;
  moneda_extranjera?: MonedaExtranjera | null;
}

export const ROLES = ['RepresentanteLegal', 'Suplente', 'Apoderado', 'Socio', 'JuntaDirectiva', 'RevisorFiscal', 'Contador'] as const;
export type RolPersona = (typeof ROLES)[number];

export const TIPOS_IDENTIFICACION_PERSONA = ['CC', 'CE', 'NIT', 'PA', 'PPT', 'TI', 'PASAPORTE', 'ID_FISCAL'] as const;

export interface PersonaRelacionada {
  rol?: RolPersona | null;
  tipo_representacion?: string | null;
  tipo_identificacion?: string | null;
  numero?: string | null;
  dv?: string | null;
  nombres?: string | null;
  apellidos?: string | null;
  cargo?: string | null;
  porcentaje?: number | null;
  pep?: boolean | null;
  vinculo_pep?: boolean | null;
  fecha_expedicion?: string | null;
  lugar_expedicion?: string | null;
  fecha_nacimiento?: string | null;
  nacionalidad?: string | null;
  residencia?: string | null;
  correo?: string | null;
  telefono?: string | null;
  declaracion_pep?: DeclaracionPep | null;
}

export interface FirmaRevisora {
  nit?: string | null;
  razon_social?: string | null;
}

export interface DatosLegales {
  personas?: PersonaRelacionada[] | null;
  revisor_fiscal_firma?: FirmaRevisora | null;
}

export const PREGUNTAS_SST = [
  'programa_salud_ocupacional',
  'identifica_factores_de_riesgo',
  'comite_paritario',
  'programa_higiene_y_seguridad',
  'gestion_ambiental',
  'reglamento_interno',
  'politica_hseq',
  'licencias_y_permisos',
] as const;
export type PreguntaSst = (typeof PREGUNTAS_SST)[number];
export type DatosSst = Partial<Record<PreguntaSst, boolean | null>>;

export interface DatosEspecialidades {
  especialidades?: string[] | null;
}

/** Lo que cada formulario recibe: sus datos, cómo cambiarlos (por claves de primer nivel) y el mensaje de cada ruta. */
export interface PropsFormulario<T> {
  datos: T;
  onCambiar: (cambio: Partial<T>) => void;
  /** Error de formato (422) o faltante del servidor de la ruta (`personas[0].pep`); `null` si no hay. */
  mensaje: (ruta: string) => string | null;
  /** ¿Hay un error o faltante del servidor en alguna ruta que empiece por el prefijo (`personas[0].declaracion_pep`)? */
  hayEnRuta?: (prefijo: string) => boolean;
  deshabilitado: boolean;
}
