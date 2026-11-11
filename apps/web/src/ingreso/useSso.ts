import { useMutation, useQuery } from '@tanstack/react-query';
import { descubrirSsoIngreso, iniciarSsoIngreso, type SsoDescubiertoDto } from '@validasoft/api-client';
import { useEffect, useState } from 'react';
import { esCorreoPlausible } from './estado';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;
const ESPERA_DEL_CORREO_MS = 450;

/** Lleva el navegador a una dirección (el proveedor de identidad). Aparte para poder probarlo y para no aceptar nada que no sea http(s). */
export function navegarA(url: string): void {
  const destino = new URL(url);
  if (destino.protocol !== 'https:' && destino.protocol !== 'http:') throw new Error('El destino del inicio de sesión no es una dirección web.');
  window.location.assign(destino.toString());
}

/** El correo ya escrito de corrido: se espera a que deje de teclear para no consultar por cada letra. */
function useCorreoEstable(correo: string): string {
  const [estable, setEstable] = useState(correo);
  useEffect(() => {
    const espera = window.setTimeout(() => setEstable(correo), ESPERA_DEL_CORREO_MS);
    return () => window.clearTimeout(espera);
  }, [correo]);
  return estable;
}

/**
 * Si el dominio del correo tiene inicio de sesión único de su empresa (PI-1), cuál es su nombre y si reemplaza al código. Sin un correo plausible o sin
 * SSO devuelve `null`: la persona sigue con el código o la passkey como siempre.
 */
export function useSsoDelCorreo(correo: string): SsoDescubiertoDto | null {
  const estable = useCorreoEstable(correo.trim());
  const consulta = useQuery({
    queryKey: ['/api/v1/ingreso/sso/descubrir', estable.toLowerCase()],
    queryFn: () => descubrirSsoIngreso({ correo: estable }, SIN_CREDENCIAL),
    enabled: esCorreoPlausible(estable),
    staleTime: 60_000,
    retry: false,
  });
  // Mientras se teclea otro correo, el resultado del anterior no vale.
  return esCorreoPlausible(correo) && estable === correo.trim() && consulta.data?.disponible ? consulta.data : null;
}

/** Arranca el inicio de sesión de la empresa: pide la dirección de autorización al servidor (que guarda el estado) y lleva al navegador allí. */
export function useIniciarSso(correo: string) {
  return useMutation({
    mutationFn: () => iniciarSsoIngreso({ correo: correo.trim() }, SIN_CREDENCIAL),
    onSuccess: (respuesta) => navegarA(respuesta.urlDeAutorizacion),
  });
}
