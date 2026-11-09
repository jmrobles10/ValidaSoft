import type { AtributoPermitidoDto, CrearTipoVinculoDto, TipoVinculoDto } from '@validasoft/api-client';

/**
 * Tipos de vínculo del grupo y sus atributos permitidos. Los atributos alimentan las condiciones «Se pide a» de los
 * flujos (RQ-12: `atributos.<clave>`), así que aquí se valida lo mismo que `TipoVinculo.Crear` en el dominio —código,
 * nombre, claves sin repetir, opciones para Opcion/ListaOpciones— para avisar antes de guardar. La API decide (422
 * `codigo-invalido`, `atributo-duplicado`, `opciones-requeridas`, `codigo-inmutable`; 409 `tipo-vinculo-duplicado`).
 */
export const TIPOS_ATRIBUTO = ['Texto', 'Numero', 'Fecha', 'Booleano', 'Opcion', 'ListaOpciones'] as const;
export type TipoAtributo = (typeof TIPOS_ATRIBUTO)[number];

export const esTipoAtributo = (v: string): v is TipoAtributo => TIPOS_ATRIBUTO.some((t) => t === v);
export const conOpciones = (tipo: TipoAtributo): boolean => tipo === 'Opcion' || tipo === 'ListaOpciones';

export interface AtributoBorrador {
  clave: string;
  tipo: TipoAtributo;
  obligatorio: boolean;
  /** Una opción por línea. */
  opciones: string;
}

export interface BorradorTipoVinculo {
  codigo: string;
  nombre: string;
  exigeCuentaDesignada: boolean;
  exigeOtpEnEnlace: boolean;
  atributos: AtributoBorrador[];
}

export type CodigoErrorTipo = 'codigo-invalido' | 'nombre-requerido' | 'clave-requerida' | 'atributo-duplicado' | 'opciones-requeridas';

export type ErrorTipoVinculo = { campo: 'codigo' | 'nombre'; codigo: CodigoErrorTipo } | { campo: 'atributo'; indice: number; codigo: CodigoErrorTipo };

export function borradorTipoNuevo(): BorradorTipoVinculo {
  return { codigo: '', nombre: '', exigeCuentaDesignada: false, exigeOtpEnEnlace: false, atributos: [] };
}

export function atributoNuevo(): AtributoBorrador {
  return { clave: '', tipo: 'Texto', obligatorio: false, opciones: '' };
}

export function borradorDeTipo(tipo: TipoVinculoDto): BorradorTipoVinculo {
  return {
    codigo: tipo.codigo,
    nombre: tipo.nombre,
    exigeCuentaDesignada: tipo.exigeCuentaDesignada,
    exigeOtpEnEnlace: tipo.exigeOtpEnEnlace ?? false,
    atributos: tipo.atributosPermitidos.map((a) => ({
      clave: a.clave,
      tipo: esTipoAtributo(a.tipo) ? a.tipo : 'Texto',
      obligatorio: a.obligatorio,
      opciones: (a.opciones ?? []).join('\n'),
    })),
  };
}

/** Como el dominio: sin espacios a los lados y en mayúsculas. */
export function normalizarCodigo(codigo: string): string {
  return codigo.trim().toUpperCase();
}

export function codigoValido(codigo: string): boolean {
  return /^[A-Z][A-Z0-9_]{1,39}$/.test(normalizarCodigo(codigo));
}

/** Opciones escritas una por línea: sin vacías ni repetidas (las condiciones las comparan tal cual). */
export function opcionesDeTexto(texto: string): string[] {
  return [...new Set(texto.split('\n').map((o) => o.trim()).filter(Boolean))];
}

export function validarTipoVinculo(b: BorradorTipoVinculo): ErrorTipoVinculo[] {
  const errores: ErrorTipoVinculo[] = [];
  if (!codigoValido(b.codigo)) errores.push({ campo: 'codigo', codigo: 'codigo-invalido' });
  if (!b.nombre.trim()) errores.push({ campo: 'nombre', codigo: 'nombre-requerido' });
  const vistas = new Set<string>();
  b.atributos.forEach((a, indice) => {
    const clave = a.clave.trim();
    if (!clave) errores.push({ campo: 'atributo', indice, codigo: 'clave-requerida' });
    else if (vistas.has(clave)) errores.push({ campo: 'atributo', indice, codigo: 'atributo-duplicado' });
    vistas.add(clave);
    if (conOpciones(a.tipo) && opcionesDeTexto(a.opciones).length === 0) errores.push({ campo: 'atributo', indice, codigo: 'opciones-requeridas' });
  });
  return errores;
}

export function cuerpoTipoVinculo(b: BorradorTipoVinculo): CrearTipoVinculoDto {
  const atributos: AtributoPermitidoDto[] = b.atributos.map((a) => ({
    clave: a.clave.trim(),
    tipo: a.tipo,
    obligatorio: a.obligatorio,
    opciones: conOpciones(a.tipo) ? opcionesDeTexto(a.opciones) : null,
  }));
  return {
    codigo: normalizarCodigo(b.codigo),
    nombre: b.nombre.trim(),
    atributosPermitidos: atributos,
    exigeCuentaDesignada: b.exigeCuentaDesignada,
    exigeOtpEnEnlace: b.exigeOtpEnEnlace,
  };
}

/** Lo que un tipo guardado tiene en el formulario de edición, para activarlo o desactivarlo sin cambiar nada más. */
export function cuerpoDeTipo(tipo: TipoVinculoDto): CrearTipoVinculoDto {
  return cuerpoTipoVinculo(borradorDeTipo(tipo));
}
