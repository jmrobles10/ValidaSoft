import { RELACIONES_PEP, TIPOS_IDENTIFICACION_PERSONA, type DeclaracionPep, type FamiliarPep, type SociedadPep, type VinculoConPep } from '../tipos';
import { familiarVacio, MAXIMO, MAXIMO_FILAS, pepRelacionadaVacia, sociedadVacia, type DatosBasicos } from './declaracion';

/** Qué falla de un campo; se traduce en `ficha.pep.errores.<código>`. */
export type CodigoError =
  | 'requerido'
  | 'fechaFutura'
  | 'finAnterior'
  | 'paisesRequeridos'
  | 'filaIncompleta'
  | 'conyugeIncompleto'
  | 'maxFilas'
  | 'juramento'
  | 'firma'
  | 'tipoDocumento'
  | 'largo'
  | 'relacionInvalida';

/** Errores por ruta de la declaración (las del contrato: `cargo`, `familiares[0].parentesco`, `vinculo_con_pep.nombre`…). */
export type ErroresDeclaracion = Record<string, CodigoError>;

const texto = (valor: string | null | undefined) => (valor ?? '').trim();
const vacio = (valor: string | null | undefined) => texto(valor) === '';

/** Fecha de hoy `aaaa-mm-dd` en la zona del negocio (la misma con la que el servidor dice «no futura»). */
export function hoyIso(ahora = new Date()): string {
  // `en-CA` ya escribe el día como aaaa-mm-dd.
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota', year: 'numeric', month: '2-digit', day: '2-digit' }).format(ahora);
}

/** Vinculación obligatoria y no futura; desvinculación (≥ vinculación y no futura) o «vigente». */
function validarPeriodo(
  errores: ErroresDeclaracion,
  prefijo: string,
  p: { fecha_vinculacion?: string | null; fecha_desvinculacion?: string | null; vigente?: boolean | null },
  hoy: string,
): void {
  const ruta = (campo: string) => (prefijo ? `${prefijo}.${campo}` : campo);
  if (vacio(p.fecha_vinculacion)) errores[ruta('fecha_vinculacion')] = 'requerido';
  else if ((p.fecha_vinculacion ?? '') > hoy) errores[ruta('fecha_vinculacion')] = 'fechaFutura';
  if (p.vigente) return;
  if (vacio(p.fecha_desvinculacion)) errores[ruta('fecha_desvinculacion')] = 'requerido';
  else if ((p.fecha_desvinculacion ?? '') > hoy) errores[ruta('fecha_desvinculacion')] = 'fechaFutura';
  else if (!vacio(p.fecha_vinculacion) && (p.fecha_desvinculacion ?? '') < (p.fecha_vinculacion ?? '')) errores[ruta('fecha_desvinculacion')] = 'finAnterior';
}

const esTipoDocumento = (valor: string | null | undefined) => TIPOS_IDENTIFICACION_PERSONA.some((t) => t === valor);

function requerido(errores: ErroresDeclaracion, ruta: string, valor: string | null | undefined, maximo?: number): void {
  if (vacio(valor)) errores[ruta] = 'requerido';
  else if (maximo !== undefined && texto(valor).length > maximo) errores[ruta] = 'largo';
}

function validarConyuge(errores: ErroresDeclaracion, d: DeclaracionPep): void {
  const c = d.conyuge;
  if (!c) return;
  const llenos = [c.nombre, c.tipo_identificacion, c.numero].filter((v) => !vacio(v)).length;
  if (llenos === 0) return;
  if (llenos < 3) {
    if (vacio(c.nombre)) errores['conyuge.nombre'] = 'conyugeIncompleto';
    if (vacio(c.tipo_identificacion)) errores['conyuge.tipo_identificacion'] = 'conyugeIncompleto';
    if (vacio(c.numero)) errores['conyuge.numero'] = 'conyugeIncompleto';
  }
  if (!vacio(c.nombre) && texto(c.nombre).length > MAXIMO.nombre) errores['conyuge.nombre'] = 'largo';
  if (!vacio(c.numero) && texto(c.numero).length > MAXIMO.numero) errores['conyuge.numero'] = 'largo';
}

/** Cada familiar es todo o nada: una fila con algo escrito debe estar completa; la vacía se descarta. */
function validarFamiliares(errores: ErroresDeclaracion, filas: readonly FamiliarPep[]): void {
  if (filas.filter((f) => !familiarVacio(f)).length > MAXIMO_FILAS) errores.familiares = 'maxFilas';
  filas.forEach((f, i) => {
    if (familiarVacio(f)) return;
    const ruta = (campo: string) => `familiares[${String(i)}].${campo}`;
    if (vacio(f.nombre)) errores[ruta('nombre')] = 'filaIncompleta';
    else if (texto(f.nombre).length > MAXIMO.nombre) errores[ruta('nombre')] = 'largo';
    if (vacio(f.parentesco)) errores[ruta('parentesco')] = 'filaIncompleta';
    else if (texto(f.parentesco).length > MAXIMO.parentesco) errores[ruta('parentesco')] = 'largo';
    if (vacio(f.tipo_identificacion)) errores[ruta('tipo_identificacion')] = 'filaIncompleta';
    else if (!esTipoDocumento(f.tipo_identificacion)) errores[ruta('tipo_identificacion')] = 'tipoDocumento';
    if (vacio(f.numero)) errores[ruta('numero')] = 'filaIncompleta';
    else if (texto(f.numero).length > MAXIMO.numero) errores[ruta('numero')] = 'largo';
  });
}

