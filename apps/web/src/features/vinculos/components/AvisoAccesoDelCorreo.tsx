import { Alert, AlertTitle } from '@mui/material';
import type { VinculoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { CircleCheck, CircleSlash, ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const POR_ESTADO = {
  Pendiente: { severidad: 'warning', icono: ShieldAlert },
  Aprobado: { severidad: 'success', icono: CircleCheck },
  Rechazado: { severidad: 'error', icono: CircleSlash },
} as const;

/**
 * Acceso aislado: si el correo que invitó la empresa no es contacto conocido de un tercero que ya existía, dice en qué va la decisión de los contactos del tercero. Pendiente:
 * nada de la bóveda ni de la información del tercero se comparte con la empresa y lo que ese correo suba queda solo en este vínculo. Rechazado: se invita a otro correo.
 * Un vínculo cuyo correo es de confianza no lleva aviso.
 */
export function AvisoAccesoDelCorreo({ vinculo }: { vinculo: Pick<VinculoDto, 'accesoDelCorreo'> }) {
  const { t } = useTranslation();
  const acceso = vinculo.accesoDelCorreo;
  if (!acceso) return null;
  const estado = acceso.estado === 'Aprobado' ? 'Aprobado' : acceso.estado === 'Rechazado' ? 'Rechazado' : 'Pendiente';
  const { severidad, icono: Icono } = POR_ESTADO[estado];
  const fecha = formatearFecha(estado === 'Pendiente' ? acceso.desde : (acceso.resueltoEn ?? acceso.desde));
  // Un correo que sí era contacto, pero sin verificar, y perdió el acceso completo porque otro contacto del tercero se verificó (decisión AV).
  const clave = estado === 'Pendiente' && acceso.motivo === 'ContactoDesplazado' ? 'Desplazado' : estado;
  return (
    <Alert severity={severidad} icon={<Icono size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
      <AlertTitle>{t(`vinculos.accesoDelCorreo.${clave}.titulo`, { correo: acceso.correo })}</AlertTitle>
      {t(`vinculos.accesoDelCorreo.${clave}.texto`, { correo: acceso.correo, fecha })}
    </Alert>
  );
}
