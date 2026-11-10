import type { FirmanteDeAserciones, UsuarioDelegado } from './asercion.js';
import type {
  AbrirExpedienteDto,
  BloquearVinculoDto,
  CertificadoDelVinculoDto,
  CertificadoDto,
  ConsultaCertificacionDto,
  CrearVinculoDto,
  DescargaDto,
  EnlaceInvitacionDto,
  EventoPublicoDto,
  ExpedienteDto,
  InvitacionDto,
  JustificacionDto,
  PaginaEventosDto,
  SesionUsuarioDto,
  TipoEventoPublicoDto,
  VinculoDto,
} from './tipos.js';

/** Respuesta de error de la API (Problem Details): `codigo` es el código estable de la regla para decidir en el integrador. */
export class ErrorValidasoft extends Error {
  constructor(
    readonly estado: number,
    readonly codigo: string | undefined,
    readonly detalle: string | undefined,
    readonly correlacion: string | undefined,
  ) {
    super(`Validasoft respondió ${String(estado)}${codigo ? ` ${codigo}` : ''}: ${detalle ?? 'sin detalle'}`);
    this.name = 'ErrorValidasoft';
  }
}

export interface OpcionesCliente {
  /** Origen de la API, p. ej. `https://valida.acme.co` (sin `/api/v1`). */
  urlBase: string;
  /** `X-Api-Key` (`vsk_live_…`): solo en el backend, nunca en el navegador. */
  apiKey: string;
  /** Empresa interna por defecto (`X-Empresa`). */
  empresaId?: string;
  /** Firma las aserciones `X-Usuario-Delegado` para {@link ClienteValidasoft.comoUsuario}. */
  firmante?: FirmanteDeAserciones;
  /** `fetch` a usar (por defecto el global). */
  fetch?: typeof fetch;
  /** Reintentos de lo idempotente (GET, PUT y POST con clave de idempotencia) ante 5xx, 408, 429 o error de red. */
  reintentos?: number;
  /** Espera base entre reintentos en ms (se duplica en cada intento). */
  esperaMs?: number;
}

/** Respuesta de un GET condicional: `noModificado` = 304 (sigue valiendo la copia de `etag`). */
export interface RespuestaCondicional<T> {
  datos: T | null;
  etag: string | null;
  noModificado: boolean;
}

interface Peticion {
  metodo: 'GET' | 'POST' | 'PUT' | 'DELETE';
  ruta: string;
  cuerpo?: unknown;
  claveIdempotencia?: string;
  siNoCoincide?: string | null;
  reintentable?: boolean;
  conEmpresa?: boolean;
}

interface Contexto {
  opciones: Required<Pick<OpcionesCliente, 'urlBase' | 'apiKey' | 'reintentos' | 'esperaMs'>> & OpcionesCliente;
  empresaId: string | undefined;
  usuario: UsuarioDelegado | undefined;
}

const esTransitorio = (estado: number) => estado === 408 || estado === 429 || estado >= 500;

