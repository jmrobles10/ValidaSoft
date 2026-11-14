/** Lo mínimo de una descarga con URL prefirmada (la misma forma de `DescargaDto` del cliente de la API). */
export interface DescargaAbrible {
  url: string;
  nombre: string;
}

/**
 * Ofrece el archivo al navegador con un `<a download>` temporal. La URL prefirmada (5 min) trae `Content-Disposition: attachment`: el
 * navegador descarga el archivo y la pantalla se queda donde está.
 *
 * No usa `window.open`: se llama después de un `await` (pedir la URL a la API) y Safari en iPhone bloquea sin error lo que se abre
 * fuera del gesto del usuario. Un enlace sí se sigue, también en ese momento.
 */
export function abrirDescarga<T extends DescargaAbrible>(descarga: T): T {
  const enlace = document.createElement('a');
  enlace.href = descarga.url;
  enlace.rel = 'noopener';
  enlace.download = descarga.nombre;
  enlace.style.display = 'none';
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  return descarga;
}

/** Un archivo ya leído (lo que devuelve `descargarArchivo` del cliente de la API): sus bytes y el nombre que propuso el servidor. */
export interface ArchivoParaGuardar {
  blob: Blob;
  nombre?: string | null;
}

/** Ofrece el archivo que devolvió la API al navegador, con un `<a download>` temporal (el nombre es el que propuso la API, o el de reserva). */
export function guardarArchivo({ blob, nombre }: ArchivoParaGuardar, nombrePorDefecto: string): void {
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre ?? nombrePorDefecto;
  enlace.style.display = 'none';
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  // Se libera después del clic: algunos navegadores leen el `blob:` de forma asíncrona.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
