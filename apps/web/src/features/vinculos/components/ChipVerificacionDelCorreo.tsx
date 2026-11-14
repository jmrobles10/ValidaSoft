import { Chip, Tooltip } from '@mui/material';
import type { VerificacionDelCorreoDto } from '@validasoft/api-client';
import { ShieldAlert, ShieldCheck, ShieldQuestion } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const ICONO = { Verificado: ShieldCheck, SinVerificar: ShieldQuestion, NoEsContacto: ShieldAlert } as const;

/**
 * Si el correo que invitó la empresa está probado como del tercero (decisión AV): <b>verificado</b> (con qué: el correo del RUT o de la Cámara, dos empresas que lo invitaron
 * por su cuenta, la plataforma, la importación o un contacto verificado), <b>sin verificar</b> (es contacto del tercero, pero nada prueba aún que sea suyo: no decide los accesos
 * de otros correos) o <b>aún no es contacto</b> (acceso aislado). Se lee sin color: cada estado tiene su ícono y su texto.
 */
export function ChipVerificacionDelCorreo({ verificacion }: { verificacion: VerificacionDelCorreoDto | null | undefined }) {
  const { t } = useTranslation();
  if (!verificacion) return null;
  const clave = verificacion.estado === 'Verificado' ? 'Verificado' : verificacion.estado === 'SinVerificar' ? 'SinVerificar' : 'NoEsContacto';
  const Icono = ICONO[clave];
  const medio = clave === 'Verificado' && verificacion.medio ? t(`vinculos.verificacionDelCorreo.medios.${verificacion.medio}`, { defaultValue: verificacion.medio }) : null;
  return (
    <Tooltip title={t(`vinculos.verificacionDelCorreo.${clave}.ayuda`)} describeChild>
      <Chip
        size="small"
        variant="outlined"
        icon={<Icono size={14} strokeWidth={1.75} aria-hidden />}
        label={medio ? t('vinculos.verificacionDelCorreo.conMedio', { estado: t(`vinculos.verificacionDelCorreo.${clave}.etiqueta`), medio }) : t(`vinculos.verificacionDelCorreo.${clave}.etiqueta`)}
        sx={{ ml: 1, verticalAlign: 'middle' }}
      />
    </Tooltip>
  );
}