/** HTTP de bajo nivel: cabeceras, idempotencia, reintentos de lo idempotente y errores. */
async function enviar(ctx: Contexto, p: Peticion): Promise<RespuestaCondicional<unknown>> {
  const { opciones } = ctx;
  const hacerFetch = opciones.fetch ?? fetch;
  // La misma clave en todos los intentos: un reintento repite la respuesta guardada en lugar de crear otro recurso.
  const clave = p.metodo === 'POST' ? (p.claveIdempotencia ?? crypto.randomUUID()) : undefined;
  const reintentable = p.metodo === 'GET' || p.metodo === 'PUT' || p.reintentable === true;
  const url = `${opciones.urlBase.replace(/\/+$/, '')}/${p.ruta.replace(/^\/+/, '')}`;

  for (let intento = 0; ; intento++) {
    const cabeceras: Record<string, string> = { Accept: 'application/json', 'X-Api-Key': opciones.apiKey };
    if (p.conEmpresa !== false && ctx.empresaId) cabeceras['X-Empresa'] = ctx.empresaId;
    if (ctx.usuario) {
      if (!opciones.firmante) throw new Error('Para actuar como usuario configura `firmante` con la clave privada del sistema.');
      // Una aserción por intento: Validasoft rechaza un jti repetido (SE-4).
      cabeceras['X-Usuario-Delegado'] = await opciones.firmante.firmar(ctx.usuario);
    }
    if (clave) cabeceras['Idempotency-Key'] = clave;
    if (p.siNoCoincide) cabeceras['If-None-Match'] = p.siNoCoincide;
    if (p.cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json';

    let respuesta: Response;
    try {
      respuesta = await hacerFetch(url, { method: p.metodo, headers: cabeceras, body: p.cuerpo === undefined ? undefined : JSON.stringify(p.cuerpo) });
    } catch (error) {
      if (reintentable && intento < opciones.reintentos) {
        await esperar(opciones.esperaMs * 2 ** intento);
        continue;
      }
      throw error;
    }

    if (reintentable && intento < opciones.reintentos && esTransitorio(respuesta.status)) {
      await esperar(opciones.esperaMs * 2 ** intento);
      continue;
    }

    const etag = respuesta.headers.get('ETag');
    if (respuesta.status === 304) return { datos: null, etag: etag ?? p.siNoCoincide ?? null, noModificado: true };
    const texto = await respuesta.text();
    if (!respuesta.ok) throw error(respuesta, texto);
    return { datos: texto ? (JSON.parse(texto) as unknown) : null, etag, noModificado: false };
  }
}

function error(respuesta: Response, texto: string): ErrorValidasoft {
  let codigo: string | undefined;
  let detalle: string | undefined;
  try {
    const problema = JSON.parse(texto) as { codigo?: unknown; detail?: unknown; title?: unknown };
    codigo = typeof problema.codigo === 'string' ? problema.codigo : undefined;
    detalle = typeof problema.detail === 'string' ? problema.detail : typeof problema.title === 'string' ? problema.title : undefined;
  } catch {
    detalle = texto.slice(0, 300) || undefined;
  }
  return new ErrorValidasoft(respuesta.status, codigo, detalle, respuesta.headers.get('X-Correlation-Id') ?? undefined);
}

const esperar = (ms: number) => new Promise<void>((resolver) => setTimeout(resolver, ms));

/** El tipo de la respuesta lo fija el contrato de cada ruta (docs/05 §4). */
async function obtener<T>(ctx: Contexto, p: Peticion): Promise<T> {
  return (await enviar(ctx, p)).datos as T;
}

async function condicional<T>(ctx: Contexto, p: Peticion): Promise<RespuestaCondicional<T>> {
  return (await enviar(ctx, p)) as RespuestaCondicional<T>;
}

const q = encodeURIComponent;

/** Desde qué cursor, qué tipos y de a cuántos (1–200) leer los eventos. */
export interface FiltroEventos {
  desde?: string;
  tipos?: readonly string[];
  limite?: number;
}

async function* recorrer(cliente: ClienteValidasoft, filtro: FiltroEventos): AsyncGenerator<EventoPublicoDto> {
  let desde = filtro.desde;
  for (;;) {
    const pagina = await cliente.eventos.listar({ ...filtro, desde, limite: filtro.limite ?? 100 });
    for (const evento of pagina.datos) yield evento;
    if (!pagina.hayMas || pagina.datos.length === 0) return;
    desde = pagina.cursor;
  }
}

/**
 * Cliente de la API v1 para un sistema integrador (H9, docs/08 §2): vínculos, invitaciones, expedientes, certificados,
 * consulta de certificación, eventos (pull) y sesiones para pantallas embebidas. Lógica de negocio: ninguna.
 */
export class ClienteValidasoft {
  private readonly ctx: Contexto;

  /** @param derivado Uso interno de {@link paraEmpresa} y {@link comoUsuario}. */
  constructor(opciones: OpcionesCliente, derivado?: { empresaId: string | undefined; usuario: UsuarioDelegado | undefined }) {
    if (!opciones.apiKey) throw new Error('Configura la API key (apiKey).');
    if (!opciones.urlBase) throw new Error('Configura la URL de Validasoft (urlBase).');
    this.ctx = {
      opciones: { reintentos: 2, esperaMs: 300, ...opciones },
      empresaId: derivado ? derivado.empresaId : opciones.empresaId,
      usuario: derivado?.usuario,
    };
  }

  /** El mismo cliente sobre otra empresa interna (`X-Empresa`). */
  paraEmpresa(empresaId: string): ClienteValidasoft {
    return new ClienteValidasoft(this.ctx.opciones, { empresaId, usuario: this.ctx.usuario });
  }

  /** El mismo cliente en nombre de una persona del sistema (identidad delegada); requiere `firmante`. */
  comoUsuario(usuario: UsuarioDelegado): ClienteValidasoft {
    return new ClienteValidasoft(this.ctx.opciones, { empresaId: this.ctx.empresaId, usuario });
  }

  readonly vinculos = {
    /** Crea el vínculo (y el tercero si no existe). Con `claveIdempotencia` estable un reintento no duplica. */
    crear: (vinculo: CrearVinculoDto, claveIdempotencia?: string) =>
      obtener<VinculoDto>(this.ctx, { metodo: 'POST', ruta: 'api/v1/vinculos', cuerpo: vinculo, claveIdempotencia, reintentable: claveIdempotencia !== undefined }),
    obtener: (id: string) => obtener<VinculoDto>(this.ctx, { metodo: 'GET', ruta: `api/v1/vinculos/${q(id)}` }),
    porReferencia: (sistema: string, id: string) =>
      obtener<VinculoDto>(this.ctx, { metodo: 'GET', ruta: `api/v1/vinculos/por-referencia?sistema=${q(sistema)}&id=${q(id)}` }),
    bloquear: (id: string, bloqueo: BloquearVinculoDto) => obtener<VinculoDto>(this.ctx, { metodo: 'POST', ruta: `api/v1/vinculos/${q(id)}/bloquear`, cuerpo: bloqueo }),
    desbloquear: (id: string, justificacion: JustificacionDto) =>
      obtener<VinculoDto>(this.ctx, { metodo: 'POST', ruta: `api/v1/vinculos/${q(id)}/desbloquear`, cuerpo: justificacion }),
  };

  readonly invitaciones = {
    /** Envía (o reenvía) la invitación al tercero. */
    enviar: (vinculoId: string, invitacion: InvitacionDto = {}) =>
      obtener<EnlaceInvitacionDto>(this.ctx, { metodo: 'POST', ruta: `api/v1/vinculos/${q(vinculoId)}/invitaciones`, cuerpo: invitacion }),
  };

  readonly expedientes = {
    abrir: (vinculoId: string, expediente: AbrirExpedienteDto = {}, claveIdempotencia?: string) =>
      obtener<ExpedienteDto>(this.ctx, {
        metodo: 'POST',
        ruta: `api/v1/vinculos/${q(vinculoId)}/expedientes`,
        cuerpo: expediente,
        claveIdempotencia,
        reintentable: claveIdempotencia !== undefined,
      }),
    obtener: (id: string) => obtener<ExpedienteDto>(this.ctx, { metodo: 'GET', ruta: `api/v1/expedientes/${q(id)}` }),
  };

  readonly certificados = {
    delVinculo: (vinculoId: string) => obtener<CertificadoDelVinculoDto>(this.ctx, { metodo: 'GET', ruta: `api/v1/vinculos/${q(vinculoId)}/certificado` }),
    obtener: (folio: string) => obtener<CertificadoDto>(this.ctx, { metodo: 'GET', ruta: `api/v1/certificados/${q(folio)}` }),
    /** URL prefirmada del PDF (5 minutos); cada descarga queda auditada. */
    descargar: (folio: string) => obtener<DescargaDto>(this.ctx, { metodo: 'POST', ruta: `api/v1/certificados/${q(folio)}/descarga` }),
  };

  readonly consultas = {
    /**
     * Consulta rápida de certificación (scope `consultas:leer`). Con `etag` (el de la respuesta anterior) la API responde
     * 304 si nada cambió: `noModificado` y se sigue usando la copia local.
     */
    certificacion: async (
      consulta: { tipoIdentificacion: string; numero: string; tipoVinculo?: string; pais?: string },
      etag?: string | null,
    ): Promise<RespuestaCondicional<ConsultaCertificacionDto>> => {
      const parametros = new URLSearchParams({ pais: consulta.pais ?? 'CO', tipoIdentificacion: consulta.tipoIdentificacion, numero: consulta.numero });
      if (consulta.tipoVinculo) parametros.set('tipoVinculo', consulta.tipoVinculo);
      return condicional<ConsultaCertificacionDto>(this.ctx, {
        metodo: 'GET',
        ruta: `api/v1/consultas/certificacion?${parametros.toString()}`,
        siNoCoincide: etag ?? null,
      });
    },
    certificacionDelVinculo: (vinculoId: string, etag?: string | null): Promise<RespuestaCondicional<ConsultaCertificacionDto>> =>
      condicional<ConsultaCertificacionDto>(this.ctx, { metodo: 'GET', ruta: `api/v1/consultas/certificacion?vinculoId=${q(vinculoId)}`, siNoCoincide: etag ?? null }),
  };

  readonly eventos = {
    /** Una página del modo pull desde `desde` (el cursor del último evento procesado). */
    listar: (filtro: FiltroEventos & { empresaId?: string } = {}) => {
      const parametros = new URLSearchParams();
      if (filtro.desde) parametros.set('desde', filtro.desde);
      if (filtro.tipos?.length) parametros.set('tipos', filtro.tipos.join(','));
      if (filtro.limite !== undefined) parametros.set('limite', String(filtro.limite));
      if (filtro.empresaId) parametros.set('empresaId', filtro.empresaId);
      const consulta = parametros.toString();
      return obtener<PaginaEventosDto>(this.ctx, { metodo: 'GET', ruta: `api/v1/eventos${consulta ? `?${consulta}` : ''}`, conEmpresa: !filtro.empresaId });
    },
    /** Recorre todas las páginas hasta el último evento disponible; guarda `evento.cursor` al procesar cada uno. */
    recorrer: (filtro: FiltroEventos = {}): AsyncGenerator<EventoPublicoDto> => recorrer(this, filtro),
    tipos: () => obtener<TipoEventoPublicoDto[]>(this.ctx, { metodo: 'GET', ruta: 'api/v1/eventos/tipos' }),
  };

  readonly sesiones = {
    /**
     * Canjea la identidad delegada por un token de 30 minutos para el iframe (`Authorization: Bearer`). Solo desde
     * {@link ClienteValidasoft.comoUsuario}.
     */
    crear: () => {
      if (!this.ctx.usuario) return Promise.reject(new Error('La sesión es de una persona: usa cliente.comoUsuario(usuario).sesiones.crear().'));
      return obtener<SesionUsuarioDto>(this.ctx, { metodo: 'POST', ruta: 'api/v1/sesiones-usuario', conEmpresa: false });
    },
  };
}
