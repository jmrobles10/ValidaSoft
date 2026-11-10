import { Button, TextField } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { BarraFiltros, FiltroMenu } from '@validasoft/ui';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { usePermisos } from '../../shared/usePermisos';
import { useAreas } from '../configuracion/areas/api';
import { useFlujos } from '../flujos/api';
import { useTiposVinculo } from '../vinculos/api';
import { CAMPOS_FECHA, ESTADOS_EXPEDIENTE, hayFiltrosEstado, rangoInvalido, TIPOS_EXPEDIENTE, type FiltrosEstado } from './modelo';

type Clave = 'estado' | 'tipo' | 'areaId' | 'flujoId' | 'tipoVinculoId' | 'fecha' | 'desde' | 'hasta' | 'terceroId';

/** Filtros del informe de estado: todo vive en la URL (se comparte el enlace y la descarga usa los mismos). */
export function FiltrosEstadoExpedientes({ empresaId, filtros }: { empresaId: string; filtros: FiltrosEstado }) {
  const { t } = useTranslation();
  const [, setParametros] = useSearchParams();
  const permisos = usePermisos(empresaId);
  const areas = useAreas(empresaId, permisos.puede(SCOPES.configuracionLeer));
  const flujos = useFlujos(empresaId, undefined, permisos.puede(SCOPES.flujosLeer));
  const tipos = useTiposVinculo(empresaId, permisos.puede(SCOPES.configuracionLeer));
  const errorRango = rangoInvalido(filtros);

  const cambiar = (clave: Clave, valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor && !(clave === 'fecha' && valor === 'Apertura')) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );

  const campoFecha = (clave: 'desde' | 'hasta') => (
    <TextField
      type="date"
      size="small"
      label={t(`informes.estado.filtros.${clave}`)}
      value={filtros[clave]}
      onChange={(e) => cambiar(clave, e.target.value)}
      error={errorRango}
      helperText={errorRango && clave === 'hasta' ? t('informes.estado.filtros.rangoInvalido') : undefined}
      slotProps={{ inputLabel: { shrink: true } }}
      sx={{ width: 160 }}
    />
  );

  return (
    <BarraFiltros>
      <FiltroMenu
        etiqueta={t('informes.estado.filtros.estado')}
        textoTodos={t('comun.todos')}
        valor={filtros.estado}
        onCambiar={(v) => cambiar('estado', v)}
        opciones={ESTADOS_EXPEDIENTE.map((e) => ({ valor: e, etiqueta: t(`informes.estado.estados.${e}`) }))}
      />
      <FiltroMenu
        etiqueta={t('informes.estado.filtros.tipo')}
        textoTodos={t('comun.todos')}
        valor={filtros.tipo}
        onCambiar={(v) => cambiar('tipo', v)}
        opciones={TIPOS_EXPEDIENTE.map((e) => ({ valor: e, etiqueta: t(`informes.estado.tipos.${e}`) }))}
      />
      {areas.data && (
        <FiltroMenu
          etiqueta={t('informes.estado.filtros.area')}
          textoTodos={t('comun.todas')}
          valor={filtros.areaId}
          onCambiar={(v) => cambiar('areaId', v)}
          opciones={areas.data.map((a) => ({ valor: a.id, etiqueta: a.nombre }))}
        />
      )}
      {tipos.data && (
        <FiltroMenu
          etiqueta={t('informes.estado.filtros.tipoVinculo')}
          textoTodos={t('comun.todos')}
          valor={filtros.tipoVinculoId}
          onCambiar={(v) => cambiar('tipoVinculoId', v)}
          opciones={tipos.data.map((tv) => ({ valor: tv.id, etiqueta: tv.nombre }))}
        />
      )}
      {flujos.data && (
        <FiltroMenu
          etiqueta={t('informes.estado.filtros.flujo')}
          textoTodos={t('comun.todos')}
          valor={filtros.flujoId}
          onCambiar={(v) => cambiar('flujoId', v)}
          opciones={flujos.data.map((f) => ({ valor: f.id, etiqueta: f.nombre }))}
        />
      )}
      <FiltroMenu
        etiqueta={t('informes.estado.filtros.fecha')}
        textoTodos={t('informes.estado.filtros.fechas.Apertura')}
        valor={filtros.fecha === 'Cierre' ? 'Cierre' : ''}
        onCambiar={(v) => cambiar('fecha', v)}
        opciones={CAMPOS_FECHA.filter((c) => c !== 'Apertura').map((c) => ({ valor: c, etiqueta: t(`informes.estado.filtros.fechas.${c}`) }))}
      />
      {campoFecha('desde')}
      {campoFecha('hasta')}
      {filtros.terceroId && (
        <Button variant="outlined" size="small" endIcon={<X size={14} aria-hidden />} onClick={() => cambiar('terceroId', '')}>
          {t('informes.estado.filtros.tercero')}
        </Button>
      )}
      {hayFiltrosEstado(filtros) && (
        <Button variant="text" onClick={() => setParametros(new URLSearchParams(), { replace: true })}>
          {t('informes.limpiar')}
        </Button>
      )}
    </BarraFiltros>
  );
}
