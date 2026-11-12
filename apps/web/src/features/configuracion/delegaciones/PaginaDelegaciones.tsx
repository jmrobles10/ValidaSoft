import { Box, Button, Chip, Stack, Table, TableBody, TableCell, TableHead, TableRow, Tooltip, Typography } from '@mui/material';
import { SCOPES, type DelegacionDto, type EstadoDeDelegacion } from '@validasoft/api-client';
import { BarraFiltros, ChipEstado, EsqueletoTabla, EstadoVacio, FiltroMenu, formatearFecha, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { ArrowRight, UserRoundCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useSesion } from '../../../sesion/sesionStore';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useAreas } from '../areas/api';
import { useDelegaciones, useRevocarDelegacion } from './api';
import { DialogoDelegar } from './DialogoDelegar';
import { CHIP_DELEGACION, ESTADOS_DELEGACION, formatearDia, puedeRevocar } from './modelo';

const MAXIMO_AREAS_VISIBLES = 3;

function esEstado(valor: string): valor is EstadoDeDelegacion {
  return (ESTADOS_DELEGACION as readonly string[]).includes(valor);
}

/**
 * Delegaciones de revisión (OP-7…OP-9): quién revisa en lugar de quién, qué áreas y entre qué fechas. Cada persona ve las
 * suyas (las que dio y las que recibió); quien administra expedientes ve todas y puede delegar por otra persona.
 */
