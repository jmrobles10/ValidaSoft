import { useEffect, useState } from 'react';

/** El valor, un momento después de que deja de cambiar (para no consultar a la API en cada tecla). Usa valores primitivos. */
export function useDiferido<T extends string | number | boolean | null>(valor: T, ms: number): T {
  const [diferido, setDiferido] = useState(valor);
  useEffect(() => {
    const espera = setTimeout(() => setDiferido(valor), ms);
    return () => clearTimeout(espera);
  }, [valor, ms]);
  return diferido;
}
