import type { CodigosDeRecuperacionDto } from '@validasoft/api-client';

/** Los códigos en texto plano, uno por línea (para copiarlos al portapapeles o a un gestor de contraseñas). */
export function textoDeCodigos(codigos: readonly string[]): string {
  return codigos.join('\n');
}

function escaparHtml(texto: string): string {
  return texto.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/** El documento que se imprime: título, aviso y los códigos en monoespaciado, sin estilos externos ni nada que cargar. */
export function documentoParaImprimir(codigos: CodigosDeRecuperacionDto, titulo: string, ayuda: string): string {
  const filas = codigos.codigos.map((c) => `<li>${escaparHtml(c)}</li>`).join('');
  return `<!doctype html><html lang="es-CO"><head><meta charset="utf-8"><title>${escaparHtml(titulo)}</title><style>body{font-family:Arial,sans-serif;margin:32px;color:#111}h1{font-size:20px}p{font-size:13px;color:#444}ul{list-style:none;padding:0;columns:2;font-family:Consolas,monospace;font-size:16px;line-height:2}</style></head><body><h1>${escaparHtml(titulo)}</h1><p>${escaparHtml(ayuda)}</p><ul>${filas}</ul></body></html>`;
}

/**
 * Imprime los códigos desde un marco oculto con solo el documento de arriba (sin ventana emergente que el navegador pueda bloquear, y sin tocar la
 * pantalla de la persona). El marco se retira solo al terminar.
 */
export function imprimirCodigos(codigos: CodigosDeRecuperacionDto, titulo: string, ayuda: string): void {
  const marco = document.createElement('iframe');
  marco.setAttribute('aria-hidden', 'true');
  marco.title = titulo;
  marco.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden';
  marco.srcdoc = documentoParaImprimir(codigos, titulo, ayuda);
  marco.addEventListener('load', () => {
    marco.contentWindow?.focus();
    marco.contentWindow?.print();
    window.setTimeout(() => marco.remove(), 1500);
  });
  document.body.append(marco);
}
