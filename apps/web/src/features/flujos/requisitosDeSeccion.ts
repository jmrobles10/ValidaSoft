import { esErrorApi, type RequisitoDelFlujoDto } from '@validasoft/api-client';
import { MAXIMO_PERIODOS, seccionFicha, type CodigoSeccionFicha } from '../terceros/ficha/secciones';
import type { ErrorFlujo, RequisitoEnEditor } from './editor';
import { MAXIMO_DIAS_REVALIDACION, MAXIMO_VIGENCIA_VINCULO_DIAS } from './vigencia';

/**
 * Requisitos de sección de la ficha en el editor de flujos (F2, FM-25). Replican `Flujo.ValidarSeccion` del dominio para
 * avisar antes de enviar: sección del catálogo (`seccion-invalida`), sin repetir (`requisito-duplicado`), periodos solo
 * en la financiera (`periodos-no-aplica`) y entre 1 y 5 (`periodos-invalidos`), vigencia propia de 1 a 3650 días
 * (`vigencia-seccion-invalida`) y plazo de revalidación de 1 a 365 (`dias-revalidacion-invalidos`). La API vuelve a validar.
 */

export function validarRequisitoDeSeccion(requisito: RequisitoEnEditor, campo: string, vistas: Set<string>): ErrorFlujo[] {
  const definicion = seccionFicha(requisito.seccion);
  if (!definicion) return [{ codigo: 'seccion-invalida', campo }];
  const errores: ErrorFlujo[] = [];
  if (vistas.has(definicion.codigo)) errores.push({ codigo: 'requisito-duplicado', campo });
  vistas.add(definicion.codigo);
  if (requisito.periodos !== null) {
    if (!definicion.porPeriodo) errores.push({ codigo: 'periodos-no-aplica', campo: `${campo}.periodos` });
    else if (!Number.isInteger(requisito.periodos) || requisito.periodos < 1 || requisito.periodos > MAXIMO_PERIODOS) {
      errores.push({ codigo: 'periodos-invalidos', campo: `${campo}.periodos` });
    }
  }
  if (requisito.vigenciaDias !== null && (!Number.isInteger(requisito.vigenciaDias) || requisito.vigenciaDias < 1 || requisito.vigenciaDias > MAXIMO_VIGENCIA_VINCULO_DIAS)) {
    errores.push({ codigo: 'vigencia-seccion-invalida', campo: `${campo}.vigencia` });
  }
  if (requisito.diasRevalidacion !== null && (!Number.isInteger(requisito.diasRevalidacion) || requisito.diasRevalidacion < 1 || requisito.diasRevalidacion > MAXIMO_DIAS_REVALIDACION)) {
    errores.push({ codigo: 'dias-revalidacion-invalidos', campo: `${campo}.revalidacion` });
  }
  return errores;
}

/** Requisito de sección para la API: sin tipo de documento; los periodos solo en la financiera (1 por omisión). */
export function requisitoDeSeccionADto(r: RequisitoEnEditor): RequisitoDelFlujoDto {
  const porPeriodo = seccionFicha(r.seccion)?.porPeriodo ?? false;
  return {
    id: r.id,
    tipoDocumentoTenantId: null,
    tipoDocumento: null,
    obligatorio: r.obligatorio,
    areasQueRevisan: r.areasQueRevisan,
    aprobadoresRequeridos: r.aprobadoresRequeridos,
    condicion: r.condicion,
    seccion: r.seccion,
    periodos: porPeriodo ? (r.periodos ?? 1) : null,
    exigeSoporte: r.exigeSoporte,
    vigenciaDias: r.vigenciaDias,
    diasRevalidacion: r.diasRevalidacion,
  };
}

/** Códigos 422 de la API que corresponden a un requisito concreto (FM-25). */
const CODIGOS_DE_REQUISITO = [
  'seccion-invalida',
  'requisito-duplicado',
  'periodos-no-aplica',
  'periodos-invalidos',
  'requisito-invalido',
  'vigencia-seccion-invalida',
  'dias-revalidacion-invalidos',
] as const;
type CodigoDeRequisito = (typeof CODIGOS_DE_REQUISITO)[number];

const esCodigoDeRequisito = (codigo: string): codigo is CodigoDeRequisito => (CODIGOS_DE_REQUISITO as readonly string[]).includes(codigo);

/**
 * Lleva un 422 de la API al requisito que lo causó, para mostrarlo en su campo y no solo arriba: la API dice su posición en
 * la extensión `requisito`. `null` si el error no es de un requisito o no trae la posición.
 */
export function errorDeApiEnRequisitos(error: unknown, requisitos: readonly RequisitoEnEditor[]): ErrorFlujo | null {
  if (!esErrorApi(error) || !error.esValidacion || !esCodigoDeRequisito(error.codigo)) return null;
  const indice = error.extensiones.requisito;
  if (typeof indice !== 'number' || !Number.isInteger(indice) || indice < 0 || indice >= requisitos.length) return null;
  const campo = `requisito.${String(indice)}`;
  const sufijo = error.codigo.startsWith('periodos-') ? '.periodos' : error.codigo === 'vigencia-seccion-invalida' ? '.vigencia' : error.codigo === 'dias-revalidacion-invalidos' ? '.revalidacion' : '';
  return { codigo: error.codigo, campo: `${campo}${sufijo}` };
}

/**
 * Secciones que respalda cada tipo canónico de documento (`MapaDeRespaldo` de la plataforma, docs/14 §5.1): el mismo mapa que
 * usan el cruce, «Usar como soporte» y la actualización desde el documento.
 */
const RESPALDA: Readonly<Record<string, readonly CodigoSeccionFicha[]>> = {
  RUT: ['FICHA_GENERAL', 'FICHA_TRIBUTARIA', 'FICHA_LEGAL'],
  CAMARA_COMERCIO: ['FICHA_GENERAL', 'FICHA_LEGAL'],
  ESTADOS_FINANCIEROS: ['FICHA_FINANCIERA'],
  CEDULA: ['FICHA_GENERAL'],
};

/** Secciones de la ficha que respalda un documento de ese tipo canónico (vacío = no respalda ninguna). */
export function seccionesQueRespalda(tipoCanonico: string | null | undefined): readonly CodigoSeccionFicha[] {
  return tipoCanonico ? (RESPALDA[tipoCanonico.toUpperCase()] ?? []) : [];
}
