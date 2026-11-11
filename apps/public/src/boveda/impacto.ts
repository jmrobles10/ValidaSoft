import type { EmpresaAfectadaPorRetiroDto, ImpactoDeRetiroDto, SeccionAfectadaPorRetiroDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';

/** Qué le pasa a una empresa cuando el tercero retira la versión (el servidor lo decide; aquí solo se cuenta en lenguaje llano). */
export type EfectoDeRetiro = 'SinCambios' | 'VuelveALaAnterior' | 'Revalidar' | 'VolverARevisar' | 'SinVersion';

const EFECTOS: readonly EfectoDeRetiro[] = ['SinCambios', 'VuelveALaAnterior', 'Revalidar', 'VolverARevisar', 'SinVersion'];

/** Un efecto que la API agregue después se cuenta como «no cambia nada» en lugar de mostrar un texto equivocado. */
export function efectoDe(empresa: Pick<EmpresaAfectadaPorRetiroDto, 'efecto'>): EfectoDeRetiro {
  return EFECTOS.find((e) => e === empresa.efecto) ?? 'SinCambios';
}

/** A la empresa le toca revisar de nuevo o se queda sin el documento: es lo que obliga a avisarle y a ofrecer la alternativa. */
export function lePesa(empresa: Pick<EmpresaAfectadaPorRetiroDto, 'efecto'>): boolean {
  const efecto = efectoDe(empresa);
  return efecto === 'Revalidar' || efecto === 'VolverARevisar' || efecto === 'SinVersion';
}

/** Alguna empresa tendrá que revisar de nuevo (o se queda sin el documento): se ofrece «Subir una versión corregida». */
export function hayEmpresasAfectadas(impacto: Pick<ImpactoDeRetiroDto, 'empresas'>): boolean {
  return impacto.empresas.some(lePesa);
}

/** Las empresas de las que hay algo que contar, primero a las que les pesa. */
export function empresasOrdenadas(impacto: Pick<ImpactoDeRetiroDto, 'empresas'>): EmpresaAfectadaPorRetiroDto[] {
  return [...impacto.empresas].sort((a, b) => Number(lePesa(b)) - Number(lePesa(a)) || a.empresa.nombre.localeCompare(b.empresa.nombre, 'es'));
}

/** Qué versión queda vigente al retirar esta: una frase, sin ambigüedad. */
export function textoDeVersionVigente(impacto: Pick<ImpactoDeRetiroDto, 'esLaVigente' | 'versionQueQuedaVigente'>, tipo: string, t: TFunction): string {
  if (!impacto.esLaVigente) return t('boveda.retiro.sigueVigente', { n: String(impacto.versionQueQuedaVigente ?? '') });
  if (impacto.versionQueQuedaVigente === null) return t('boveda.retiro.sinVigente', { tipo });
  return t('boveda.retiro.quedaVigente', { tipo, n: String(impacto.versionQueQuedaVigente) });
}

/** Lo que le pasa a una empresa: «Constructora Norte la aprobó: tendrá que revisar de nuevo tu RUT versión 7». */
export function textoDeEmpresa(empresa: EmpresaAfectadaPorRetiroDto, impacto: Pick<ImpactoDeRetiroDto, 'versionQueQuedaVigente'>, tipo: string, t: TFunction): string {
  return t(`boveda.retiro.efectos.${efectoDe(empresa)}`, {
    empresa: empresa.empresa.nombre,
    tipo,
    n: String(impacto.versionQueQuedaVigente ?? ''),
    folio: empresa.folio ?? '',
  });
}

/** «Mi información general» / «Información financiera · 2025»: la sección con su periodo, como en el resumen de la ficha. */
export function nombreDeSeccion(seccion: Pick<SeccionAfectadaPorRetiroDto, 'nombre' | 'periodo'>, t: TFunction): string {
  return seccion.periodo ? t('ficha.nombreConPeriodo', { nombre: seccion.nombre, periodo: String(seccion.periodo) }) : seccion.nombre;
}
