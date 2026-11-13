// Estructura común de las páginas del sitio del manual: barra superior con buscador, columna lateral y pie.
import { escapar } from './comun.mjs';

const ICONO_BUSCAR =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>';
const ICONO_TEMA =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 3v18a9 9 0 0 0 0-18Z" fill="currentColor"/></svg>';
const ICONO_MENU =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M4 7h16M4 12h16M4 17h16"/></svg>';

/**
 * Página completa. `raiz` es el prefijo relativo hasta la raíz del sitio (`./`, `../`, `../../`) y `seccion`, la pestaña activa de la barra
 * (`recorridos` o `api`). `lateral` y `contenido` son HTML ya construido.
 */
export function pagina({ titulo, descripcion = '', raiz, seccion, lateral, migas = [], contenido, generado }) {
  const rastro =
    migas.length > 0
      ? `<nav aria-label="Ruta de navegación"><ol class="migas">${migas
          .map((m, i) => (m.href && i < migas.length - 1 ? `<li><a href="${escapar(m.href)}">${escapar(m.texto)}</a></li>` : `<li>${escapar(m.texto)}</li>`))
          .join('')}</ol></nav>`
      : '';
  return `<!doctype html>
<html lang="es-CO">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escapar(titulo)} · Manual de Validasoft</title>
${descripcion ? `<meta name="description" content="${escapar(descripcion)}">` : ''}
<link rel="stylesheet" href="${raiz}assets/manual.css">
<script>try{var t=localStorage.getItem('manual-tema');if(t==='claro'||t==='oscuro')document.documentElement.setAttribute('data-tema',t)}catch(e){}</script>
</head>
<body data-raiz="${raiz}">
<a class="saltar" href="#contenido">Saltar al contenido</a>
<header class="barra">
  <button class="boton-icono boton-menu" id="abrir-menu" type="button" aria-label="Abrir el menú" aria-expanded="false">${ICONO_MENU}</button>
  <a class="marca" href="${raiz}index.html"><span class="marca-sello" aria-hidden="true">V</span><span>Validasoft <small>Manual</small></span></a>
  <nav class="barra-enlaces" aria-label="Secciones del manual">
    <a href="${raiz}index.html"${seccion === 'recorridos' ? ' aria-current="page"' : ''}>Recorridos</a>
    <a href="${raiz}api/index.html"${seccion === 'api' ? ' aria-current="page"' : ''}>API</a>
  </nav>
  <div class="buscador" role="search">
    ${ICONO_BUSCAR}
    <label class="solo-lectores" for="buscar">Buscar en el manual</label>
    <input id="buscar" type="search" placeholder="Buscar en el manual y la API  ( / )" autocomplete="off" aria-controls="resultados">
    <div class="resultados" id="resultados" role="listbox" hidden></div>
  </div>
  <button class="boton-icono" id="cambiar-tema" type="button" aria-label="Cambiar entre modo claro y oscuro">${ICONO_TEMA}</button>
</header>
<div class="disposicion">
  <aside class="lateral" aria-label="Navegación">${lateral}</aside>
  <main class="contenido" id="contenido">
    ${rastro}
    ${contenido}
    <p class="pie">Manual generado${generado ? ` el ${escapar(generado)}` : ''} a partir de recorridos reales de la aplicación (Playwright) y del contrato OpenAPI. No se edita a mano: si el producto cambia, se vuelve a recorrer.</p>
  </main>
</div>
<dialog id="visor" aria-label="Pantallazo ampliado">
  <div class="visor-barra"><span></span><button type="button" class="boton-icono" data-cerrar aria-label="Cerrar">✕</button></div>
  <img alt="">
</dialog>
<script src="${raiz}assets/buscador.js" defer></script>
<script src="${raiz}assets/manual.js" defer></script>
</body>
</html>
`;
}
