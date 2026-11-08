/**
 * Página de demostración: hace de sistema integrado que aloja Validasoft en un iframe (ID-3). Muestra el contrato
 * postMessage de `sesion/puenteEmbed.ts` desde el lado del contenedor: responde a `sesion-requerida` emitiendo una
 * sesión, ajusta la altura con `resize`, hace scroll hasta la región de `visible` y envía el tema.
 */
import { abrirSesionDelegada, cargarSemilla, type SemillaDev } from '../dev/firmarAsercion';

interface MensajeDeValidasoft {
  tipo: string;
  motivo?: string;
  height?: number;
  top?: number;
  version?: number;
}

/**
 * `validasoft:visible`: la región `top..top+height` del iframe (un diálogo o panel recién abierto) debe verse. Solo
 * desplaza si hace falta y lo mínimo; si la región es más alta que la ventana, muestra su borde superior.
 */
function mostrarRegion(marcoIframe: HTMLIFrameElement, top: number, alto: number) {
  const margen = 16;
  const arriba = marcoIframe.getBoundingClientRect().top + top;
  const abajo = arriba + alto;
  let desplazamiento = 0;
  if (arriba < margen || alto + 2 * margen > window.innerHeight) desplazamiento = arriba - margen;
  else if (abajo > window.innerHeight - margen) desplazamiento = abajo - window.innerHeight + margen;
  if (desplazamiento !== 0) window.scrollBy(0, desplazamiento);
}

function elemento(id: string): HTMLElement {
  const encontrado = document.getElementById(id);
  if (!encontrado) throw new Error(`Falta #${id} en embed-demo.html`);
  return encontrado;
}

const persona = elemento('persona') as HTMLSelectElement;
const empresa = elemento('empresa') as HTMLSelectElement;
const tema = elemento('tema') as HTMLSelectElement;
const botonAbrir = elemento('abrir') as HTMLButtonElement;
const botonRenovar = elemento('renovar') as HTMLButtonElement;
const botonCerrar = elemento('cerrar') as HTMLButtonElement;
const cajaError = elemento('error');
const bitacora = elemento('bitacora');
const marco = document.querySelector<HTMLElement>('.marco');
const ORIGEN = window.location.origin;

let semilla: SemillaDev | null = null;
let iframe: HTMLIFrameElement | null = null;

function registrar(direccion: 'entrante' | 'saliente', texto: string) {
  const item = document.createElement('li');
  item.className = direccion;
  item.textContent = `${new Date().toLocaleTimeString('es-CO')} ${texto}`;
  bitacora.prepend(item);
}

function mostrarError(mensaje: string | null) {
  cajaError.hidden = !mensaje;
  cajaError.textContent = mensaje ?? '';
}

function enviar(mensaje: Record<string, unknown>, resumen: string) {
  iframe?.contentWindow?.postMessage(mensaje, ORIGEN);
  registrar('saliente', resumen);
}

let canjeEnCurso: Promise<void> | null = null;

/** Canjea la sesión de la persona elegida y la entrega al iframe. Si ya hay un canje en curso, no inicia otro. */
function entregarSesion(): Promise<void> {
  canjeEnCurso ??= canjear().finally(() => {
    canjeEnCurso = null;
  });
  return canjeEnCurso;
}

async function canjear() {
  if (!semilla) {
    mostrarError('Aún no se cargan las credenciales de la semilla.');
    return;
  }
  const elegida = semilla.usuarios.find((u) => u.sub === persona.value);
  if (!elegida) return;
  registrar('saliente', `POST /api/v1/sesiones-usuario (${elegida.nombre})…`);
  try {
    mostrarError(null);
    const limite = new Promise<never>((_, rechazar) =>
      window.setTimeout(() => rechazar(new Error('La API no respondió en 10 s. ¿Está corriendo en el puerto 5080?')), 10_000),
    );
    const sesion = await Promise.race([
      abrirSesionDelegada({ apiKey: semilla.apiKey, sistema: semilla.sistema.codigo, clavePrivadaPem: semilla.clavePrivadaPem }, elegida),
      limite,
    ]);
    enviar(
      { tipo: 'validasoft:sesion', token: sesion.token, empresaId: empresa.value || null },
      `validasoft:sesion (${elegida.nombre}, vence ${new Date(sesion.expira).toLocaleTimeString('es-CO')})`,
    );
  } catch (error) {
    const mensaje = error instanceof Error ? error.message : 'No se pudo abrir la sesión.';
    registrar('saliente', `error: ${mensaje}`);
    mostrarError(mensaje);
  }
}

function enviarTema() {
  enviar({ tipo: 'validasoft:tema', mode: tema.value }, `validasoft:tema ${tema.value}`);
}

function abrir() {
  if (!marco) return;
  marco.replaceChildren();
  iframe = document.createElement('iframe');
  iframe.title = 'Validasoft';
  iframe.src = `${import.meta.env.BASE_URL}index.html#/`;
  marco.append(iframe);
  botonRenovar.disabled = false;
  botonCerrar.disabled = false;
  registrar('saliente', 'iframe creado');
}

window.addEventListener('message', (evento: MessageEvent<MensajeDeValidasoft>) => {
  // Solo el iframe propio y su origen: cualquier otro mensaje se ignora.
  if (!iframe) return;
  if (evento.source !== iframe.contentWindow || evento.origin !== ORIGEN) return;
  const { tipo } = evento.data;

  switch (tipo) {
    case 'validasoft:ready':
      registrar('entrante', `validasoft:ready (protocolo v${String(evento.data.version ?? '?')})`);
      enviarTema();
      break;
    case 'validasoft:sesion-requerida':
      registrar('entrante', `validasoft:sesion-requerida (${evento.data.motivo ?? '—'})`);
      void entregarSesion();
      break;
    case 'validasoft:resize':
      if (typeof evento.data.height === 'number') iframe.style.height = `${String(Math.max(640, evento.data.height))}px`;
      break;
    case 'validasoft:visible':
      if (typeof evento.data.top === 'number' && typeof evento.data.height === 'number') {
        registrar('entrante', `validasoft:visible (top ${String(evento.data.top)}, alto ${String(evento.data.height)})`);
        mostrarRegion(iframe, evento.data.top, evento.data.height);
      }
      break;
    default:
      registrar('entrante', tipo);
  }
});

botonAbrir.addEventListener('click', abrir);
botonRenovar.addEventListener('click', () => void entregarSesion());
botonCerrar.addEventListener('click', () => enviar({ tipo: 'validasoft:cerrar-sesion' }, 'validasoft:cerrar-sesion'));
tema.addEventListener('change', enviarTema);
persona.addEventListener('change', () => {
  if (iframe) void entregarSesion();
});

void cargarSemilla().then((datos) => {
  semilla = datos;
  if (!datos) {
    elemento('sin-semilla').hidden = false;
    botonAbrir.disabled = true;
    return;
  }
  persona.replaceChildren(...datos.usuarios.map((u) => new Option(`${u.nombre} — ${u.descripcion}`, u.sub)));
  empresa.replaceChildren(new Option('(la que elija la persona)', ''), ...datos.empresas.map((e) => new Option(e.nombre, e.id)));
});
