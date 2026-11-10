import { useLocation } from 'react-router';

/** Dónde quedó el router (para comprobar si el wizard avanzó o se quedó en la sección). */
export function Ubicacion() {
  const { pathname, search } = useLocation();
  return <p data-testid="ubicacion">{`${pathname}${search}`}</p>;
}
