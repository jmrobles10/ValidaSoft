import type { ArchivoDescargado } from '@validasoft/api-client';

/** Ofrece el archivo que devolvió la API al navegador, con un `<a download>` temporal (el nombre es el que propuso la API, o el de reserva). */
export function guardarArchivo({ blob, nombre }: ArchivoDescargado, nombrePorDefecto: string): void {
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
