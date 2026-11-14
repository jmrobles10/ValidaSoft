import type { FlujoDeExpedienteDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { describirCondicion } from '../flujos/condiciones';

/**
 * Por qué el expediente tiene su flujo (FR-1): lo eligieron al abrirlo o al crear el vínculo, lo escogió una regla de selección (con la
 * condición que se cumplió, tal como estaba en la versión publicada y la posición en que se evaluó) o no se cumplió ninguna y se usó el
 * flujo por defecto del tipo de vínculo.
 */
export function explicarOrigenDelFlujo(flujo: Pick<FlujoDeExpedienteDto, 'origen' | 'regla'>, t: TFunction): string {
  if (flujo.origen !== 'Regla') return t(`origenDelFlujo.${flujo.origen}`);
  const regla = flujo.regla;
  if (!regla) return t('origenDelFlujo.ReglaSinDetalle');
  return t('origenDelFlujo.Regla', {
    condicion: describirCondicion(regla.condicion, t),
    prioridad: String(regla.prioridad),
    evaluadas: String(regla.reglasEvaluadas),
  });
}