export function PaginaDelegaciones() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.expedientesRevisar);
  const esAdministrador = permisos.puede(SCOPES.expedientesAdministrar);
  const yoId = useSesion((s) => s.sesion?.usuarioId ?? '');
  const [parametros, setParametros] = useSearchParams();
  const filtro = parametros.get('estado') ?? '';
  const estado = esEstado(filtro) ? filtro : '';
  const consulta = useDelegaciones(empresa.id, estado ? { estado } : {}, puede);
  const areas = useAreas(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const [delegando, setDelegando] = useState(false);
  const [revocando, setRevocando] = useState<DelegacionDto | null>(null);

  if (!puede) return <SinPermiso seccion={t('nav.delegaciones')} />;

  const delegaciones = consulta.data ?? [];
  const cambiarEstado = (valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set('estado', valor);
        else nuevos.delete('estado');
        return nuevos;
      },
      { replace: true },
    );
  const nombre = (id: string, nombreCompleto: string) => (id === yoId ? t('delegaciones.tu') : nombreCompleto);

  return (
    <>
      <EncabezadoPagina
        titulo={t('delegaciones.titulo')}
        subtitulo={esAdministrador ? t('delegaciones.subtituloAdministrador', { empresa: empresa.nombre }) : t('delegaciones.subtitulo')}
        acciones={
          <Button variant="contained" startIcon={<UserRoundCheck size={16} aria-hidden />} onClick={() => setDelegando(true)}>
            {t('delegaciones.delegar')}
          </Button>
        }
      />
      <BarraFiltros>
        <FiltroMenu
          etiqueta={t('delegaciones.filtros.estado')}
          textoTodos={t('comun.todos')}
          valor={estado}
          onCambiar={cambiarEstado}
          opciones={ESTADOS_DELEGACION.map((e) => ({ valor: e, etiqueta: t(`delegaciones.estados.${e}`), contenido: <EstadoDeLaDelegacion estado={e} /> }))}
        />
        {estado && (
          <Button variant="text" onClick={() => cambiarEstado('')}>
            {t('bandeja.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>

      {consulta.isError && <ErrorRecuperable mensaje={`${t('delegaciones.errorLista')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && <EsqueletoTabla etiqueta={t('delegaciones.titulo')} columnas={5} filas={4} />}
      {consulta.data && (
        <MarcoTabla>
          {delegaciones.length === 0 ? (
            <EstadoVacio
              compacto
              icono={UserRoundCheck}
              titulo={estado ? t('delegaciones.vacioFiltro') : t('delegaciones.vacio')}
              texto={estado ? undefined : t('delegaciones.vacioAyuda')}
            />
          ) : (
            <Table size="small" aria-label={t('delegaciones.titulo')}>
              <TableHead>
                <TableRow>
                  <TableCell>{t('delegaciones.columnas.personas')}</TableCell>
                  <TableCell>{t('delegaciones.columnas.areas')}</TableCell>
                  <TableCell sx={{ width: 190 }}>{t('delegaciones.columnas.fechas')}</TableCell>
                  <TableCell sx={{ width: 150 }}>{t('delegaciones.columnas.estado')}</TableCell>
                  <TableCell sx={{ width: 110 }}>
                    <Typography component="span" sx={visualmenteOculto}>
                      {t('delegaciones.columnas.acciones')}
                    </Typography>
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {delegaciones.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell>
                      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', fontWeight: 500 }}>
                        <span>{nombre(d.deUsuarioId, d.de)}</span>
                        <ArrowRight size={14} strokeWidth={1.75} aria-hidden />
                        <Box component="span" sx={visualmenteOculto}>
                          {' a '}
                        </Box>
                        <span>{nombre(d.aUsuarioId, d.a)}</span>
                      </Stack>
                      {d.motivo && (
                        <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
                          {t('delegaciones.motivo', { motivo: d.motivo })}
                        </Typography>
                      )}
                      {d.estado === 'Revocada' && d.motivoDeRevocacion && (
                        <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
                          {t('delegaciones.motivoRevocacion', { motivo: d.motivoDeRevocacion })}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <AreasDeLaDelegacion delegacion={d} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="monoTabla">{t('delegaciones.fechas', { desde: formatearDia(d.desde), hasta: formatearDia(d.hasta) })}</Typography>
                    </TableCell>
                    <TableCell>
                      <Stack spacing={0.25} sx={{ alignItems: 'flex-start' }}>
                        <EstadoDeLaDelegacion estado={d.estado} />
                        {d.estado === 'Revocada' && d.revocadaEn && (
                          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                            {t('delegaciones.revocadaEl', { fecha: formatearFecha(d.revocadaEn) })}
                          </Typography>
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell align="right">
                      {puedeRevocar(d) && (
                        <Button
                          variant="text"
                          color="error"
                          aria-label={t('delegaciones.revocarA', { de: d.de, a: d.a })}
                          onClick={() => setRevocando(d)}
                        >
                          {t('delegaciones.revocar')}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </MarcoTabla>
      )}

      {delegando && <DialogoDelegar empresaId={empresa.id} yoId={yoId} esAdministrador={esAdministrador} areas={areas.isError ? [] : areas.data} onCerrar={() => setDelegando(false)} />}
      {revocando && <DialogoRevocar empresaId={empresa.id} delegacion={revocando} onCerrar={() => setRevocando(null)} />}
    </>
  );
}

function EstadoDeLaDelegacion({ estado }: { estado: EstadoDeDelegacion }) {
  const { t } = useTranslation();
  return <ChipEstado estado={CHIP_DELEGACION[estado]} etiqueta={t(`delegaciones.estados.${estado}`)} />;
}

/** «Todas sus áreas» o hasta tres áreas con sus nombres (el resto, en un `+N` con la lista completa al pasar el cursor). */
function AreasDeLaDelegacion({ delegacion }: { delegacion: DelegacionDto }) {
  const { t } = useTranslation();
  const nombres = delegacion.areas.map((a) => a.nombre);
  if (delegacion.todasLasAreas) {
    return (
      <Tooltip title={nombres.join(', ')} disableHoverListener={nombres.length === 0}>
        <Chip size="small" variant="outlined" label={t('delegaciones.todasLasAreas')} />
      </Tooltip>
    );
  }
  const visibles = delegacion.areas.slice(0, MAXIMO_AREAS_VISIBLES);
  const resto = delegacion.areas.length - visibles.length;
  return (
    <Stack direction="row" useFlexGap spacing={0.5} sx={{ flexWrap: 'wrap' }}>
      {visibles.map((a) => (
        <Chip key={a.areaId} size="small" variant="outlined" label={a.nombre} />
      ))}
      {resto > 0 && (
        <Tooltip title={nombres.slice(MAXIMO_AREAS_VISIBLES).join(', ')}>
          <Chip size="small" variant="outlined" label={t('delegaciones.masAreas', { count: resto })} />
        </Tooltip>
      )}
    </Stack>
  );
}

function DialogoRevocar({ empresaId, delegacion, onCerrar }: { empresaId: string; delegacion: DelegacionDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const revocar = useRevocarDelegacion(empresaId);
  return (
    <DialogoConfirmacion
      titulo={t('delegaciones.revocacion.titulo')}
      subtitulo={t('delegaciones.deA', { de: delegacion.de, a: delegacion.a })}
      mensaje={
        <>
          {t('delegaciones.revocacion.mensaje', { de: delegacion.de, a: delegacion.a })}
          {revocar.isError && (
            <Typography component="span" role="alert" sx={{ color: 'error.main', display: 'block', mt: 1 }}>
              {mensajeDeError(revocar.error, t)}
            </Typography>
          )}
        </>
      }
      confirmar={t('delegaciones.revocacion.confirmar')}
      ocupado={revocar.isPending}
      onConfirmar={() =>
        revocar.mutate(
          { id: delegacion.id },
          {
            onSuccess: () => {
              mostrar(t('delegaciones.revocacion.exito'));
              onCerrar();
            },
          },
        )
      }
      onCerrar={onCerrar}
    />
  );
}
