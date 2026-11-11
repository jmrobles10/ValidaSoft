import { Alert, Box, Button, Chip, Stack, Typography } from '@mui/material';
import type { CoincidenciaDto, DecisionCoincidencia, ExpedienteDto, VerificacionDto } from '@validasoft/api-client';
import { Dato, formatearFecha, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { FilePlus2, FileDown, RefreshCw } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionVerificacion, useDescargarEvidencia } from '../api';
import { fuenteManual } from '../consultaManual';
import { esSistema, type AccionesDeVerificacion } from '../modelo';
import { CoincidenciaDeListas } from './CoincidenciaDeListas';
import { ConsultasAnteriores } from './ConsultasAnteriores';
import { DialogoConsultaManual } from './DialogoConsultaManual';
import { DialogoResolverCoincidencia } from './DialogoResolverCoincidencia';
import { EncabezadoVerificacion } from './EncabezadoVerificacion';

export interface ListasRestrictivasProps {
  expediente: ExpedienteDto;
  empresaId: string;
  verificacion: VerificacionDto;
  acciones: AccionesDeVerificacion;
  nombres: ReadonlyMap<string, string>;
}

/** Listas restrictivas del expediente: estado, personas consultadas, coincidencias con su decisión, evidencia, reconsulta y consulta hecha por fuera. */
export function ListasRestrictivas({ expediente, empresaId, verificacion: v, acciones, nombres }: ListasRestrictivasProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const accion = useAccionVerificacion(empresaId, expediente.id);
  const descarga = useDescargarEvidencia(empresaId);
  const [resolver, setResolver] = useState<{ coincidencia: CoincidenciaDto; decision: DecisionCoincidencia } | null>(null);
  const [manual, setManual] = useState(false);
  const consultada = Boolean(v.registradaEn);
  const quien = (id: string | null | undefined) => (!id || esSistema(id) ? t('cumplimiento.panel.sistema') : (nombres.get(id) ?? id.slice(0, 8)));
  const fuente = fuenteManual(v.proveedor);

  const reconsultar = () =>
    accion.mutate({ tipo: 'consultar' }, { onSuccess: () => mostrar(t('cumplimiento.panel.consultado')), onError: (e) => mostrar(mensajeDeError(e, t), 'error') });
  const descargar = () => descarga.mutate({ expedienteId: expediente.id }, { onError: (e) => mostrar(mensajeDeError(e, t), 'error') });

  return (
    <Box component="section" aria-label={t('cumplimiento.tipos.ListasRestrictivas')}>
      <EncabezadoVerificacion
        verificacion={v}
        acciones={
          <>
            {acciones.descargar && v.evidencia && (
              <Button size="small" variant="text" startIcon={descarga.isPending ? <IconoCarga /> : <FileDown size={16} aria-hidden />} onClick={descargar} disabled={descarga.isPending}>
                {t('cumplimiento.panel.evidencia')}
              </Button>
            )}
            {acciones.consultaManual && (
              <Button size="small" variant="text" startIcon={<FilePlus2 size={16} aria-hidden />} onClick={() => {
                  accion.reset();
                  setManual(true);
                }}>
                {t('cumplimiento.manual.boton')}
              </Button>
            )}
            {acciones.consultar && (
              <Button size="small" variant="outlined" startIcon={accion.isPending ? <IconoCarga /> : <RefreshCw size={16} aria-hidden />} onClick={reconsultar} disabled={accion.isPending}>
                {consultada ? t('cumplimiento.panel.reconsultar') : t('cumplimiento.panel.consultar')}
              </Button>
            )}
          </>
        }
      />
      {v.ultimoError && (
        <Alert severity="warning" sx={{ mb: 1 }}>
          {t('cumplimiento.panel.ultimoError', { error: v.ultimoError, cuando: v.reintentarDesde ? formatearFechaHora(v.reintentarDesde) : '' })}
        </Alert>
      )}
      {!consultada && <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.panel.pendienteListas')}</Typography>}
      {v.estado === 'Expirada' && <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.panel.expiradaListas')}</Typography>}
      {v.registradaEn && (
        <Stack spacing={1.5}>
          <RejillaDatos columnas={3}>
            <Dato etiqueta={t('cumplimiento.panel.proveedor')}>
              <Stack direction="row" spacing={0.75} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <span>{v.proveedor ?? '—'}</span>
                {fuente !== null && <Chip size="small" variant="outlined" label={t('cumplimiento.panel.manual')} />}
              </Stack>
              {fuente !== null && (
                <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12, display: 'block' }}>
                  {t('cumplimiento.manual.delOficial', { quien: quien(v.registradaPor) })}
                </Typography>
              )}
            </Dato>
            <Dato etiqueta={t('cumplimiento.panel.consultada')}>{`${formatearFechaHora(v.registradaEn)} · ${quien(v.registradaPor)}`}</Dato>
            <Dato etiqueta={t('cumplimiento.panel.vigenteHasta')}>{v.vigenteHasta ? formatearFecha(v.vigenteHasta) : '—'}</Dato>
            <Dato etiqueta={t('cumplimiento.panel.personas')} ancho>
              {v.personas.map((p) => `${p.nombre} · ${t(`cumplimiento.roles.${p.rol as 'Tercero'}`, { defaultValue: p.rol })} · ${p.identificacion}`).join(' — ')}
            </Dato>
          </RejillaDatos>
          <Box>
            <Typography sx={{ fontSize: 13, fontWeight: 500, mb: 0.5 }}>{t('cumplimiento.panel.coincidencias')}</Typography>
            {v.coincidencias.length === 0 ? (
              <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.panel.sinCoincidencias')}</Typography>
            ) : (
              <Stack component="ul" spacing={1} sx={{ listStyle: 'none', p: 0, m: 0 }}>
                {v.coincidencias.map((c) => (
                  <CoincidenciaDeListas key={c.id} coincidencia={c} puedeDecidir={acciones.decidir} quien={quien} onResolver={(decision) => setResolver({ coincidencia: c, decision })} />
                ))}
              </Stack>
            )}
          </Box>
          {v.anteriores.length > 0 && <ConsultasAnteriores empresaId={empresaId} expedienteId={expediente.id} anteriores={v.anteriores} puedeDescargar={acciones.descargar} />}
        </Stack>
      )}
      {manual && (
        <DialogoConsultaManual
          expediente={expediente}
          empresaId={empresaId}
          ocupado={accion.isPending}
          error={accion.error}
          onCerrar={() => setManual(false)}
          onGuardar={(consulta) =>
            accion.mutate(
              { tipo: 'consultaManual', consulta },
              {
                onSuccess: () => {
                  mostrar(t('cumplimiento.manual.guardada'));
                  setManual(false);
                },
              },
            )
          }
        />
      )}
      {resolver && (
        <DialogoResolverCoincidencia
          coincidencia={resolver.coincidencia}
          decision={resolver.decision}
          ocupado={accion.isPending}
          error={accion.error}
          onCerrar={() => setResolver(null)}
          onGuardar={(justificacion) =>
            accion.mutate(
              { tipo: 'resolver', coincidenciaId: resolver.coincidencia.id, decision: resolver.decision, justificacion },
              {
                onSuccess: () => {
                  mostrar(t('cumplimiento.resolver.guardada'));
                  setResolver(null);
                },
              },
            )
          }
        />
      )}
    </Box>
  );
}
