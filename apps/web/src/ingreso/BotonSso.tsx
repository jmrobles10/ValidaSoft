import { Button } from '@mui/material';
import { Building2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../shared/componentes';

interface BotonSsoProps {
  nombre: string;
  /** El botón principal cuando el SSO reemplaza al código; uno secundario si conviven. */
  principal: boolean;
  ocupado: boolean;
  cargando: boolean;
  alPulsar: () => void;
}

/** «Entrar con {empresa}»: el principal envía el formulario; el secundario inicia por su cuenta. */
export function BotonSso({ nombre, principal, ocupado, cargando, alPulsar }: BotonSsoProps) {
  const { t } = useTranslation();
  return (
    <Button
      type={principal ? 'submit' : 'button'}
      variant={principal ? 'contained' : 'outlined'}
      size={principal ? 'large' : 'medium'}
      fullWidth
      disabled={ocupado}
      onClick={principal ? undefined : alPulsar}
      startIcon={cargando ? <IconoCarga /> : <Building2 size={16} strokeWidth={1.75} aria-hidden />}
    >
      {t('ingreso.sso.entrarCon', { nombre })}
    </Button>
  );
}
