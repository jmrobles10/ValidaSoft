import { esErrorApi, type RequisitoDelFlujoDto } from '@validasoft/api-client';
import { MAXIMO_PERIODOS, seccionFicha } from '../terceros/ficha/secciones';
import type { ErrorFlujo, RequisitoEnEditor } from './editor';

/**
 * Requisitos de sección de la ficha en el editor de flujos (F2, FM-25). Replican `Flujo.ValidarSeccion` del dominio para
 * avisar antes de enviar: sección del catálogo (`seccion-invalida`), sin repetir (`requisito-duplicado`), periodos solo
 * en la financiera (`periodos-no-aplica`) y entre 1 y 5 (`periodos-invalidos`). La API vuelve a validar.
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
  };
}

/** Códigos 422 de la API que corresponden a un requisito concreto (FM-25). */
const CODIGOS_DE_REQUISITO = ['seccion-invalida', 'requisito-duplicado', 'periodos-no-aplica', 'periodos-invalidos', 'requisito-invalido'] as const;
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
  return { codigo: error.codigo, campo: error.codigo.startsWith('periodos-') ? `${campo}.periodos` : campo };
}
