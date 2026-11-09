import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';

/**
 * Texto de búsqueda que vive en la URL (`?q=`) con espera: el campo responde al instante y la URL (y con ella la
 * consulta) cambia cuando se deja de escribir `espera` ms. Si la URL cambia por otra vía (p. ej. «Limpiar filtros»),
 * el campo la sigue.
 */
export function useBusquedaEnUrl(espera: number, clave = 'q') {
  const [parametros, setParametros] = useSearchParams();
  const enUrl = parametros.get(clave) ?? '';
  const [texto, setTexto] = useState(enUrl);
  // Último valor que escribió este campo: al volver por la URL no debe pisar lo que se siguió escribiendo.
  const escrito = useRef(enUrl);

  useEffect(() => {
    if (enUrl === escrito.current) return;
    escrito.current = enUrl;
    setTexto(enUrl);
  }, [enUrl]);

  useEffect(() => {
    const valor = texto.trim();
    if (valor === escrito.current.trim()) return;
    const temporizador = setTimeout(() => {
      escrito.current = valor;
      setParametros(
        (previos) => {
          const nuevos = new URLSearchParams(previos);
          if (valor) nuevos.set(clave, valor);
          else nuevos.delete(clave);
          return nuevos;
        },
        { replace: true },
      );
    }, espera);
    return () => clearTimeout(temporizador);
  }, [texto, espera, clave, setParametros]);

  return { texto, setTexto };
}
