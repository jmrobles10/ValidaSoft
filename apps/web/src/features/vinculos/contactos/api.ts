import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarContactosVinculo, type ContactoDeEmpresaDto } from '@validasoft/api-client';
import { invalidarRuta } from '../../../shared/consultas';

/** Reemplaza los contactos del tercero con la empresa (lista completa); al guardar se recarga el vínculo y su historial. */
export function useActualizarContactos(empresaId: string, vinculoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (contactos: ContactoDeEmpresaDto[]) => actualizarContactosVinculo(vinculoId, { contactos }, { empresa: empresaId }),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/vinculos'),
  });
}
