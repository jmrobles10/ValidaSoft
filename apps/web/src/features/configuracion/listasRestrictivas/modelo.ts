import type { CredencialesListasDto, ModoBusquedaListas, ProveedorDePlataformaDto, ProveedorListasDto } from '@validasoft/api-client';

/** Formulario de credenciales de un proveedor real (la clave nunca llega del servidor: vacío = conservar la actual). */
export interface BorradorCredenciales {
  usuario: string;
  clave: string;
  urlBase: string;
  plan: string;
  cuotaMensual: string;
  modoBusqueda: ModoBusquedaListas;
}

export function borradorCredenciales(p: ProveedorListasDto): BorradorCredenciales {
  return {
    usuario: p.usuario ?? '',
    clave: '',
    urlBase: p.urlBase ?? '',
    plan: p.plan ?? '',
    cuotaMensual: p.cuotaMensual === null ? '' : String(p.cuotaMensual),
    modoBusqueda: p.modoBusqueda === 'PorDocumento' ? 'PorDocumento' : 'PorNombre',
  };
}

/** El formulario de la cuenta de la plataforma (G-2): la cuota es la de cada empresa. */
export function borradorDePlataforma(p: ProveedorDePlataformaDto): BorradorCredenciales {
  return {
    usuario: p.usuario ?? '',
    clave: '',
    urlBase: p.urlBase ?? '',
    plan: p.plan ?? '',
    cuotaMensual: p.cuotaMensualPorEmpresa === null ? '' : String(p.cuotaMensualPorEmpresa),
    modoBusqueda: p.modoBusqueda === 'PorDocumento' ? 'PorDocumento' : 'PorNombre',
  };
}

/**
 * Cuerpo de `PUT /configuracion/listas-restrictivas`: la clave vacía no se envía (`null` conserva la actual) salvo para
 * quitar las credenciales (`""`). Lo que no es número en la cuota se envía tal cual para que la API responda 422.
 */
export function credencialesADto(proveedor: string, b: BorradorCredenciales, quitar = false): CredencialesListasDto {
  if (quitar) return { proveedor, clave: '' };
  const cuota = b.cuotaMensual.trim();
  return {
    proveedor,
    usuario: b.usuario.trim() || null,
    clave: b.clave.trim() ? b.clave : null,
    urlBase: b.urlBase.trim() || null,
    plan: b.plan.trim() || null,
    cuotaMensual: cuota === '' ? null : Number(cuota),
    modoBusqueda: b.modoBusqueda,
  };
}
