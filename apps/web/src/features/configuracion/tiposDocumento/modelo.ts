import type {
  ActualizarTipoDocumentoDto,
  CampoFormularioDto,
  CrearTipoDocumentoDto,
  TipoDocumentoCanonicoDto,
  TipoDocumentoTenantDto,
} from '@validasoft/api-client';

/**
 * Tipos de documento configurables (docs/05 §2.9.4, TD-1…TD-9): reglas de archivo, modo y campos, vigencia y plantilla.
 * Aquí vive el borrador del formulario y su validación, igual que la API, para avisar antes de guardar; la API decide
 * (422 `formato-mas-laxo`, `limite-mas-laxo`, `limite-sobre-el-global`, `campo-duplicado`, `campo-reservado`…).
 */

/** Formatos que admite la plataforma, por su nombre corto (la API los devuelve como MIME). */
export const FORMATOS = [
  { corto: 'pdf', mime: 'application/pdf', nombre: 'PDF' },
  { corto: 'jpg', mime: 'image/jpeg', nombre: 'JPG' },
  { corto: 'png', mime: 'image/png', nombre: 'PNG' },
  { corto: 'webp', mime: 'image/webp', nombre: 'WebP' },
] as const;
export type FormatoCorto = (typeof FORMATOS)[number]['corto'];

export const MODOS = ['Archivo', 'Formulario', 'ArchivoYFormulario'] as const;
export type Modo = (typeof MODOS)[number];
export const AMBITOS = ['Global', 'PorVinculo'] as const;
export const TIPOS_CAMPO = ['Texto', 'Numero', 'Fecha', 'Lista', 'SiNo'] as const;
export type TipoCampo = (typeof TIPOS_CAMPO)[number];
export const REGLAS_FRESCURA = ['Ninguna', 'MaxDiasDesdeExpedicion', 'MaxDiasDesdeAprobacion', 'PeriodoMinimo'] as const;
export type ReglaFrescura = (typeof REGLAS_FRESCURA)[number];
/** Claves con significado propio: la fecha de expedición alimenta la vigencia y el vencimiento propio la acota. */
export const CLAVES_RESERVADAS = ['fecha_expedicion', 'fecha_vencimiento'] as const;
export const MAXIMO_CAMPOS = 50;

export interface CampoBorrador {
  clave: string;
  etiqueta: string;
  tipo: TipoCampo;
  obligatorio: boolean;
  /** Opciones de una lista, una por línea. */
  opciones: string;
  ayuda: string;
}

export interface BorradorTipo {
  codigo: string;
  nombre: string;
  tipoCanonico: string;
  ambito: string;
  textoAyuda: string;
  activo: boolean;
  formatos: FormatoCorto[];
  tamanoMb: string;
  paginas: string;
  modo: Modo;
  campos: CampoBorrador[];
  frescura: ReglaFrescura;
  diasFrescura: string;
  periodo: string;
  diasAviso: string;
  diasGracia: string;
}

export type CodigoError =
  | 'requerido'
  | 'codigo-invalido'
  | 'formulario-solo-por-vinculo'
  | 'limite-invalido'
  | 'campos-requeridos'
  | 'campos-excesivos'
  | 'etiqueta-requerida'
  | 'campo-invalido'
  | 'campo-duplicado'
  | 'campo-reservado'
  | 'opciones-requeridas'
  | 'dias-invalidos';

export type CampoDelBorrador = 'codigo' | 'nombre' | 'tamanoMb' | 'paginas' | 'diasFrescura' | 'periodo' | 'diasAviso' | 'diasGracia' | 'campos' | 'ambito';

export type ErrorBorrador = { campo: CampoDelBorrador; codigo: CodigoError } | { campo: 'campo'; indice: number; codigo: CodigoError };

const esModo = (v: string | undefined): v is Modo => MODOS.some((m) => m === v);
const esTipoCampo = (v: string): v is TipoCampo => TIPOS_CAMPO.some((t) => t === v);
const esFrescura = (v: string): v is ReglaFrescura => REGLAS_FRESCURA.some((r) => r === v);

export function formatoCortoDe(mime: string): FormatoCorto | null {
  return FORMATOS.find((f) => f.mime === mime || f.corto === mime)?.corto ?? null;
}

export function nombreDeFormato(mime: string): string {
  return FORMATOS.find((f) => f.mime === mime || f.corto === mime)?.nombre ?? mime;
}

