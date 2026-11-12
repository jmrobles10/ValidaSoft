import type { DeclaracionPep, FamiliarPep, PersonaRelacionada, RelacionEmpresa, RolPersona, SociedadPep } from '../tipos';

/** Máximo de familiares y de sociedades (el servidor rechaza más). */
export const MAXIMO_FILAS = 30;

/** Largos máximos (los del servidor). */
export const MAXIMO = { cargo: 200, institucion: 200, direccion: 500, nombre: 300, parentesco: 100, numero: 50, otra: 100, cargoInstitucion: 300 } as const;

/** Origen de la declaración: el propio tercero (persona natural, en la General) o una persona de la información legal. */
export type OrigenDeclaracion =
  | { tipo: 'natural'; nombres: string | null | undefined; apellidos: string | null | undefined; tipoIdentificacion: string | null | undefined; numero: string | null | undefined; direccion: string | null | undefined }
  | { tipo: 'persona'; persona: PersonaRelacionada };

/** Lo que la declaración muestra de solo lectura (sección 1) y lo que fija sola. */
export interface DatosBasicos {
  nombre: string;
  tipoIdentificacion: string | null;
  numero: string | null;
  relacion: RelacionEmpresa;
  relacionOtra: string | null;
  /** Natural: la dirección de la General (no se escribe en la declaración). `null`: la escribe quien declara. */
  direccionFija: string | null;
  /** Persona con nacionalidad ya registrada: no se vuelve a pedir. */
  nacionalidadFija: string | null;
  esNatural: boolean;
  /** Se firma siempre a nombre propio (persona natural). */
  soloNombrePropio: boolean;
}

/** Claves de lo que falta para poder declarar (se traducen en `ficha.pep.faltaBasico.*`). */
export type FaltaBasico = 'nombre' | 'direccion' | 'rol' | 'tipoIdentificacion' | 'numero';

const texto = (valor: string | null | undefined) => (valor ?? '').trim();

/** Relación con la empresa según el rol de la persona (como la fija el servidor). */
export function relacionDeRol(rol: RolPersona | null | undefined): { relacion: RelacionEmpresa; otra: string | null } | null {
  switch (rol) {
    case 'RepresentanteLegal':
    case 'Suplente':
    case 'Apoderado':
      return { relacion: 'Proveedor', otra: null };
    case 'Socio':
    case 'JuntaDirectiva':
      return { relacion: 'Socio', otra: null };
    case 'RevisorFiscal':
      return { relacion: 'Otro', otra: 'Revisor fiscal' };
    case 'Contador':
      return { relacion: 'Otro', otra: 'Contador' };
    default:
      return null;
  }
}

const nombreDe = (nombres: string | null | undefined, apellidos: string | null | undefined) => [texto(nombres), texto(apellidos)].filter(Boolean).join(' ');

/** Lo que falta de la persona o del tercero para poder declarar PEP (la sección 1 es de solo lectura y sale de ahí). */
export function faltasBasicas(origen: OrigenDeclaracion): FaltaBasico[] {
  if (origen.tipo === 'natural') {
    const faltas: FaltaBasico[] = [];
    if (!nombreDe(origen.nombres, origen.apellidos)) faltas.push('nombre');
    if (!texto(origen.direccion)) faltas.push('direccion');
    return faltas;
  }
  const p = origen.persona;
  const faltas: FaltaBasico[] = [];
  if (!p.rol) faltas.push('rol');
  if (!texto(p.tipo_identificacion)) faltas.push('tipoIdentificacion');
  if (!texto(p.numero)) faltas.push('numero');
  if (!nombreDe(p.nombres, p.tipo_identificacion === 'NIT' ? null : p.apellidos)) faltas.push('nombre');
  return faltas;
}

