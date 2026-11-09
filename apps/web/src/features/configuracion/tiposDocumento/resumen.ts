import type { TipoDocumentoTenantDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { megas, nombreDeFormato } from './modelo';

/** Lista en español: «PDF», «PDF o JPG», «PDF, JPG o PNG». */
export function enumerar(partes: readonly string[]): string {
  if (partes.length <= 1) return partes.join('');
  return `${partes.slice(0, -1).join(', ')} o ${partes.at(-1) ?? ''}`;
}

/** Reglas de archivo efectivas en corto: «PDF · hasta 5 MB · 10 págs.». Vacío si el tipo no recibe archivo. */
export function resumenArchivo(tipo: Pick<TipoDocumentoTenantDto, 'modo' | 'reglasEfectivas'>, t: TFunction): string {
  const reglas = tipo.reglasEfectivas;
  if (tipo.modo === 'Formulario' || !reglas) return '';
  const formatos = enumerar(reglas.formatos.map(nombreDeFormato));
  return t('tiposDocumento.resumen.archivo', { formatos, peso: megas(reglas.tamanoMaximoBytes), paginas: String(reglas.paginasMaximas) });
}

/** Cómo se entrega y qué trae: «PDF · hasta 5 MB · 50 págs. · con plantilla» o «Formulario (4 campos)». */
export function resumenTipo(tipo: Pick<TipoDocumentoTenantDto, 'modo' | 'reglasEfectivas' | 'campos' | 'plantilla'>, t: TFunction): string {
  const partes: string[] = [];
  const archivo = resumenArchivo(tipo, t);
  if (archivo) partes.push(archivo);
  const campos = tipo.campos?.length ?? 0;
  if (tipo.modo === 'Formulario' || tipo.modo === 'ArchivoYFormulario') partes.push(t('tiposDocumento.resumen.formulario', { count: campos }));
  if (tipo.plantilla) partes.push(t('tiposDocumento.resumen.conPlantilla'));
  return partes.join(' · ');
}

/** Regla de vigencia en palabras: «No vence», «Vale 30 días desde su expedición». */
export function resumenVigencia(tipo: Pick<TipoDocumentoTenantDto, 'reglaFrescura'>, t: TFunction): string {
  const { tipo: regla, dias, regla: periodo } = tipo.reglaFrescura;
  switch (regla) {
    case 'MaxDiasDesdeExpedicion':
      return t('tiposDocumento.vigencia.resumen.expedicion', { count: dias ?? 0 });
    case 'MaxDiasDesdeAprobacion':
      return t('tiposDocumento.vigencia.resumen.carga', { count: dias ?? 0 });
    case 'PeriodoMinimo':
      return t('tiposDocumento.vigencia.resumen.periodo', { periodo: periodo ?? '—' });
    default:
      return t('tiposDocumento.vigencia.resumen.ninguna');
  }
}
