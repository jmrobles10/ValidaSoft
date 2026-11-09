import type { ActualizarEmpresaDto, EmpresaDto } from '@validasoft/api-client';

/** Igual que `Empresa.TamanoMaximoLogo` del dominio (CF-1). */
export const TAMANO_MAXIMO_LOGO = 200 * 1024;
export const TIPOS_LOGO = ['image/svg+xml', 'image/png'] as const;

export interface BorradorMarca {
  contactoSoporte: string;
  colorMarca: string;
  logoUrl: string;
}

export type ErrorMarca = 'contacto-soporte-invalido' | 'color-marca-invalido';

export function borradorDe(empresa: Pick<EmpresaDto, 'replyTo' | 'colorMarca' | 'logoUrl'>): BorradorMarca {
  return { contactoSoporte: empresa.replyTo ?? '', colorMarca: empresa.colorMarca ?? '', logoUrl: empresa.logoUrl ?? '' };
}

/** Las mismas reglas que el dominio: correo válido y color `#RRGGBB` (vacíos se aceptan: quitan el valor). */
export function validarMarca(b: BorradorMarca): ErrorMarca[] {
  const errores: ErrorMarca[] = [];
  const correo = b.contactoSoporte.trim();
  if (correo && (correo.length > 254 || !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(correo))) errores.push('contacto-soporte-invalido');
  if (b.colorMarca.trim() && !/^#[0-9A-Fa-f]{6}$/.test(b.colorMarca.trim())) errores.push('color-marca-invalido');
  return errores;
}

/** Solo lo que cambió (nulo conserva; cadena vacía quita), con el color en mayúsculas. */
export function cuerpoActualizar(original: BorradorMarca, b: BorradorMarca): ActualizarEmpresaDto {
  const cambio = (antes: string, despues: string) => (antes.trim() === despues.trim() ? null : despues.trim());
  return {
    contactoSoporte: cambio(original.contactoSoporte, b.contactoSoporte),
    colorMarca: cambio(original.colorMarca.toUpperCase(), b.colorMarca.toUpperCase()),
    logoUrl: cambio(original.logoUrl, b.logoUrl),
  };
}

export function hayCambios(original: BorradorMarca, b: BorradorMarca): boolean {
  const cuerpo = cuerpoActualizar(original, b);
  return cuerpo.contactoSoporte !== null || cuerpo.colorMarca !== null || cuerpo.logoUrl !== null;
}

export type ErrorLogo = 'logo-tipo' | 'logo-tamano';

/** Valida un archivo de logo antes de leerlo. */
export function errorDeLogo(archivo: Pick<File, 'type' | 'size'>): ErrorLogo | null {
  if (!(TIPOS_LOGO as readonly string[]).includes(archivo.type)) return 'logo-tipo';
  if (archivo.size > TAMANO_MAXIMO_LOGO) return 'logo-tamano';
  return null;
}

/** Lee el logo como data URI (así viaja y se guarda en la empresa, sin almacenamiento aparte). */
export function leerComoDataUri(archivo: Blob): Promise<string> {
  return new Promise((resolver, rechazar) => {
    const lector = new FileReader();
    lector.onload = () => resolver(typeof lector.result === 'string' ? lector.result : '');
    lector.onerror = () => rechazar(lector.error ?? new Error('No se pudo leer el archivo.'));
    lector.readAsDataURL(archivo);
  });
}