export function borradorNuevo(): BorradorTipo {
  return {
    codigo: '',
    nombre: '',
    tipoCanonico: '',
    ambito: 'PorVinculo',
    textoAyuda: '',
    activo: true,
    formatos: [],
    tamanoMb: '',
    paginas: '',
    modo: 'Archivo',
    campos: [],
    frescura: 'Ninguna',
    diasFrescura: '',
    periodo: '',
    diasAviso: '30, 15, 5',
    diasGracia: '15',
  };
}

export function borradorDe(tipo: TipoDocumentoTenantDto): BorradorTipo {
  const frescura = esFrescura(tipo.reglaFrescura.tipo) ? tipo.reglaFrescura.tipo : 'Ninguna';
  return {
    codigo: tipo.codigo,
    nombre: tipo.nombre,
    tipoCanonico: tipo.tipoCanonico ?? '',
    ambito: tipo.ambito,
    textoAyuda: tipo.textoAyuda ?? '',
    activo: tipo.activo,
    formatos: (tipo.formatosPermitidos ?? []).flatMap((m) => formatoCortoDe(m) ?? []),
    tamanoMb: tipo.tamanoMaximoMb ? String(tipo.tamanoMaximoMb) : '',
    paginas: tipo.paginasMaximas ? String(tipo.paginasMaximas) : '',
    modo: esModo(tipo.modo) ? tipo.modo : 'Archivo',
    campos: (tipo.campos ?? []).map((c) => ({
      clave: c.clave,
      etiqueta: c.etiqueta,
      tipo: esTipoCampo(c.tipo) ? c.tipo : 'Texto',
      obligatorio: c.obligatorio ?? false,
      opciones: (c.opciones ?? []).join('\n'),
      ayuda: c.ayuda ?? '',
    })),
    frescura,
    diasFrescura: tipo.reglaFrescura.dias ? String(tipo.reglaFrescura.dias) : '',
    periodo: tipo.reglaFrescura.regla ?? '',
    diasAviso: tipo.diasAviso.join(', '),
    diasGracia: String(tipo.diasGracia),
  };
}

export function campoNuevo(): CampoBorrador {
  return { clave: '', etiqueta: '', tipo: 'Texto', obligatorio: false, opciones: '', ayuda: '' };
}

/** «Fecha de expedición» → «fecha_de_expedicion»: la clave sugerida a partir de la etiqueta. */
export function claveDesdeEtiqueta(etiqueta: string): string {
  return etiqueta
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .replace(/^(\d)/, 'c_$1')
    .slice(0, 60);
}

const entero = (texto: string) => (/^\d+$/.test(texto.trim()) ? Number(texto.trim()) : null);
const lista = (texto: string) => texto.split(/[\s,;]+/).filter(Boolean);
export const opcionesDe = (texto: string) => texto.split('\n').map((o) => o.trim()).filter(Boolean);
export const conCampos = (modo: Modo) => modo !== 'Archivo';

/** Errores del borrador, en el orden del formulario (el primero es el que se anuncia junto al botón de guardar). */
export function validarBorrador(b: BorradorTipo, creando: boolean): ErrorBorrador[] {
  const errores: ErrorBorrador[] = [];
  if (creando && !/^[A-Z][A-Z0-9_]{1,49}$/.test(b.codigo.trim())) errores.push({ campo: 'codigo', codigo: b.codigo.trim() ? 'codigo-invalido' : 'requerido' });
  if (!b.nombre.trim()) errores.push({ campo: 'nombre', codigo: 'requerido' });
  if (conCampos(b.modo) && b.ambito !== 'PorVinculo') errores.push({ campo: 'ambito', codigo: 'formulario-solo-por-vinculo' });
  if (b.tamanoMb.trim() && !(entero(b.tamanoMb) !== null && Number(b.tamanoMb) >= 1 && Number(b.tamanoMb) <= 1024)) errores.push({ campo: 'tamanoMb', codigo: 'limite-invalido' });
  if (b.paginas.trim() && !(entero(b.paginas) !== null && Number(b.paginas) >= 1 && Number(b.paginas) <= 1000)) errores.push({ campo: 'paginas', codigo: 'limite-invalido' });

  if (conCampos(b.modo)) {
    if (b.campos.length === 0) errores.push({ campo: 'campos', codigo: 'campos-requeridos' });
    if (b.campos.length > MAXIMO_CAMPOS) errores.push({ campo: 'campos', codigo: 'campos-excesivos' });
    const vistas = new Set<string>();
    b.campos.forEach((c, indice) => {
      const clave = c.clave.trim();
      if (!c.etiqueta.trim()) errores.push({ campo: 'campo', indice, codigo: 'etiqueta-requerida' });
      if (!/^[a-z][a-z0-9_]{0,59}$/.test(clave)) errores.push({ campo: 'campo', indice, codigo: 'campo-invalido' });
      else if (vistas.has(clave)) errores.push({ campo: 'campo', indice, codigo: 'campo-duplicado' });
      else if (CLAVES_RESERVADAS.some((r) => r === clave) && c.tipo !== 'Fecha') errores.push({ campo: 'campo', indice, codigo: 'campo-reservado' });
      if (c.tipo === 'Lista' && opcionesDe(c.opciones).length === 0) errores.push({ campo: 'campo', indice, codigo: 'opciones-requeridas' });
      vistas.add(clave);
    });
  }

  if ((b.frescura === 'MaxDiasDesdeExpedicion' || b.frescura === 'MaxDiasDesdeAprobacion') && !(entero(b.diasFrescura) !== null && Number(b.diasFrescura) >= 1))
    errores.push({ campo: 'diasFrescura', codigo: 'dias-invalidos' });
  if (b.frescura === 'PeriodoMinimo' && !b.periodo.trim()) errores.push({ campo: 'periodo', codigo: 'requerido' });
  if (lista(b.diasAviso).some((d) => entero(d) === null || Number(d) < 1 || Number(d) > 365)) errores.push({ campo: 'diasAviso', codigo: 'dias-invalidos' });
  if (b.diasGracia.trim() && !(entero(b.diasGracia) !== null && Number(b.diasGracia) <= 365)) errores.push({ campo: 'diasGracia', codigo: 'dias-invalidos' });
  return errores;
}

