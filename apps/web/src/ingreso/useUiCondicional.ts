import { obtenerOpcionesPasskeyIngreso, verificarPasskeyIngreso, type PasoDeIngresoDto } from '@validasoft/api-client';
import { useEffect, useRef } from 'react';
import { esErrorDePasskey, firmarConPasskey, soportaPasskeysCondicional } from './passkeys';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;

/**
 * Passkeys en el campo del correo (WebAuthn condicional, PI-14): al llegar al primer paso se deja abierta una petición `mediation: 'conditional'` y el
 * navegador ofrece las passkeys guardadas dentro del autocompletado del campo (`autocomplete="username webauthn"`), sin ventana ni botón. Al elegir una se
 * verifica como cualquier ingreso con passkey. Se aborta al salir del paso o cuando la persona usa otro camino (una petición modal aborta la condicional).
 * Si el navegador no la admite, no hace nada: el botón «Entrar con una passkey» sigue ahí.
 */
export function useUiCondicionalDePasskeys(alResultado: (paso: PasoDeIngresoDto) => void, alFallar: (error: unknown) => void, activa = true): void {
  // Los callbacks cambian en cada render; la petición abierta debe llamar siempre al último sin reiniciarse.
  const resultado = useRef(alResultado);
  const fallo = useRef(alFallar);
  useEffect(() => {
    resultado.current = alResultado;
    fallo.current = alFallar;
  });

  useEffect(() => {
    if (!activa) return undefined;
    const control = new AbortController();
    const abortada = () => control.signal.aborted;

    void (async () => {
      if (!(await soportaPasskeysCondicional()) || abortada()) return;
      try {
        const { desafio, opciones } = await obtenerOpcionesPasskeyIngreso(SIN_CREDENCIAL);
        if (abortada()) return;
        const respuesta = await firmarConPasskey(opciones, control.signal, true);
        if (abortada()) return;
        const paso = await verificarPasskeyIngreso({ desafio, respuesta }, SIN_CREDENCIAL);
        if (!abortada()) resultado.current(paso);
      } catch (error) {
        // Abortar (salir del paso, usar el botón) o cerrar el selector no es un fallo que la persona deba ver.
        if (abortada() || (esErrorDePasskey(error) && error.causa === 'cancelada')) return;
        fallo.current(error);
      }
    })();

    return () => control.abort();
  }, [activa]);
}
