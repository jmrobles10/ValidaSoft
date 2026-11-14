import type { AccesoSolicitadoDto, ContactoDelTerceroDto, EmpresaQueInvitoDto, SesionActivaDto } from '@validasoft/api-client';

/** Lo que falta decidir a los contactos del tercero. */
export const porDecidir = (solicitudes: readonly AccesoSolicitadoDto[]): AccesoSolicitadoDto[] => solicitudes.filter((s) => s.estado === 'Pendiente');

/** Lo que falta decidir y esta sesión puede decidir: solo un contacto verificado aprueba o rechaza (la API no lo dice con `puedesDecidir: false`). */
export const porDecidirPorMi = (solicitudes: readonly AccesoSolicitadoDto[]): AccesoSolicitadoDto[] => porDecidir(solicitudes).filter((s) => s.puedesDecidir !== false);

/** ¿Esta sesión decide esta solicitud? Sin el dato (respuestas antiguas) se supone que sí; la API es la que impone la regla. */
export const puedoDecidir = (solicitud: AccesoSolicitadoDto): boolean => solicitud.puedesDecidir !== false;

/** Cómo se llama en pantalla lo que dice la API del estado de un contacto: desplazado pesa más que sin verificar. */
export function estadoDelContacto(contacto: Pick<ContactoDelTerceroDto, 'verificacion' | 'desplazado'>): 'Verificado' | 'Desplazado' | 'SinVerificar' {
  if (contacto.verificacion === 'Verificado') return 'Verificado';
  return contacto.desplazado ? 'Desplazado' : 'SinVerificar';
}

/** Los contactos con la sesión actual primero y luego verificados, sin verificar y desplazados, por correo. */
export const ordenarContactos = (contactos: readonly ContactoDelTerceroDto[]): ContactoDelTerceroDto[] => {
  const peso = (c: ContactoDelTerceroDto) => ({ Verificado: 0, SinVerificar: 1, Desplazado: 2 })[estadoDelContacto(c)];
  return [...contactos].sort((a, b) => Number(b.esTuCorreo) - Number(a.esTuCorreo) || peso(a) - peso(b) || a.correo.localeCompare(b.correo));
};

/** Lo ya decidido (aprobado o rechazado), lo más reciente primero. */
export const decididas = (solicitudes: readonly AccesoSolicitadoDto[]): AccesoSolicitadoDto[] =>
  solicitudes.filter((s) => s.estado !== 'Pendiente').sort((a, b) => (b.decididoEn ?? '').localeCompare(a.decididoEn ?? ''));

/** «A», «A y B», «A, B y C»: sin repetir una empresa que invitó con varios vínculos. */
export function listaDeEmpresas(empresas: readonly EmpresaQueInvitoDto[]): string {
  const nombres = [...new Set(empresas.map((e) => e.empresa))];
  if (nombres.length <= 1) return nombres[0] ?? '';
  return `${nombres.slice(0, -1).join(', ')} y ${nombres[nombres.length - 1] ?? ''}`;
}

/** Desde cuándo está pidiendo acceso un correo: la primera invitación. */
export const desdeCuando = (solicitud: AccesoSolicitadoDto): string => solicitud.desde;

/** El nombre del tercero con las letras tapadas que viene en la sesión, o un texto neutro si no llegó. */
export const nombreDelTercero = (enmascarado: string | null | undefined, sinNombre: string): string => (enmascarado && enmascarado.trim() !== '' ? enmascarado : sinNombre);

/** Navegador y sistema que se reconocen en el `User-Agent` de una sesión (`null` si no se reconoce nada). */
export function reconocerDispositivo(agente: string | null | undefined): { navegador: string | null; sistema: string | null } {
  const ua = agente ?? '';
  const tiene = (...marcas: string[]) => marcas.some((marca) => ua.includes(marca));
  const navegador = /Edg(e|A|iOS)?\//.test(ua)
    ? 'Edge'
    : tiene('OPR/', 'Opera')
      ? 'Opera'
      : tiene('Firefox/', 'FxiOS/')
        ? 'Firefox'
        : tiene('Chrome/', 'CriOS/')
          ? 'Chrome'
          : tiene('Safari/')
            ? 'Safari'
            : null;
  const sistema = tiene('Windows')
    ? 'Windows'
    : tiene('Android')
      ? 'Android'
      : tiene('iPhone', 'iPad', 'iPod')
        ? 'iOS'
        : tiene('Mac OS X', 'Macintosh')
          ? 'macOS'
          : tiene('Linux', 'X11')
            ? 'Linux'
            : null;
  return { navegador, sistema };
}

/** La sesión de este navegador primero y luego las demás, la más reciente primero. */
export const ordenarSesiones = (sesiones: readonly SesionActivaDto[]): SesionActivaDto[] =>
  [...sesiones].sort((a, b) => Number(b.esActual) - Number(a.esActual) || b.creadaEn.localeCompare(a.creadaEn));
