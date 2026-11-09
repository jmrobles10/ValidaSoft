import { Box, Button, Tab, Tabs, TextField, Typography } from '@mui/material';
import { BarraFiltros, CampoBusqueda, FiltroMenu } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { useBusquedaEnUrl } from '../../shared/useBusquedaEnUrl';
import { entidadesDe, ESPERA_TEXTO_MS, hayFiltros, rangoInvalido, textoEntidad, TIPO_POR_DEFECTO, TIPOS_AUDITORIA, type FiltrosAuditoria, type TipoAuditoria } from './modelo';

/** Al cambiar de pestaña se conservan actor y fechas; la entidad y su id son de cada tipo. */
const CONSERVAR_AL_CAMBIAR_TIPO = ['actor', 'desde', 'hasta'] as const;

/** Pestañas por tipo y barra de filtros de la auditoría; todo vive en la URL. */
export function FiltrosAuditoria({ filtros }: { filtros: FiltrosAuditoria }) {
  const { t } = useTranslation();
  const [, setParametros] = useSearchParams();
  const actor = useBusquedaEnUrl(ESPERA_TEXTO_MS, 'actor');
  const entidadId = useBusquedaEnUrl(ESPERA_TEXTO_MS, 'entidadId');
  const { tipo } = filtros;

  const cambiar = (clave: 'entidad' | 'desde' | 'hasta', valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );

  const cambiarTipo = (nuevo: TipoAuditoria) =>
    setParametros((previos) => {
      const nuevos = new URLSearchParams();
      if (nuevo !== TIPO_POR_DEFECTO) nuevos.set('tipo', nuevo);
      for (const clave of CONSERVAR_AL_CAMBIAR_TIPO) {
        const valor = previos.get(clave);
        if (valor) nuevos.set(clave, valor);
      }
      return nuevos;
    });

  const limpiar = () => setParametros(tipo === TIPO_POR_DEFECTO ? new URLSearchParams() : new URLSearchParams({ tipo }), { replace: true });
  const errorRango = rangoInvalido(filtros);
  const campoFecha = (clave: 'desde' | 'hasta') => (
    <TextField
      type="date"
      size="small"
      label={t(`auditoria.filtros.${clave}`)}
      value={filtros[clave]}
      onChange={(e) => cambiar(clave, e.target.value)}
      error={errorRango}
      slotProps={{ inputLabel: { shrink: true } }}
      sx={{ width: 170 }}
    />
  );

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', columnGap: 2, mb: 1.5, borderBottom: 1, borderColor: 'divider' }}>
        <Tabs value={tipo} onChange={(_, valor: TipoAuditoria) => cambiarTipo(valor)} aria-label={t('auditoria.pestanas.etiqueta')}>
          {TIPOS_AUDITORIA.map((tp) => (
            <Tab key={tp} value={tp} label={t(`auditoria.pestanas.${tp}`)} />
          ))}
        </Tabs>
        <Typography variant="caption" sx={{ color: 'text.secondary' }}>
          {t('auditoria.ayudaTipos')}
        </Typography>
      </Box>

      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('auditoria.filtros.entidad')}
          textoTodos={t('comun.todas')}
          valor={filtros.entidad}
          onCambiar={(valor) => cambiar('entidad', valor)}
          opciones={entidadesDe(tipo).map((e) => ({ valor: e, etiqueta: textoEntidad(e, t) }))}
        />
        <CampoBusqueda
          ancho={260}
          valor={entidadId.texto}
          onCambiar={entidadId.setTexto}
          placeholder={tipo === 'Acceso' ? t('auditoria.filtros.entidadIdAcceso') : t('auditoria.filtros.entidadId')}
        />
        <CampoBusqueda ancho={200} valor={actor.texto} onCambiar={actor.setTexto} placeholder={t('auditoria.filtros.actor')} />
        {campoFecha('desde')}
        {campoFecha('hasta')}
        {hayFiltros(filtros) && (
          <Button variant="text" onClick={limpiar}>
            {t('auditoria.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>
    </>
  );
}
