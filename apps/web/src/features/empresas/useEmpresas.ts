import { getListarEmpresasQueryKey, useListarEmpresas, type EmpresaDto } from '@validasoft/api-client';
import type { EmpresaOpcion } from '@validasoft/ui';
import { useOutletContext } from 'react-router';
import { useSesion } from '../../sesion/sesionStore';

export function aOpcion(empresa: EmpresaDto): EmpresaOpcion {
  const { numero, digitoVerificacion } = empresa.identificacion;
  return {
    id: empresa.id,
    nombre: empresa.nombre,
    identificacion: digitoVerificacion ? `${numero}-${digitoVerificacion}` : numero,
    logoUrl: empresa.logoUrl,
    prefijo: empresa.prefijoFolio,
  };
}

/** Empresas que la sesión puede usar (la API ya filtra por el alcance de la credencial). */
export function useEmpresas() {
  const usuario = useSesion((s) => s.sesion?.usuarioId ?? null);
  return useListarEmpresas({
    query: { queryKey: [...getListarEmpresasQueryKey(), { usuario }], staleTime: 5 * 60_000, enabled: Boolean(usuario) },
    request: { empresa: null },
  });
}

/**
 * Elige la empresa inicial: la que sugirió el contenedor, la última que usó la persona o la primera de la lista.
 */
export function elegirEmpresaInicial(
  empresas: readonly Pick<EmpresaDto, 'id'>[],
  sugerida: string | null,
  ultima: string | undefined,
): string | null {
  const existe = (id: string | null | undefined): id is string => Boolean(id) && empresas.some((e) => e.id === id);
  if (existe(sugerida)) return sugerida;
  if (existe(ultima)) return ultima;
  return empresas[0]?.id ?? null;
}

export interface ContextoEmpresa {
  empresa: EmpresaDto;
  empresas: EmpresaDto[];
}

export function useEmpresaActiva(): ContextoEmpresa {
  return useOutletContext<ContextoEmpresa>();
}
