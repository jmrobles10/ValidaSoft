import { useTranslation } from 'react-i18next';
import { useDescargarVersion } from '../shared/api';
import { useAvisos } from '../shared/useAvisos';
import { mensajeDeError } from '../shared/errores';

/** Descarga una versión de la bóveda con aviso de inicio o del error. */
export function useDescargar() {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const descarga = useDescargarVersion();
  return (documentoId: string, numero: number) =>
    descarga.mutate(
      { documentoId, numero },
      {
        onSuccess: (d) => mostrar(t('boveda.descargaLista', { nombre: d.nombre }), 'info'),
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      },
    );
}
