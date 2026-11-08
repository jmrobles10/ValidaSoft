import { esErrorApi, type CampoFormularioDto, type DatoDeclaradoDto, type EnviarFormularioDto, type TipoDocumentoRequisitoDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import type { DestinoCarga } from '../boveda/subida';

/** Tipos de campo que configura la empresa (TD-4). Uno desconocido se pide como texto. */
export type TipoCampo = 'Texto' | 'Numero' | 'Fecha' | 'Lista' | 'SiNo';

/** Lo que escribe el tercero, siempre como texto: `SiNo` es `'true'`, `'false'` o vacío. */
export type ValoresFormulario = Readonly<Record<string, string>>;

/** Por qué un campo aún no se puede enviar. Las fechas reservadas siguen las reglas de la API (TD-6). */
export type ErrorCampo = 'requerido' | 'numero' | 'fecha' | 'opcion' | 'largo' | 'expedicion-futura' | 'vencimiento-antes';

/** Máximo de un campo de texto (`EsquemaDeFormulario.MaximoTexto`). */
export const MAXIMO_TEXTO = 2000;
const FECHA_EXPEDICION = 'fecha_expedicion';
const FECHA_VENCIMIENTO = 'fecha_vencimiento';
const NUMERO = /^-?\d+([.,]\d+)?$/;
const FECHA = /^\d{4}-\d{2}-\d{2}$/;

export function tipoDeCampo(campo: Pick<CampoFormularioDto, 'tipo'>): TipoCampo {
  switch (campo.tipo) {
    case 'Numero':
    case 'Fecha':
    case 'Lista':
    case 'SiNo':
      return campo.tipo;
    default:
      return 'Texto';
  }
}

export function camposDe(tipo: Pick<TipoDocumentoRequisitoDto, 'campos'> | null | undefined): CampoFormularioDto[] {
  return tipo?.campos ?? [];
}

/**
 * Valores de partida al actualizar: lo que el tercero ya declaró en la versión vigente (la API los devuelve normalizados:
 * número invariante, fecha ISO, `true`/`false`), solo de los campos que el tipo sigue pidiendo.
 */
export function valoresIniciales(campos: readonly CampoFormularioDto[], declarados: readonly Pick<DatoDeclaradoDto, 'clave' | 'valor'>[] | null | undefined): Record<string, string> {
  const valores: Record<string, string> = {};
  for (const dato of declarados ?? []) {
    if (campos.some((c) => c.clave === dato.clave)) valores[dato.clave] = dato.valor;
  }
  return valores;
}

/** `aaaa-mm-dd` que existe en el calendario (no 2026-02-30). */
function esFechaValida(texto: string): boolean {
  if (!FECHA.test(texto)) return false;
  const fecha = new Date(`${texto}T00:00:00Z`);
  return !Number.isNaN(fecha.getTime()) && fecha.toISOString().startsWith(texto);
}

/** Error de un campo con lo que lleva escrito; `hoy` en `aaaa-mm-dd` para la fecha de expedición. */
export function errorDeCampo(campo: CampoFormularioDto, valores: ValoresFormulario, hoy: string): ErrorCampo | null {
  const valor = (valores[campo.clave] ?? '').trim();
  if (!valor) return campo.obligatorio ? 'requerido' : null;
  switch (tipoDeCampo(campo)) {
    case 'Texto':
      return valor.length > MAXIMO_TEXTO ? 'largo' : null;
    case 'Numero':
      return NUMERO.test(valor) ? null : 'numero';
    case 'Lista':
      return (campo.opciones ?? []).includes(valor) ? null : 'opcion';
    case 'SiNo':
      return valor === 'true' || valor === 'false' ? null : 'opcion';
    case 'Fecha': {
      if (!esFechaValida(valor)) return 'fecha';
      if (campo.clave === FECHA_EXPEDICION && valor > hoy) return 'expedicion-futura';
      const expedicion = (valores[FECHA_EXPEDICION] ?? '').trim();
      if (campo.clave === FECHA_VENCIMIENTO && esFechaValida(expedicion) && valor < expedicion) return 'vencimiento-antes';
      return null;
    }
  }
}

/** Errores de todos los campos (solo los que tienen error). */
export function erroresDelFormulario(campos: readonly CampoFormularioDto[], valores: ValoresFormulario, hoy: string): Record<string, ErrorCampo> {
  const errores: Record<string, ErrorCampo> = {};
  for (const campo of campos) {
    const error = errorDeCampo(campo, valores, hoy);
    if (error) errores[campo.clave] = error;
  }
  return errores;
}

/**
 * Qué falta para enviar, junto al botón deshabilitado: primero lo que falta llenar y, si todo está lleno, lo que hay que
 * corregir. `null` si se puede enviar. `archivo`: el texto habla de subir el archivo (tipo `ArchivoYFormulario`).
 */
export function faltaParaEnviar(campos: readonly CampoFormularioDto[], errores: Readonly<Record<string, ErrorCampo>>, t: TFunction, archivo = false): string | null {
  const con = (filtro: (e: ErrorCampo) => boolean) =>
    campos
      .filter((c) => {
        const error = errores[c.clave];
        return error !== undefined && filtro(error);
      })
      .map((c) => c.etiqueta);
  const vacios = con((e) => e === 'requerido');
  if (vacios.length > 0) return t(archivo ? 'formulario.faltaLlenarArchivo' : 'formulario.faltaLlenar', { campos: vacios.join(', ') });
  const invalidos = con(() => true);
  if (invalidos.length > 0) return t(archivo ? 'formulario.faltaCorregirArchivo' : 'formulario.faltaCorregir', { campos: invalidos.join(', ') });
  return null;
}

/**
 * Cuerpo `datos` para la API: textos recortados, número con punto decimal (la API lo lee invariante), `SiNo` como
 * booleano y sin los opcionales vacíos (la API no los guarda).
 */
export function datosParaEnviar(campos: readonly CampoFormularioDto[], valores: ValoresFormulario): Record<string, string | boolean> {
  const datos: Record<string, string | boolean> = {};
  for (const campo of campos) {
    const valor = (valores[campo.clave] ?? '').trim();
    if (!valor) continue;
    const tipo = tipoDeCampo(campo);
    datos[campo.clave] = tipo === 'SiNo' ? valor === 'true' : tipo === 'Numero' ? valor.replace(',', '.') : valor;
  }
  return datos;
}

/**
 * Cuerpo de `POST /public/v1/boveda/formularios`: el vínculo y el tipo (propio de la empresa o canónico). Sin vínculo no
 * se puede enviar (`null`): un formulario es siempre del vínculo (TD-4).
 */
export function cuerpoFormulario(destino: DestinoCarga, datos: Record<string, string | boolean>): EnviarFormularioDto | null {
  if ('tipoDocumentoTenantId' in destino) return { vinculoId: destino.vinculoId, tipoDocumentoTenantId: destino.tipoDocumentoTenantId, datos };
  return destino.vinculoId ? { vinculoId: destino.vinculoId, tipoCanonico: destino.tipoCanonico, datos } : null;
}

/** Campo que señala un 422 del formulario (`campo-requerido`, `valor-invalido`, `campo-desconocido`): extensión `campo`. */
export function campoDelError(error: unknown): string | null {
  if (!esErrorApi(error) || error.estado !== 422) return null;
  const campo = error.extensiones.campo;
  return typeof campo === 'string' && campo ? campo : null;
}

/** El error ya se muestra en un campo de este formulario (no hace falta el aviso general). */
export function esErrorDeCampo(error: unknown, campos: readonly CampoFormularioDto[]): boolean {
  const campo = campoDelError(error);
  return campo !== null && campos.some((c) => c.clave === campo);
}
