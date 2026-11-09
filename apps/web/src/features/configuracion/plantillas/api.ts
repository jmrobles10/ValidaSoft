import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  getListarPlantillasNotificacionQueryOptions,
  previsualizarPlantillaNotificacion,
  type ActualizarPlantillaNotificacionDto,
} from '@validasoft/api-client';
import { useEffect, useState } from 'react';
import { conEmpresa } from '../../../shared/consultas';
import { cuerpoDe, type BorradorPlantilla } from './modelo';

/** Espera de la vista previa mientras se escribe (PL-1). */
export const ESPERA_VISTA_PREVIA = 500;

/** Catálogo de correos personalizables con lo guardado por la empresa activa (`GET /plantillas-notificacion`). */
export function usePlantillas(empresaId: string, habilitado: boolean) {
  const opciones = getListarPlantillasNotificacionQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

/** El valor después de `espera` ms sin cambios (con un valor primitivo: comparar objetos reinicia la espera). */
export function useDiferido<T>(valor: T, espera: number): T {
  const [diferido, setDiferido] = useState(valor);
  useEffect(() => {
    const temporizador = setTimeout(() => setDiferido(valor), espera);
    return () => clearTimeout(temporizador);
  }, [valor, espera]);
  return diferido;
}

/**
 * Vista previa de lo que se está editando, sin guardar (`POST …/vista-previa`): se pide 500 ms después de la última
 * tecla y conserva la anterior mientras llega la nueva. Un 422 (variable no permitida, asunto muy largo) no se reintenta.
 */
export function useVistaPrevia(empresaId: string, evento: string, borrador: BorradorPlantilla) {
  // Se difiere el texto (no el objeto, que cambia en cada render) para no reprogramar el temporizador sin fin.
  const clave = useDiferido(JSON.stringify(cuerpoDe(borrador)), ESPERA_VISTA_PREVIA);
  const cuerpo = JSON.parse(clave) as ActualizarPlantillaNotificacionDto;
  return useQuery({
    queryKey: ['/api/v1/plantillas-notificacion/vista-previa', evento, cuerpo, { empresa: empresaId }],
    queryFn: ({ signal }) => previsualizarPlantillaNotificacion(evento, 'correo', cuerpo, { empresa: empresaId }, signal),
    placeholderData: keepPreviousData,
    retry: false,
    staleTime: 60_000,
  });
}
