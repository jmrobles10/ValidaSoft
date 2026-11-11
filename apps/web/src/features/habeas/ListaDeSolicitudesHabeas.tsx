import { Alert, Box, Stack, Typography } from '@mui/material';
import type { SolicitudHabeasDto } from '@validasoft/api-client';
import { BarraFiltros, EsqueletoTarjetas, EstadoVacio, FiltroMenu, PaginacionCursor, usePaginacionCursor } from '@validasoft/ui';
import { ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useResumenDeSolicitudes, useSolicitudesHabeas, type OrigenDeSolicitudes } from './api';
import { DialogoResolverSolicitud } from './DialogoResolverSolicitud';
import { hayUrgentes, type FiltroDeSolicitudes } from './modelo';
import { TarjetaSolicitudHabeas } from './TarjetaSolicitudHabeas';

const OPCIONES: readonly FiltroDeSolicitudes[] = ['abiertas', 'vencidas', 'resueltas'];

/** Las cuatro cuentas de arriba: lo abierto, lo que vence pronto, lo vencido y lo resuelto. */
function Resumen({ origen }: { origen: OrigenDeSolicitudes }) {
  const { t } = useTranslation();
  const resumen = useResumenDeSolicitudes(origen);
  const datos = resumen.data;
  const cuentas = [
    ['abiertas', datos?.abiertas, false],
    ['porVencer', datos?.porVencer, (datos?.porVencer ?? 0) > 0],
    ['vencidas', datos?.vencidas, (datos?.vencidas ?? 0) > 0],
    ['resueltas', datos?.resueltas, false],
  ] as const;
  return (
    <Box
      component="ul"
      aria-label={t('habeas.resumen.titulo')}
      aria-busy={resumen.isPending}
      sx={{ m: 0, mb: 1.5, p: 0, listStyle: 'none', display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', md: 'repeat(4, minmax(0, 1fr))' }, gap: 1 }}
    >
      {cuentas.map(([clave, cantidad, urgente]) => (
        <Box key={clave} component="li" sx={(th) => ({ p: 1.25, border: 1, borderColor: urgente ? 'error.main' : 'divider', borderRadius: `${String(th.radio.md)}px`, bgcolor: 'background.paper' })}>
          <Typography sx={{ fontSize: 22, lineHeight: '28px', fontWeight: 600, color: urgente ? 'error.main' : 'text.primary', minHeight: 28 }}>
            {cantidad === undefined ? '' : cantidad.toLocaleString('es-CO')}
          </Typography>
          <Typography sx={{ fontSize: 13, lineHeight: '18px', color: 'text.secondary' }}>{t(`habeas.resumen.${clave}`)}</Typography>
        </Box>
      ))}
    </Box>
  );
}

/**
 * Las solicitudes de habeas data que atiende quien la abre (OD-1, OD-3): la empresa las de sus vínculos y la plataforma las de la bóveda
 * global. Resumen arriba, filtro por estado, las abiertas primero con la que vence antes arriba, y el diálogo para responder.
 */
export function ListaDeSolicitudesHabeas({ origen }: { origen: OrigenDeSolicitudes }) {
  const { t } = useTranslation();
  const [filtro, setFiltro] = useState<FiltroDeSolicitudes>('abiertas');
  const [respondiendo, setRespondiendo] = useState<SolicitudHabeasDto | null>(null);
  const paginacion = usePaginacionCursor();
  const consulta = useSolicitudesHabeas(origen, filtro, paginacion.cursor);
  const resumen = useResumenDeSolicitudes(origen);
  const pagina = consulta.data;

  const cambiarFiltro = (valor: string) => {
    setFiltro(valor === '' ? 'todas' : (valor as FiltroDeSolicitudes));
    paginacion.reiniciar();
  };

  return (
    <>
      {hayUrgentes(resumen.data) && (
        <Alert severity="warning" sx={{ mb: 1.5 }}>
          {t('habeas.resumen.urgentes', { porVencer: String(resumen.data?.porVencer ?? 0), vencidas: String(resumen.data?.vencidas ?? 0) })}
        </Alert>
      )}
      <Resumen origen={origen} />
      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('habeas.filtro.estado')}
          valor={filtro === 'todas' ? '' : filtro}
          opciones={OPCIONES.map((o) => ({ valor: o, etiqueta: t(`habeas.filtro.${o}`) }))}
          onCambiar={cambiarFiltro}
          textoTodos={t('habeas.filtro.todas')}
        />
      </BarraFiltros>

      {consulta.isError && !pagina ? (
        <ErrorRecuperable mensaje={`${t('habeas.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
      ) : !pagina ? (
        <EsqueletoTarjetas cantidad={3} lineas={3} etiqueta={t('habeas.cargando')} />
      ) : pagina.datos.length === 0 ? (
        <EstadoVacio icono={ShieldCheck} titulo={t(`habeas.vacio.${filtro}.titulo`)} texto={t(`habeas.vacio.${filtro}.texto`)} />
      ) : (
        <Stack component="ul" spacing={1.25} sx={{ m: 0, p: 0, listStyle: 'none', opacity: consulta.isPlaceholderData ? 0.6 : 1 }}>
          {pagina.datos.map((s) => (
            <li key={s.id}>
              <TarjetaSolicitudHabeas solicitud={s} onResponder={setRespondiendo} />
            </li>
          ))}
        </Stack>
      )}

      {pagina && (paginacion.hayAnterior || pagina.siguienteCursor) && (
        <PaginacionCursor
          pagina={paginacion.pagina}
          hayAnterior={paginacion.hayAnterior}
          haySiguiente={Boolean(pagina.siguienteCursor)}
          onAnterior={paginacion.irAAnterior}
          onSiguiente={() => pagina.siguienteCursor && paginacion.irASiguiente(pagina.siguienteCursor)}
          cargando={consulta.isFetching}
        />
      )}

      {respondiendo && <DialogoResolverSolicitud solicitud={respondiendo} origen={origen} onCerrar={() => setRespondiendo(null)} />}
    </>
  );
}
