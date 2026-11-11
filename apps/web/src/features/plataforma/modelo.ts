import type {
  ActualizarPlanIaEmpresaDto,
  CrearTenantDto,
  EmpresaDeGrupoDto,
  PlanIaEmpresaDto,
  TipoIdentificacionDto,
} from '@validasoft/api-client';
import { cuerpoCrearEmpresa, validarEmpresa, type BorradorEmpresa, type ErrorEmpresa } from '../configuracion/empresas/modelo';
import { origenesDeTexto } from '../configuracion/sistemas/origenes';
import { aNumero, leerPrecio, precioDePlan } from './modeloConsumo';

/**
 * Consola de Plataforma (PT-5, PT-6): alta de un grupo con su primera empresa, su administrador y su sistema integrado, y
 * el plan de IA de cada empresa. Mismas reglas que el dominio (`Tenant.Crear`, `Empresa.Crear`, `SistemaIntegrado.Crear`,
 * `PlanDeIaEmpresa.Actualizar`) para avisar antes de enviar; la API decide.
 */

export const ZONA_POR_DEFECTO = 'America/Bogota';
/** Un grupo con más empresas que estas muestra su tabla plegada en la lista (PT-11). */
export const MAX_EMPRESAS_SIN_COLAPSAR = 5;
/** Alto aproximado (px) de la tarjeta de un grupo con una empresa, mientras la lista virtual no la mide. */
export const ALTO_ESTIMADO_GRUPO = 200;
/** Espera (ms) tras dejar de escribir en el buscador antes de pedir al servidor. */
export const ESPERA_BUSQUEDA_GRUPOS_MS = 300;
export const MAXIMO_VALIDACIONES = 1_000_000;
/** Duración de la sesión directa y cierre por inactividad con los que nace un grupo (el administrador del grupo los cambia en Configuración › Ingreso). */
export const SESION_HORAS_POR_DEFECTO = 8;
export const INACTIVIDAD_POR_DEFECTO = 30;

// ---------- Alta de grupo ----------

export interface BorradorGrupo {
  nombre: string;
  slug: string;
  /** El operador editó el slug: ya no se deriva del nombre. */
  slugManual: boolean;
  zonaHoraria: string;
  empresa: BorradorEmpresa;
  adminNombre: string;
  adminCorreo: string;
  /** Su usuario (`sub`) en el sistema integrado; vacío = se enlaza por correo en su primer acceso. */
  adminIdExterno: string;
  sistemaCodigo: string;
  sistemaNombre: string;
  sistemaClave: string;
  /** Uno por línea (o separados por coma). */
  sistemaOrigenes: string;
  /** Cómo entran las personas del grupo (ING-1): desde el sistema integrado (identidad delegada), directo a Validasoft, o ambos. */
  ingresoDelegado: boolean;
  ingresoDirecto: boolean;
}

export type ErrorGrupo =
  | 'grupo-nombre-requerido'
  | 'slug-invalido'
  | `empresa-${ErrorEmpresa}`
  | 'admin-nombre-requerido'
  | 'admin-correo-invalido'
  | 'modos-requeridos'
  | 'sistema-codigo-invalido'
  | 'sistema-nombre-requerido'
  | 'clave-publica-requerida'
  | 'origen-invalido';

export function borradorGrupoNuevo(empresa: BorradorEmpresa): BorradorGrupo {
  return {
    nombre: '',
    slug: '',
    slugManual: false,
    zonaHoraria: ZONA_POR_DEFECTO,
    empresa,
    adminNombre: '',
    adminCorreo: '',
    adminIdExterno: '',
    sistemaCodigo: '',
    sistemaNombre: '',
    sistemaClave: '',
    sistemaOrigenes: '',
    ingresoDelegado: true,
    ingresoDirecto: false,
  };
}

/** «Grupo Constructor Andino S.A.S.» → «grupo-constructor-andino-sas» (3 a 30 caracteres, como `Tenant.Crear`). */
export function slugDesdeNombre(nombre: string): string {
  return nombre
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 30)
    .replace(/-+$/g, '');
}

export function slugValido(slug: string): boolean {
  return /^[a-z0-9](?:[a-z0-9-]{1,28})[a-z0-9]$/.test(slug.trim());
}

