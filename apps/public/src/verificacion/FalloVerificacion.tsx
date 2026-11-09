import { Button } from '@mui/material';
import { PaginaEstado } from '@validasoft/ui';
import { CloudOff, Hourglass, RotateCcw, SearchX, ShieldQuestion, WifiOff, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { falloDeConsulta, falloReintentable, type FalloDeConsulta } from './estadoCertificado';

const ICONO: Record<FalloDeConsulta, LucideIcon> = {
  'certificado-no-encontrado': SearchX,
  'huella-invalida': ShieldQuestion,
  'folio-ambiguo': ShieldQuestion,
  'demasiadas-peticiones': Hourglass,
  red: WifiOff,
  'no-disponible': CloudOff,
};

/** El certificado no se pudo mostrar: qué pasó y qué hacer (reintentar solo si no es culpa del enlace). */
export function FalloVerificacion({ error, onReintentar }: { error: unknown; onReintentar: () => void }) {
  const { t } = useTranslation();
  const fallo = falloDeConsulta(error);
  return (
    <PaginaEstado
      icono={ICONO[fallo]}
      titulo={t(`verificacion.fallos.${fallo}.titulo`)}
      texto={t(`verificacion.fallos.${fallo}.texto`)}
      rol="alert"
      acciones={
        falloReintentable(fallo) ? (
          <Button variant="contained" startIcon={<RotateCcw size={18} aria-hidden />} onClick={onReintentar}>
            {t('comun.reintentar')}
          </Button>
        ) : undefined
      }
    />
  );
}
