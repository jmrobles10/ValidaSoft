import { Button } from '@mui/material';
import { ClipboardList, Eye } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { DocumentoEnVisor } from '../../documentos/VisorDocumento';

export interface BotonVerVersionProps {
  documento: DocumentoEnVisor;
  /** `documentos:descargar`: sin él no se ofrece ver el archivo (los datos declarados ya vienen en el expediente). */
  puedeVerArchivo: boolean;
  variante?: 'text' | 'outlined';
  onVer: (documento: DocumentoEnVisor) => void;
}

/**
 * «Ver» abre el archivo de la versión en el visor. Una versión de solo formulario no tiene archivo (su descarga
 * responde 409 `version-sin-archivo`): en su lugar «Ver datos» abre el visor en modo «solo datos».
 */
export function BotonVerVersion({ documento, puedeVerArchivo, variante = 'outlined', onVer }: BotonVerVersionProps) {
  const { t } = useTranslation();
  const n = String(documento.n);
  if (documento.sinArchivo) {
    return (
      <Button
        size="small"
        variant={variante}
        startIcon={<ClipboardList size={14} aria-hidden />}
        onClick={() => onVer(documento)}
        aria-label={t('expedientes.datosDeclarados.verDatosAccesible', { documento: documento.tipo, n })}
      >
        {t('expedientes.datosDeclarados.verDatos')}
      </Button>
    );
  }
  if (!puedeVerArchivo) return null;
  return (
    <Button
      size="small"
      variant={variante}
      startIcon={<Eye size={variante === 'text' ? 14 : 16} aria-hidden />}
      onClick={() => onVer(documento)}
      aria-label={t('expedientes.requisitos.verAccesible', { documento: documento.tipo, n })}
    >
      {t('expedientes.requisitos.ver')}
    </Button>
  );
}
