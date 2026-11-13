import { Button } from '@mui/material';
import { FileDown } from 'lucide-react';
import { useContext } from 'react';
import { useTranslation } from 'react-i18next';
import { guardarArchivo } from '../../shared/archivos';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { DescargaDeDeclaracionDisponible, useDescargarDeclaracionPropia } from './descargaPdf';

interface Props {
  seccion: string;
  /** Posición de la persona en `personas` (solo en la información legal); `null` en la general. */
  persona: number | null;
  /** Nombre de la persona de la legal, para el nombre accesible del botón; sin él, es la declaración del propio tercero. */
  nombre?: string;
  variante?: 'outlined' | 'text';
}

/** «Descargar PDF» de una declaración PEP ya guardada. No se muestra si hay cambios sin guardar (ver `DescargaDeDeclaracionDisponible`). */
export function BotonDescargarDeclaracion({ seccion, persona, nombre, variante = 'outlined' }: Props) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const disponible = useContext(DescargaDeDeclaracionDisponible);
  const descarga = useDescargarDeclaracionPropia(seccion, persona);
  if (!disponible) return null;

  const descargar = () =>
    descarga.mutate(undefined, {
      onSuccess: (archivo) => guardarArchivo(archivo, t('ficha.pep.descarga.archivo')),
      onError: (error) => mostrar(`${t('ficha.pep.descarga.error')} ${mensajeDeError(error, t)}`, 'error'),
    });

  return (
    <Button
      variant={variante}
      startIcon={<FileDown size={16} aria-hidden />}
      onClick={descargar}
      disabled={descarga.isPending}
      aria-label={nombre ? t('ficha.pep.descarga.accesiblePersona', { nombre }) : t('ficha.pep.descarga.accesible')}
    >
      {t('ficha.pep.descarga.boton')}
    </Button>
  );
}
