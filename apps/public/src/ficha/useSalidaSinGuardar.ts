import { useRef } from 'react';
import { useBeforeUnload, useBlocker } from 'react-router';

/**
 * Con el panel de secciones a la vista es fácil saltar a otra: si la sección tiene cambios sin guardar, la salida se
 * detiene y se pregunta (también al cerrar o recargar la pestaña). `permitirSalida` deja pasar la navegación que hace el
 * propio wizard tras guardar («Guardar y continuar»).
 */
export function useSalidaSinGuardar(sinGuardar: boolean) {
  const permitida = useRef(false);
  const bloqueo = useBlocker(
    ({ currentLocation, nextLocation }) =>
      sinGuardar && !permitida.current && (currentLocation.pathname !== nextLocation.pathname || currentLocation.search !== nextLocation.search),
  );
  useBeforeUnload((evento) => {
    if (sinGuardar && !permitida.current) evento.preventDefault();
  });
  return {
    bloqueo,
    permitirSalida: () => {
      permitida.current = true;
    },
  };
}
