import type { BandejaNotificacionesTerceroDto, NotificacionTerceroDto } from '@validasoft/api-client';
import { Logo, PanelDeNotificaciones, type MarcaDeNotificacion } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { LogoEmpresa } from '../shared/componentes';

export interface PanelNotificacionesProps {
  bandeja: BandejaNotificacionesTerceroDto | undefined;
  cargando: boolean;
  error: string | null;
  onReintentar: () => void;
  onAbrir: (notificacion: NotificacionTerceroDto) => void;
  onMarcarTodas: () => void;
  onPreferencias: () => void;
}

/**
 * La marca de la empresa que originó el aviso (logo y, si la tiene, su color en el borde). Los que no son de ninguna empresa (una
 * propuesta desde un documento) llevan el ícono de Validasoft.
 */
function marcaDe({ empresa }: NotificacionTerceroDto): MarcaDeNotificacion {
  return {
    icono: empresa ? <LogoEmpresa nombre={empresa.nombre} logoUrl={empresa.logoUrl} tamano={32} /> : <Logo variante="icono" tamano={28} />,
    color: empresa?.colorMarca ?? null,
  };
}

/** Panel de la campana del tercero: el del paquete compartido con sus textos y la marca de la empresa de cada aviso. */
export function PanelNotificaciones(props: PanelNotificacionesProps) {
  const { t } = useTranslation();
  return (
    <PanelDeNotificaciones
      {...props}
      alturaEsqueleto={52}
      marcaDe={marcaDe}
      textos={{
        titulo: t('notificaciones.titulo'),
        marcarTodas: t('notificaciones.marcarTodas'),
        preferencias: t('notificaciones.preferencias.abrir'),
        cargando: t('comun.cargando'),
        reintentar: t('comun.reintentar'),
        vacio: t('notificaciones.vacio'),
        vacioAyuda: t('notificaciones.vacioAyuda'),
        hayMas: t('notificaciones.hayMas'),
        sinLeer: t('notificaciones.sinLeer'),
      }}
    />
  );
}
