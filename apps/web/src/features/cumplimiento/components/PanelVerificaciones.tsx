import { Alert, Box, Button, Chip, Stack, Typography } from '@mui/material';
import type { CoincidenciaDto, DecisionCoincidencia, ExpedienteDto, VerificacionDto } from '@validasoft/api-client';
import { Dato, formatearFecha, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { FileDown, RefreshCw, ShieldCheck, ShieldX } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionVerificacion, useDescargarEvidencia } from '../api';
import { accionesDeVerificacion, esSistema } from '../modelo';
import { DialogoResolverCoincidencia } from './DialogoResolverCoincidencia';
import { EncabezadoVerificacion } from './EncabezadoVerificacion';
import { EstudioDeCumplimiento } from './EstudioDeCumplimiento';

export interface PanelVerificacionesProps {
  expediente: ExpedienteDto;
  empresaId: string;
  scopes: ReadonlySet<string>;
  nombres: ReadonlyMap<string, string>;
}

/**
 * Verificaciones del expediente (H7): listas restrictivas (estado, personas consultadas, coincidencias con su decisión,
 * evidencia y reconsulta) y el estudio del oficial de cumplimiento. Solo la empresa lo ve; el tercero no.
 */
export function PanelVerificaciones({ expediente, empresaId, scopes, nombres }: PanelVerificacionesProps) {
  const { t } = useTranslation();
  const verificaciones = expediente.verificaciones ?? [];
  if (verificaciones.length === 0) return null;
  const listas = verificaciones.find((v) => v.tipo === 'ListasRestrictivas');
  const estudio = verificaciones.find((v) => v.tipo === 'EstudioDeCumplimiento');
  const acciones = accionesDeVerificacion(expediente, scopes);

  return (
    <Tarjeta titulo={t('cumplimiento.panel.titulo')} etiqueta={t('cumplimiento.panel.titulo')}>
      <Stack spacing={2}>
        {listas && <ListasRestrictivas expediente={expediente} empresaId={empresaId} verificacion={listas} acciones={acciones} nombres={nombres} />}
        {estudio && <EstudioDeCumplimiento expediente={expediente} empresaId={empresaId} verificacion={estudio} puedeDecidir={acciones.decidir} nombres={nombres} />}
      </Stack>
    </Tarjeta>
  );
}

function ListasRestrictivas({
  expediente,
  empresaId,
  verificacion: v,
  acciones,
  nombres,
}: {
  expediente: ExpedienteDto;
  empresaId: string;
  verificacion: VerificacionDto;
  acciones: ReturnType<typeof accionesDeVerificacion>;
  nombres: ReadonlyMap<string, string>;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const accion = useAccionVerificacion(empresaId, expediente.id);
  const descarga = useDescargarEvidencia(empresaId);
  const [resolver, setResolver] = useState<{ coincidencia: CoincidenciaDto; decision: DecisionCoincidencia } | null>(null);
  const consultada = Boolean(v.registradaEn);
  const quien = (id: string | null | undefined) => (!id || esSistema(id) ? t('cumplimiento.panel.sistema') : (nombres.get(id) ?? id.slice(0, 8)));

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
            <Dato etiqueta={t('cumplimiento.panel.proveedor')}>{v.proveedor ?? '—'}</Dato>
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
                  <Coincidencia key={c.id} coincidencia={c} puedeDecidir={acciones.decidir} quien={quien} onResolver={(decision) => setResolver({ coincidencia: c, decision })} />
                ))}
              </Stack>
            )}
          </Box>
          {v.anteriores.length > 0 && (
            <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('cumplimiento.panel.anteriores', { count: v.anteriores.length })}</Typography>
          )}
        </Stack>
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

function Coincidencia({
  coincidencia: c,
  puedeDecidir,
  quien,
  onResolver,
}: {
  coincidencia: CoincidenciaDto;
  puedeDecidir: boolean;
  quien: (id: string | null | undefined) => string;
  onResolver: (decision: DecisionCoincidencia) => void;
}) {
  const { t } = useTranslation();
  return (
    <Box component="li" sx={{ border: 1, borderColor: c.decision ? 'divider' : 'warning.main', borderRadius: 1, p: 1.25 }} aria-label={`${c.persona} · ${c.lista}`}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography sx={{ fontWeight: 500, fontSize: 14 }}>{c.persona}</Typography>
        <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
          {t(`cumplimiento.roles.${c.rol as 'Tercero'}`, { defaultValue: c.rol })} · {c.identificacion}
        </Typography>
        <Chip size="small" variant="outlined" label={c.lista} />
        {c.puntaje !== null && (
          <Typography component="span" variant="mono" sx={{ fontSize: 12 }}>
            {t('cumplimiento.panel.puntaje', { puntaje: String(c.puntaje) })}
          </Typography>
        )}
      </Stack>
      <Typography sx={{ fontSize: 13, mt: 0.5 }}>
        «{c.nombreEncontrado}»{c.detalle ? ` · ${c.detalle}` : ''}
      </Typography>
      {c.decision ? (
        <Typography sx={{ fontSize: 12, color: 'text.secondary', mt: 0.5 }}>
          {c.heredada
            ? t('cumplimiento.panel.heredada', { fecha: c.vigenteHasta ? formatearFecha(c.vigenteHasta) : '' })
            : t('cumplimiento.panel.decidida', {
                decision: t(`cumplimiento.decisiones.${c.decision as DecisionCoincidencia}`),
                quien: quien(c.resueltaPor),
                fecha: c.resueltaEn ? formatearFecha(c.resueltaEn) : '',
              })}
          {c.justificacion ? ` — “${c.justificacion}”` : ''}
        </Typography>
      ) : (
        puedeDecidir && (
          <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
            <Button size="small" variant="outlined" startIcon={<ShieldCheck size={16} aria-hidden />} onClick={() => onResolver('Descartada')}>
              {t('cumplimiento.panel.descartar')}
            </Button>
            <Button size="small" variant="outlined" color="error" startIcon={<ShieldX size={16} aria-hidden />} onClick={() => onResolver('Confirmada')}>
              {t('cumplimiento.panel.confirmar')}
            </Button>
          </Stack>
        )
      )}
    </Box>
  );
}
