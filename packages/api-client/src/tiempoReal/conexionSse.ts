import { crearParserSse, type EventoSse } from './sse';

/**
 * - `conectando`: primer intento, aún sin respuesta.
 * - `conectado`: el stream está abierto.
 * - `reconectando`: se cayó y se reintenta con espera creciente.
 * - `respaldo`: falló varias veces seguidas; mientras sigue reintentando, quien lo usa refresca por su cuenta.
 */
export type EstadoConexion = 'conectando' | 'conectado' | 'reconectando' | 'respaldo';

export interface EsperasConexion {
  /** Primera espera tras una caída (ms); se duplica con cada falla seguida. */
  base: number;
  maximo: number;
  /** Fallas seguidas a partir de las cuales el estado pasa a `respaldo`. */
  fallosParaRespaldo: number;
  /** Sin recibir nada en este tiempo (el servidor manda un latido cada 15 s) se da por caída la conexión. */
  inactividad: number;
  /** Una conexión que duró menos que esto no reinicia la cuenta de fallas (evita reconectar en bucle cada `base`). */
  estable: number;
}

export const ESPERAS_POR_DEFECTO: EsperasConexion = { base: 1_000, maximo: 30_000, fallosParaRespaldo: 2, inactividad: 45_000, estable: 10_000 };

export interface OpcionesConexionSse {
  url: string;
  /** Cabeceras de cada intento (se leen al conectar: el token se renueva). `null` = aún no hay con qué conectar. */
  cabeceras: () => Record<string, string> | null;
  alEvento: (evento: EventoSse) => void;
  alEstado: (estado: EstadoConexion) => void;
  fetch: typeof fetch;
  esperas?: Partial<EsperasConexion>;
  /** Entre 0 y 1; agrega hasta un 20 % a cada espera para que no reconecten todos a la vez. */
  aleatorio?: () => number;
}

export interface ConexionSse {
  /** Cierra el stream y cancela los reintentos. */
  cerrar: () => void;
  /** Si está esperando para reintentar, reintenta ya (p. ej. llegó un token nuevo). */
  reconectarYa: () => void;
}

/** Espera antes del intento que sigue a `fallos` fallas seguidas (1, 2, 4… hasta el máximo), más el azar. */
export function esperaDeReconexion(fallos: number, esperas: Pick<EsperasConexion, 'base' | 'maximo'>, azar = 0): number {
  const sinAzar = Math.min(esperas.base * 2 ** Math.max(0, fallos - 1), esperas.maximo);
  return Math.round(sinAzar * (1 + 0.2 * azar));
}

/**
 * Abre un stream SSE con `fetch` (lleva cabeceras, a diferencia de `EventSource`) y lo mantiene: al caerse reconecta
 * con espera creciente y manda `Last-Event-ID` con el último id recibido, para que el servidor reenvíe lo que faltó.
 */
export function abrirConexionSse(opciones: OpcionesConexionSse): ConexionSse {
  const esperas = { ...ESPERAS_POR_DEFECTO, ...opciones.esperas };
  const aleatorio = opciones.aleatorio ?? Math.random;
  let cerrada = false;
  const estaCerrada = () => cerrada;
  let fallos = 0;
  let ultimoId = '';
  let reintentoServidor: number | null = null;
  let controlador: AbortController | null = null;
  let temporizador: ReturnType<typeof setTimeout> | null = null;
  let vigilante: ReturnType<typeof setTimeout> | null = null;

  const limpiarVigilante = () => {
    if (vigilante !== null) clearTimeout(vigilante);
    vigilante = null;
  };

  const vigilar = (alVencer: () => void) => {
    limpiarVigilante();
    vigilante = setTimeout(alVencer, esperas.inactividad);
  };

  const programar = () => {
    if (cerrada) return;
    opciones.alEstado(fallos >= esperas.fallosParaRespaldo ? 'respaldo' : 'reconectando');
    const base = reintentoServidor === null ? esperas : { ...esperas, base: Math.max(esperas.base, reintentoServidor) };
    temporizador = setTimeout(() => {
      temporizador = null;
      void conectar();
    }, esperaDeReconexion(fallos, base, aleatorio()));
  };

  const conectar = async () => {
    if (cerrada) return;
    const propias = opciones.cabeceras();
    if (!propias) {
      fallos += 1;
      programar();
      return;
    }
    const cabeceras = new Headers({ ...propias, Accept: 'text/event-stream' });
    if (ultimoId) cabeceras.set('Last-Event-ID', ultimoId);
    const actual = new AbortController();
    controlador = actual;
    let abiertaEn: number | null = null;
    try {
      const respuesta = await opciones.fetch(opciones.url, { headers: cabeceras, signal: actual.signal, cache: 'no-store' });
      if (!respuesta.ok || !respuesta.body) throw new Error(`El stream respondió ${String(respuesta.status)}.`);
      abiertaEn = Date.now();
      opciones.alEstado('conectado');
      const parser = crearParserSse((evento) => {
        ultimoId = evento.id;
        opciones.alEvento(evento);
      }, ultimoId);
      const lector = respuesta.body.getReader();
      const decodificador = new TextDecoder();
      vigilar(() => actual.abort());
      for (;;) {
        const { done, value } = await lector.read();
        if (done) break;
        vigilar(() => actual.abort());
        parser.agregar(decodificador.decode(value, { stream: true }));
        // Un `id` sin datos también cuenta para reanudar.
        ultimoId = parser.ultimoId;
        reintentoServidor = parser.reintento;
      }
      parser.agregar(decodificador.decode());
      ultimoId = parser.ultimoId;
    } catch {
      // Falla de red, respuesta no exitosa, inactividad o cierre: se decide abajo.
    } finally {
      limpiarVigilante();
      if (controlador === actual) controlador = null;
    }
    // `cerrar()` pudo llamarse mientras se esperaba la respuesta o el siguiente trozo.
    if (estaCerrada()) return;
    if (abiertaEn !== null && Date.now() - abiertaEn >= esperas.estable) fallos = 0;
    fallos += 1;
    programar();
  };

  opciones.alEstado('conectando');
  void conectar();

  return {
    cerrar() {
      cerrada = true;
      if (temporizador !== null) clearTimeout(temporizador);
      temporizador = null;
      limpiarVigilante();
      controlador?.abort();
      controlador = null;
    },
    reconectarYa() {
      if (cerrada || temporizador === null) return;
      clearTimeout(temporizador);
      temporizador = null;
      void conectar();
    },
  };
}
