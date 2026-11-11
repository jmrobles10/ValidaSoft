import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarAyudaDeDocumento, getListarAyudasDeDocumentosQueryOptions } from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../../shared/consultas';

/** Ayuda de cada tipo de documento: la del grupo, la de la empresa activa y la que ve el tercero (`GET /ayudas-de-documentos`). */
export function useAyudasDeDocumentos(empresaId: string, habilitado: boolean) {
  const opciones = getListarAyudasDeDocumentosQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', enabled: habilitado });
}

/** Guarda la ayuda de la empresa para un tipo; `texto: null` hace que la empresa vuelva a usar la del grupo. */
export function useGuardarAyudaDeDocumento(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ tipoDocumentoId, texto }: { tipoDocumentoId: string; texto: string | null }) =>
      actualizarAyudaDeDocumento(tipoDocumentoId, { texto }, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/ayudas-de-documentos'),
  });
}