function validarSociedades(errores: ErroresDeclaracion, filas: readonly SociedadPep[]): void {
  if (filas.filter((s) => !sociedadVacia(s)).length > MAXIMO_FILAS) errores.sociedades = 'maxFilas';
  filas.forEach((s, i) => {
    if (sociedadVacia(s)) return;
    const ruta = (campo: string) => `sociedades[${String(i)}].${campo}`;
    if (vacio(s.nombre)) errores[ruta('nombre')] = 'filaIncompleta';
    else if (texto(s.nombre).length > MAXIMO.nombre) errores[ruta('nombre')] = 'largo';
    if (vacio(s.identificacion)) errores[ruta('identificacion')] = 'filaIncompleta';
    else if (texto(s.identificacion).length > MAXIMO.numero) errores[ruta('identificacion')] = 'largo';
    if (!s.calidad) errores[ruta('calidad')] = 'filaIncompleta';
    if (s.calidad === 'Otra') {
      if (vacio(s.calidad_otra)) errores[ruta('calidad_otra')] = 'filaIncompleta';
      else if (texto(s.calidad_otra).length > MAXIMO.otra) errores[ruta('calidad_otra')] = 'largo';
    }
  });
}

function validarVinculo(errores: ErroresDeclaracion, v: VinculoConPep | null | undefined, hoy: string): void {
  if (v?.tiene === null || v?.tiene === undefined) {
    errores['vinculo_con_pep.tiene'] = 'requerido';
    return;
  }
  if (!v.tiene) return;
  const filas = (v.vinculos ?? []).filter((p) => !pepRelacionadaVacia(p));
  if (filas.length === 0) errores['vinculo_con_pep.vinculos'] = 'requerido';
  else if (filas.length > MAXIMO_FILAS) errores['vinculo_con_pep.vinculos'] = 'maxFilas';
  filas.forEach((p, i) => {
    const ruta = (campo: string) => `vinculo_con_pep.vinculos[${String(i)}].${campo}`;
    if (!p.tipo) errores[ruta('tipo')] = 'requerido';
    if (vacio(p.relacion) || !p.tipo) errores[ruta('relacion')] = 'requerido';
    else if (!RELACIONES_PEP[p.tipo].includes(texto(p.relacion))) errores[ruta('relacion')] = 'relacionInvalida';
    requerido(errores, ruta('nombre'), p.nombre, MAXIMO.nombre);
    requerido(errores, ruta('cargo'), p.cargo, MAXIMO.cargoPep);
    requerido(errores, ruta('entidad'), p.entidad, MAXIMO.entidad);
    requerido(errores, ruta('pais'), p.pais);
    validarPeriodo(errores, `vinculo_con_pep.vinculos[${String(i)}]`, p, hoy);
  });
}

/**
 * Las reglas del servidor sobre la declaración, para avisar en el diálogo antes de confirmar (el servidor las vuelve a aplicar
 * al guardar). Función pura: devuelve los errores por ruta y ninguno si la declaración está lista para confirmar.
 */
export function validarDeclaracionPep(d: DeclaracionPep, basicos: DatosBasicos, hoy: string = hoyIso()): ErroresDeclaracion {
  const errores: ErroresDeclaracion = {};

  if (!basicos.nacionalidadFija) requerido(errores, 'nacionalidad', d.nacionalidad);
  if (!basicos.direccionFija) requerido(errores, 'direccion', d.direccion, MAXIMO.direccion);

  requerido(errores, 'cargo', d.cargo, MAXIMO.cargo);
  requerido(errores, 'institucion', d.institucion, MAXIMO.institucion);
  requerido(errores, 'pais', d.pais);
  validarPeriodo(errores, '', d, hoy);

  if (d.cuentas_exterior === null || d.cuentas_exterior === undefined) errores.cuentas_exterior = 'requerido';
  else if (d.cuentas_exterior && (d.paises_cuentas_exterior ?? []).length === 0) errores.paises_cuentas_exterior = 'paisesRequeridos';
  else if (d.cuentas_exterior && (d.paises_cuentas_exterior ?? []).length > MAXIMO_FILAS) errores.paises_cuentas_exterior = 'maxFilas';

  validarConyuge(errores, d);
  validarFamiliares(errores, d.familiares ?? []);
  validarSociedades(errores, d.sociedades ?? []);
  validarVinculo(errores, d.vinculo_con_pep, hoy);

  if ((!basicos.soloNombrePropio && d.firma_en_calidad === 'RepresentanteLegal') || d.firma_en_calidad === 'Apoderado') {
    requerido(errores, 'firmante_nombre', d.firmante_nombre, MAXIMO.nombre);
    if (vacio(d.firmante_tipo_identificacion)) errores.firmante_tipo_identificacion = 'requerido';
    else if (!esTipoDocumento(d.firmante_tipo_identificacion)) errores.firmante_tipo_identificacion = 'tipoDocumento';
    requerido(errores, 'firmante_numero', d.firmante_numero, MAXIMO.numero);
  }

  if (!d.acepta_juramento) errores.acepta_juramento = 'juramento';
  if (vacio(d.firma_png)) errores.firma_png = 'firma';
  return errores;
}

/** Hay algún error. */
export const hayErrores = (errores: ErroresDeclaracion) => Object.keys(errores).length > 0;
