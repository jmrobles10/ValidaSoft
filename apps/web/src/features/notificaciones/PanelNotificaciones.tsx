import type { BandejaNotificacionesDto, NotificacionDto } from '@validasoft/api-client';
import { PanelDeNotificaciones } from '@validasoft/ui';
import { CheckCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';

export interface PanelNotificacionesProps {
  bandeja: BandejaNotificacionesDto | undefined;
  cargando: boolean;
  error: string | null;
  /** El stream no está disponible y la lista se refresca cada tanto. */
  respaldo: boolean;
  marcandoTodas: boolean;
  onReintentar: () => void;
  onAbrir: (notificacion: NotificacionDto) => void;
  onMarcarTodas: () => void;
  onPreferencias: () => void;
}

/** Panel de la campana de la web de empresa: el del paquete compartido con sus textos, el aviso de «sin conexión» y el ícono de «Marcar todas». */
export function PanelNotificaciones({ marcandoTodas, ...resto }: PanelNotificacionesProps) {
  const { t } = useTranslation();
  return (
    <PanelDeNotificaciones
      {...resto}
      marcandoTodas={marcandoTodas}
      iconoMarcarTodas={marcandoTodas ? <IconoCarga /> : <CheckCheck size={16} strokeWidth={1.75} aria-hidden />}
      alturaEsqueleto={48}
      textos={{
        titulo: t('notificaciones.titulo'),
        marcarTodas: t('notificaciones.marcarTodas'),
        preferencias: t('notificaciones.preferencias.abrir'),
        cargando: t('comun.cargando'),
        reintentar: t('comun.reintentar'),
        vacio: t('notificaciones.vacio'),
        vacioAyuda: t('notificaciones.vacioAyuda'),
        hayMas: t('notificaciones.hayMas'),
        sinConexion: t('notificaciones.sinConexion'),
        sinLeer: t('notificaciones.sinLeer'),
      }}
    />
  );
}
