import { useTranslation } from 'react-i18next';
import { EncabezadoPagina } from '../../shared/componentes';
import { ListaDeSolicitudesHabeas } from '../habeas/ListaDeSolicitudesHabeas';

/**
 * Habeas data de la plataforma (Ley 1581, OD-1, OD-5, OD-6): las solicitudes que los terceros le dirigen a ella, que responde por la
 * bóveda global (documentos, ficha y cuentas). Al atender una supresión se ejecuta el plan: lo que ninguna aceptación ni certificado
 * referencia se elimina; lo referenciado se conserva íntegro con su base legal.
 */
export function PaginaHabeasPlataforma() {
  const { t } = useTranslation();
  return (
    <>
      <EncabezadoPagina titulo={t('habeas.titulo')} subtitulo={t('habeas.subtituloPlataforma')} />
      <ListaDeSolicitudesHabeas origen={{ tipo: 'plataforma' }} />
    </>
  );
}