export function codigoSistemaValido(codigo: string): boolean {
  return /^[A-Z][A-Z0-9_]{1,19}$/.test(codigo.trim().toUpperCase());
}

export function correoValido(correo: string): boolean {
  return /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(correo.trim());
}

export function clavePublicaPresente(pem: string): boolean {
  return /-----BEGIN (RSA )?PUBLIC KEY-----[\s\S]+-----END (RSA )?PUBLIC KEY-----/.test(pem.trim());
}

/** Errores en el orden del formulario: el primero es el que dice «falta…» junto al botón. */
export function validarGrupo(b: BorradorGrupo, tipo: TipoIdentificacionDto | undefined): ErrorGrupo[] {
  const errores: ErrorGrupo[] = [];
  if (!b.nombre.trim()) errores.push('grupo-nombre-requerido');
  if (!slugValido(b.slug)) errores.push('slug-invalido');
  errores.push(...validarEmpresa(b.empresa, tipo).map((e) => `empresa-${e}` as const));
  if (!b.adminNombre.trim()) errores.push('admin-nombre-requerido');
  if (!correoValido(b.adminCorreo)) errores.push('admin-correo-invalido');
  if (!b.ingresoDelegado && !b.ingresoDirecto) errores.push('modos-requeridos');
  // El sistema integrado solo hace falta si las personas entran desde él (identidad delegada).
  if (b.ingresoDelegado) {
    if (!codigoSistemaValido(b.sistemaCodigo)) errores.push('sistema-codigo-invalido');
    if (!b.sistemaNombre.trim()) errores.push('sistema-nombre-requerido');
    if (!clavePublicaPresente(b.sistemaClave)) errores.push('clave-publica-requerida');
    if (origenesDeTexto(b.sistemaOrigenes).error) errores.push('origen-invalido');
  }
  return errores;
}

export function cuerpoAltaGrupo(b: BorradorGrupo): CrearTenantDto {
  return {
    slug: b.slug.trim(),
    nombre: b.nombre.trim(),
    region: 'co',
    zonaHoraria: b.zonaHoraria.trim() || ZONA_POR_DEFECTO,
    empresa: { ...cuerpoCrearEmpresa(b.empresa), replyTo: null },
    administrador: { nombre: b.adminNombre.trim(), correo: b.adminCorreo.trim(), idExterno: b.ingresoDelegado ? b.adminIdExterno.trim() || null : null },
    sistema: b.ingresoDelegado
      ? {
          codigo: b.sistemaCodigo.trim().toUpperCase(),
          nombre: b.sistemaNombre.trim(),
          clavePublicaPem: b.sistemaClave.trim(),
          origenesPermitidos: origenesDeTexto(b.sistemaOrigenes).origenes,
        }
      : null,
    ingreso: {
      modos: [...(b.ingresoDelegado ? ['Delegado'] : []), ...(b.ingresoDirecto ? ['Directo'] : [])],
      sesionHoras: SESION_HORAS_POR_DEFECTO,
      inactividadMinutos: INACTIVIDAD_POR_DEFECTO,
      passkeyParaTodos: false,
    },
  };
}

// ---------- Plan de IA ----------

/** La llave es de solo escritura: se conserva, se reemplaza por una nueva o se quita. */
export type CambioDeLlave = { tipo: 'conservar' } | { tipo: 'reemplazar'; valor: string } | { tipo: 'quitar' };

export interface BorradorPlanIa {
  incluidas: string;
  adicional: boolean;
  /** Vacío = sin tope. */
  tope: string;
  /** Precio propio de cada validación adicional de esta empresa (PI-18); vacío = el de la tarifa general. */
  precio: string;
  modeloPorDefecto: string;
  modeloEscalamiento: string;
  umbral: string;
  activo: boolean;
  llave: CambioDeLlave;
}

export type ErrorPlanIa = 'incluidas-invalidas' | 'tope-invalido' | 'precio-invalido' | 'modelo-invalido' | 'umbral-invalido' | 'llave-invalida';

