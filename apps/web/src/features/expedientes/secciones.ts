import type { CambioDeCampoDto, DocumentoVistaEmpresaDto, RequisitoExpedienteDto, SoporteSeccionDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { datosDe, esVersionSinArchivo } from '../documentos/formatoDatos';
import type { DocumentoEnVisor } from '../documentos/VisorDocumento';
import { formatearValor } from '../terceros/ficha/formato';

/**
 * Reglas de la UI de un requisito de sección de la ficha en el expediente (F2, FM-26, FM-28): qué es una sección, cómo
 * se abre su soporte y cómo se lee cada cambio frente a la versión aceptada. La completitud para la empresa y la
 * comparación las calcula la API; aquí solo se presentan.
 */

export function esRequisitoDeSeccion(requisito: Pick<RequisitoExpedienteDto, 'tipoRequisito' | 'seccion'>): boolean {
  return requisito.tipoRequisito === 'Seccion' || Boolean(requisito.seccion);
}

/**
 * El soporte en el visor si es un documento de la bóveda compartido con el vínculo (en esa versión); `null` si la
 * empresa no lo puede ver (un adjunto propio de la ficha o una versión no compartida).
 */
export function soporteEnVisor(soporte: SoporteSeccionDto, documentos: readonly DocumentoVistaEmpresaDto[]): DocumentoEnVisor | null {
  const documento = documentos.find((d) => d.documentoId === soporte.documentoId);
  const version = documento?.versionesCompartidas.find((v) => v.n === soporte.version);
  if (!documento || !version) return null;
  return {
    documentoId: documento.documentoId,
    tipo: documento.tipo.nombre,
    n: version.n,
    cargadaEn: version.cargadaEn,
    datosDeclarados: datosDe(version),
    vigencia: version.vigencia ?? null,
    sinArchivo: esVersionSinArchivo(version),
    hechos: version.hechos,
    tipoEsperado: { codigo: documento.tipo.codigo || null, nombre: documento.tipo.nombre },
  };
}

/** Una fila de la tabla «antes → ahora»: qué cambió (legible), los dos valores y el tipo de cambio. */
export interface FilaDeCambio {
  clave: string;
  campo: string;
  antes: string | null;
  ahora: string | null;
  tipo: CambioDeCampoDto['tipoCambio'];
}

/** `personas[1]` (la fila completa) frente a `personas[1].porcentaje` (un campo de la fila). */
const esFila = (campo: string) => campo.endsWith(']');
const dentroDeFila = (campo: string) => campo.includes('].');
const NUMERO = /^-?\d+(\.\d+)?$/;

/** El valor como lo lee una persona: cifras en pesos, fechas dd/mm/aaaa, países por su nombre (como en los datos). */
function valorLegible(campo: string, valor: string | null, t: TFunction): string | null {
  if (valor === null) return null;
  const clave = campo.split('.').at(-1)?.replace(/\[\d+\]$/, '') ?? campo;
  if (NUMERO.test(valor)) return formatearValor(clave, Number(valor), t);
  if (clave.startsWith('fecha') || ['pais', 'nacionalidad', 'residencia', 'paises'].includes(clave)) return formatearValor(clave, valor, t);
  return valor;
}

/**
 * Lo que muestra la comparación: los campos con su etiqueta (la de la API, «Socio: Juan Pérez · Porcentaje…» dentro de
 * una fila) y las filas de personas o ICA como «Se agregó …», «Se quitó …» o «Se modificó …».
 */
export function filasDeCambio(cambios: readonly CambioDeCampoDto[], t: TFunction): FilaDeCambio[] {
  return cambios.map((c, i) => {
    const clave = `${c.campo}-${String(i)}`;
    if (esFila(c.campo)) {
      const etiqueta = c.etiqueta;
      const campo = c.tipoCambio === 'Agregado' ? t('fichaEmpresa.comparacion.agregada', { etiqueta }) : t('fichaEmpresa.comparacion.quitada', { etiqueta });
      return { clave, campo, antes: c.anterior, ahora: c.nuevo, tipo: c.tipoCambio };
    }
    const campo = dentroDeFila(c.campo) ? t('fichaEmpresa.comparacion.modificada', { etiqueta: c.etiqueta }) : c.etiqueta;
    return { clave, campo, antes: valorLegible(c.campo, c.anterior, t), ahora: valorLegible(c.campo, c.nuevo, t), tipo: c.tipoCambio };
  });
}
