import { useEffect, useState } from 'react';

/** La hora actual, refrescada cada `intervalo` ms: para que «hace 2 minutos» avance con el panel abierto. */
export function useAhora(intervalo = 60_000): number {
  const [ahora, setAhora] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setAhora(Date.now()), intervalo);
    return () => clearInterval(id);
  }, [intervalo]);
  return ahora;
}