/** Datos básicos de la sección 1 (solo se llama cuando `faltasBasicas` está vacío). */
export function datosBasicos(origen: OrigenDeclaracion): DatosBasicos {
  if (origen.tipo === 'natural') {
    return {
      nombre: nombreDe(origen.nombres, origen.apellidos),
      tipoIdentificacion: texto(origen.tipoIdentificacion) || null,
      numero: texto(origen.numero) || null,
      relacion: 'Proveedor',
      relacionOtra: null,
      direccionFija: texto(origen.direccion) || null,
      nacionalidadFija: null,
      esNatural: true,
      soloNombrePropio: true,
    };
  }
  const p = origen.persona;
  const relacion = relacionDeRol(p.rol) ?? { relacion: 'Proveedor' as const, otra: null };
  return {
    nombre: nombreDe(p.nombres, p.tipo_identificacion === 'NIT' ? null : p.apellidos),
    tipoIdentificacion: texto(p.tipo_identificacion) || null,
    numero: texto(p.numero) || null,
    relacion: relacion.relacion,
    relacionOtra: relacion.otra,
    direccionFija: null,
    nacionalidadFija: texto(p.nacionalidad) || null,
    esNatural: false,
    soloNombrePropio: false,
  };
}

const vacio = (v: string | null | undefined) => texto(v) === '';

export const familiarVacio = (f: FamiliarPep) => vacio(f.nombre) && vacio(f.parentesco) && vacio(f.tipo_identificacion) && vacio(f.numero);
export const sociedadVacia = (s: SociedadPep) => vacio(s.nombre) && vacio(s.identificacion) && !s.calidad && vacio(s.calidad_otra);

/** Borrador nuevo con lo que el diálogo trae resuelto: relación, nacionalidad y firma en calidad. */
export function declaracionInicial(basicos: DatosBasicos, previa: DeclaracionPep | null | undefined): DeclaracionPep {
  return {
    ...previa,
    nacionalidad: basicos.nacionalidadFija ?? previa?.nacionalidad ?? null,
    firma_en_calidad: basicos.soloNombrePropio ? 'NombrePropio' : (previa?.firma_en_calidad ?? 'NombrePropio'),
    // El juramento se marca de nuevo en cada confirmación.
    acepta_juramento: false,
  };
}

/**
 * Lo que se entrega al formulario al confirmar: los datos básicos mostrados, las filas totalmente vacías descartadas, lo que
 * no aplica por una respuesta anterior descartado (desvinculación si está vigente, países sin cuentas, vínculo sin «sí») y
 * sin los sellos del servidor (huella, fecha e IP de la firma).
 */
export function declaracionParaEntregar(d: DeclaracionPep, basicos: DatosBasicos): DeclaracionPep {
  const { firma_huella: _huella, fecha_firma: _fecha, firma_ip: _ip, ...resto } = d;
  const familiares = (d.familiares ?? []).filter((f) => !familiarVacio(f));
  const sociedades = (d.sociedades ?? []).filter((s) => !sociedadVacia(s));
  const conyugeVacio = !d.conyuge || (vacio(d.conyuge.nombre) && vacio(d.conyuge.tipo_identificacion) && vacio(d.conyuge.numero));
  const vinculo = d.vinculo_con_pep;
  const representante = !basicos.soloNombrePropio && d.firma_en_calidad === 'RepresentanteLegal';
  return {
    ...resto,
    relacion_empresa: basicos.relacion,
    relacion_otra: basicos.relacionOtra,
    nombre: basicos.nombre,
    tipo_identificacion: basicos.tipoIdentificacion,
    numero: basicos.numero,
    direccion: basicos.direccionFija ?? d.direccion ?? null,
    nacionalidad: basicos.nacionalidadFija ?? d.nacionalidad ?? null,
    fecha_desvinculacion: d.vigente ? null : (d.fecha_desvinculacion ?? null),
    paises_cuentas_exterior: d.cuentas_exterior ? (d.paises_cuentas_exterior ?? []) : null,
    conyuge: conyugeVacio ? null : d.conyuge,
    familiares: familiares.length > 0 ? familiares : null,
    sociedades: sociedades.length > 0 ? sociedades.map((s) => (s.calidad === 'Otra' ? s : { ...s, calidad_otra: null })) : null,
    vinculo_con_pep: vinculo?.tiene
      ? { ...vinculo, fecha_desvinculacion: vinculo.vigente ? null : (vinculo.fecha_desvinculacion ?? null) }
      : { tiene: vinculo?.tiene ?? false },
    firma_en_calidad: basicos.soloNombrePropio ? 'NombrePropio' : (d.firma_en_calidad ?? 'NombrePropio'),
    firmante_nombre: representante ? d.firmante_nombre : null,
    firmante_tipo_identificacion: representante ? d.firmante_tipo_identificacion : null,
    firmante_numero: representante ? d.firmante_numero : null,
  };
}
