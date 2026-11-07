import { esRutaDeSesion } from './sesionApi';

function urlDe(entrada: RequestInfo | URL): string {
  if (typeof entrada === 'string') return entrada;
  return entrada instanceof URL ? entrada.href : entrada.url;
}

/**
 * `fetch` del cliente con un reintento: si una llamada responde 401 (el access venció antes de la renovación
 * programada, o el equipo estuvo suspendido), renueva una vez con la cookie y repite la petición con el token nuevo.
 * Las rutas de sesión, enlace y acceso no se reintentan. Si la renovación falla, devuelve el 401 original (el cliente
 * lo convierte en error y la sesión se da por terminada).
 */
export function envolverConRenovacion(base: typeof fetch, renovar: () => Promise<string | null>): typeof fetch {
  return async (entrada, init) => {
    const respuesta = await base(entrada, init);
    if (respuesta.status !== 401 || esRutaDeSesion(urlDe(entrada))) return respuesta;
    const token = await renovar();
    if (!token) return respuesta;
    const cabeceras = new Headers(init?.headers);
    cabeceras.set('Authorization', `Bearer ${token}`);
    return base(entrada, { ...init, headers: cabeceras });
  };
}