function camposDto(b: BorradorTipo): CampoFormularioDto[] {
  return b.campos.map((c) => ({
    clave: c.clave.trim(),
    etiqueta: c.etiqueta.trim(),
    tipo: c.tipo,
    obligatorio: c.obligatorio,
    opciones: c.tipo === 'Lista' ? opcionesDe(c.opciones) : null,
    ayuda: c.ayuda.trim() || null,
  }));
}

function comunes(b: BorradorTipo) {
  const dias = entero(b.diasFrescura);
  return {
    nombre: b.nombre.trim(),
    ambito: b.ambito,
    textoAyuda: b.textoAyuda.trim() || null,
    formatosPermitidos: b.formatos,
    modo: b.modo,
    reglaFrescura: {
      tipo: b.frescura,
      dias: b.frescura === 'MaxDiasDesdeExpedicion' || b.frescura === 'MaxDiasDesdeAprobacion' ? dias : null,
      regla: b.frescura === 'PeriodoMinimo' ? b.periodo.trim() : null,
    },
    diasAviso: lista(b.diasAviso).map(Number),
    diasGracia: entero(b.diasGracia) ?? 0,
  };
}

export function cuerpoCrear(b: BorradorTipo): CrearTipoDocumentoDto {
  return {
    ...comunes(b),
    codigo: b.codigo.trim(),
    tipoCanonico: b.tipoCanonico || null,
    tamanoMaximoMb: entero(b.tamanoMb),
    paginasMaximas: entero(b.paginas),
    campos: conCampos(b.modo) ? camposDto(b) : null,
  };
}

/** En un PUT, `0` vuelve al límite global y `[]` quita los campos (solo con modo Archivo). */
export function cuerpoActualizar(b: BorradorTipo): ActualizarTipoDocumentoDto {
  return {
    ...comunes(b),
    activo: b.activo,
    tamanoMaximoMb: entero(b.tamanoMb) ?? 0,
    paginasMaximas: entero(b.paginas) ?? 0,
    campos: conCampos(b.modo) ? camposDto(b) : [],
  };
}

/** Techo que impone el canónico (los tipos de la plataforma solo admiten reglas más estrictas). */
export function techoDelCanonico(canonico: TipoDocumentoCanonicoDto | undefined) {
  if (!canonico) return null;
  const formatos = (canonico.formatosPermitidos ?? []).flatMap((m) => formatoCortoDe(m) ?? []);
  return { formatos: formatos.length > 0 ? formatos : null, tamanoMb: canonico.tamanoMaximoMb ?? null, paginas: canonico.paginasMaximas ?? null };
}

/** Peso legible: «5 MB», «1.5 MB»; por debajo de 1 MB, en KB («12 KB»). */
export function megas(bytes: number): string {
  if (bytes < 1024 * 1024) return `${String(Math.max(1, Math.round(bytes / 1024)))} KB`;
  const mb = bytes / (1024 * 1024);
  return `${mb >= 10 || Number.isInteger(mb) ? String(Math.round(mb)) : mb.toFixed(1)} MB`;
}
