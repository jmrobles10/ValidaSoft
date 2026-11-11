import { Button } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { BarraFiltros, CampoFecha, FiltroMenu } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { usePermisos } from '../../shared/usePermisos';
import { useAreas } from '../configuracion/areas/api';
import { useTiposVinculo } from '../vinculos/api';
import { rangoInvalido } from './modelo';
import { hayFiltrosSla, type FiltrosSla } from './modeloSla';

type Clave = keyof FiltrosSla;

/** Filtros del informe de cumplimiento del plazo de revisión: todo vive en la URL (el Excel lleva los mismos). */
export function FiltrosInformeSla({ empresaId, filtros }: { empresaId: string; filtros: FiltrosSla }) {
  const { t } = useTranslation();
  const [, setParametros] = useSearchParams();
  const permisos = usePermisos(empresaId);
  const areas = useAreas(empresaId, permisos.puede(SCOPES.configuracionLeer));
  const tipos = useTiposVinculo(empresaId, permisos.puede(SCOPES.configuracionLeer));
  const errorRango = rangoInvalido(filtros);

  const cambiar = (clave: Clave, valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );

  const campoFecha = (clave: 'desde' | 'hasta') => (
    <CampoFecha
      etiqueta={t(`informes.sla.filtros.${clave}`)}
      valor={filtros[clave]}
      onCambiar={(iso) => cambiar(clave, iso)}
      error={errorRango}
      ayuda={errorRango && clave === 'hasta' ? t('informes.sla.filtros.rangoInvalido') : undefined}
      sx={{ width: 160 }}
    />
  );

  return (
    <BarraFiltros>
      {areas.data && (
        <FiltroMenu
          etiqueta={t('informes.sla.filtros.area')}
          textoTodos={t('comun.todas')}
          valor={filtros.areaId}
          onCambiar={(v) => cambiar('areaId', v)}
          opciones={areas.data.map((a) => ({ valor: a.id, etiqueta: a.nombre }))}
        />
      )}
      {tipos.data && (
        <FiltroMenu
          etiqueta={t('informes.sla.filtros.tipoVinculo')}
          textoTodos={t('comun.todos')}
          valor={filtros.tipoVinculoId}
          onCambiar={(v) => cambiar('tipoVinculoId', v)}
          opciones={tipos.data.map((tv) => ({ valor: tv.id, etiqueta: tv.nombre }))}
        />
      )}
      {campoFecha('desde')}
      {campoFecha('hasta')}
      {hayFiltrosSla(filtros) && (
        <Button variant="text" onClick={() => setParametros(new URLSearchParams(), { replace: true })}>
          {t('informes.limpiar')}
        </Button>
      )}
    </BarraFiltros>
  );
}