/** Un plan sin configurar se propone activo: quien lo abre es porque lo va a contratar. */
export function borradorDesdePlan(plan: PlanIaEmpresaDto): BorradorPlanIa {
  return {
    incluidas: String(plan.validacionesIncluidasMes),
    adicional: plan.adicionalHabilitado,
    tope: plan.topeAdicionalMes === null ? '' : String(plan.topeAdicionalMes),
    precio: plan.precioAdicional === null || plan.precioAdicional === undefined ? '' : String(aNumero(plan.precioAdicional)),
    modeloPorDefecto: plan.modeloPorDefecto,
    modeloEscalamiento: plan.modeloEscalamiento,
    umbral: String(plan.umbralConfianza),
    activo: plan.configurado ? plan.activo : true,
    llave: { tipo: 'conservar' },
  };
}

function entero(texto: string): number | null {
  const limpio = texto.trim();
  return /^\d+$/.test(limpio) ? Number(limpio) : null;
}

function decimal(texto: string): number | null {
  const limpio = texto.trim().replace(',', '.');
  return /^\d+(\.\d+)?$/.test(limpio) ? Number(limpio) : null;
}

export function modeloValido(modelo: string): boolean {
  return /^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$/.test(modelo.trim());
}

/** Como `PlanDeIaEmpresa.ValidarLlave`: 20 a 512 caracteres, sin espacios. */
export function llaveValida(llave: string): boolean {
  const limpia = llave.trim();
  return limpia.length >= 20 && limpia.length <= 512 && !/\s/.test(limpia);
}

export function validarPlanIa(b: BorradorPlanIa): ErrorPlanIa[] {
  const errores: ErrorPlanIa[] = [];
  const incluidas = entero(b.incluidas);
  if (incluidas === null || incluidas > MAXIMO_VALIDACIONES) errores.push('incluidas-invalidas');
  if (b.adicional && b.tope.trim()) {
    const tope = entero(b.tope);
    if (tope === null || tope > MAXIMO_VALIDACIONES) errores.push('tope-invalido');
  }
  if (b.adicional && b.precio.trim() && leerPrecio(b.precio) === null) errores.push('precio-invalido');
  if (!modeloValido(b.modeloPorDefecto) || !modeloValido(b.modeloEscalamiento)) errores.push('modelo-invalido');
  const umbral = decimal(b.umbral);
  if (umbral === null || umbral > 1) errores.push('umbral-invalido');
  if (b.llave.tipo === 'reemplazar' && !llaveValida(b.llave.valor)) errores.push('llave-invalida');
  return errores;
}

/** `apiKeyOpenAi`: nula conserva, vacía quita, con valor reemplaza (contrato de `PUT …/plan-ia`). */
export function cuerpoPlanIa(b: BorradorPlanIa): ActualizarPlanIaEmpresaDto {
  return {
    validacionesIncluidasMes: entero(b.incluidas) ?? 0,
    adicionalHabilitado: b.adicional,
    topeAdicionalMes: b.adicional && b.tope.trim() ? entero(b.tope) : null,
    // Nulo = sin precio propio: se cobra con la tarifa general.
    precioAdicional: precioDePlan(b.precio) ?? null,
    apiKeyOpenAi: b.llave.tipo === 'conservar' ? null : b.llave.tipo === 'quitar' ? '' : b.llave.valor.trim(),
    modeloPorDefecto: b.modeloPorDefecto.trim(),
    modeloEscalamiento: b.modeloEscalamiento.trim(),
    umbralConfianza: decimal(b.umbral) ?? 0,
    activo: b.activo,
  };
}

export type EstadoPlanIa = 'sin-plan' | 'inactivo' | 'sin-llave' | 'activo';

/** Cómo se ve el plan de una empresa en la lista de grupos. */
export function estadoPlanIa(e: Pick<EmpresaDeGrupoDto, 'planIaConfigurado' | 'planIaActivo' | 'llaveConfigurada'>): EstadoPlanIa {
  if (!e.planIaConfigurado) return 'sin-plan';
  if (!e.planIaActivo) return 'inactivo';
  return e.llaveConfigurada ? 'activo' : 'sin-llave';
}

/** «••••WXYZ»: lo único que se muestra de una llave. */
export function llaveEnmascarada(ultimos4: string | null | undefined): string {
  return `••••${ultimos4 ?? ''}`;
}
