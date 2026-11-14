import type { SolicitudDeAccesoDePlataformaDto } from '@validasoft/api-client';

/** «A», «A y B», «A, B y C»: sin repetir una empresa que invitó con varios vínculos. */
export function listaDeEmpresas(solicitud: Pick<SolicitudDeAccesoDePlataformaDto, 'empresas'>): string {
  const nombres = [...new Set(solicitud.empresas.map((e) => e.empresa))];
  if (nombres.length <= 1) return nombres[0] ?? '';
  return `${nombres.slice(0, -1).join(', ')} y ${nombres[nombres.length - 1] ?? ''}`;
}
