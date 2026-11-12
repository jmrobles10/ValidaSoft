import { Alert, Button, Stack, Typography } from '@mui/material';
import { esErrorApi, SCOPES, type CoincidenciaDto, type DecisionCoincidencia } from '@validasoft/api-client';
import { ChipEstado, Dato, formatearFecha, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { FileDown } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, IconoCarga, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useNombresUsuarios } from '../../configuracion/usuarios/api';
import { useDescargarEvidenciaMonitoreo, useMonitoreoListas, useResolverCoincidenciaMonitoreo } from '../api';
import { esSistema } from '../modelo';
import { CoincidenciaDeListas } from './CoincidenciaDeListas';
import { DialogoResolverCoincidencia } from './DialogoResolverCoincidencia';

/**
 * Monitoreo de listas de un vínculo activo (VE-12): la última reconsulta periódica, la próxima y las coincidencias nuevas que
 * Cumplimiento descarta o confirma. Es a donde lleva la fila «Monitoreo del vínculo» de la bandeja del oficial. Un vínculo sin
 * monitoreo (no consultó listas, o la empresa lo apagó) no muestra nada. Confirmar una coincidencia no bloquea ni desvincula: eso
 * lo decide un administrador con las acciones del vínculo (f-4).
 */
export function TarjetaMonitoreoListas({ vinculoId, empresaId }: { vinculoId: string; empresaId: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const permisos = usePermisos(empresaId);
  const consulta = useMonitoreoListas(empresaId, vinculoId, permisos.puede(SCOPES.expedientesLeer));
  const resolver = useResolverCoincidenciaMonitoreo(empresaId, vinculoId);
  const descarga = useDescargarEvidenciaMonitoreo(empresaId);
  const [decidiendo, setDecidiendo] = useState<{ coincidencia: CoincidenciaDto; decision: DecisionCoincidencia } | null>(null);
  const monitoreo = consulta.data;
  const personas = useMemo(() => {
    const ids = [monitoreo?.listas.registradaPor, ...(monitoreo?.listas.coincidencias.map((c) => c.resueltaPor) ?? [])];
    return ids.filter((id): id is string => !esSistema(id));
  }, [monitoreo]);
  const nombres = useNombresUsuarios(empresaId, personas);

  if (consulta.isError) {
    // Un vínculo que nunca consultó listas no tiene monitoreo: no hay nada que mostrar.
    if (esErrorApi(consulta.error) && consulta.error.esNoEncontrado) return null;
    return <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />;
  }
  if (!monitoreo) return null;

  const listas = monitoreo.listas;
  const quien = (id: string | null | undefined) => (esSistema(id) ? t('cumplimiento.panel.sistema') : (nombres.get(id ?? '') ?? (id ?? '').slice(0, 8)));
  const confirmada = listas.coincidencias.some((c) => c.decision === 'Confirmada');
  const puedeDecidir = permisos.puede(SCOPES.cumplimientoDecidir);
  const puedeDescargar = permisos.puede(SCOPES.documentosDescargar) && Boolean(listas.evidencia);

  return (
    <Tarjeta
      titulo={t('cumplimiento.monitoreo.titulo')}
      etiqueta={t('cumplimiento.monitoreo.titulo')}
      acciones={
        puedeDescargar && (
          <Button
            size="small"
            variant="text"
            startIcon={descarga.isPending ? <IconoCarga /> : <FileDown size={16} aria-hidden />}
            onClick={() => descarga.mutate(vinculoId, { onError: (e) => mostrar(mensajeDeError(e, t), 'error') })}
            disabled={descarga.isPending}
          >
            {t('cumplimiento.panel.evidencia')}
          </Button>
        )
      }
    >
      <Stack spacing={1.5}>
        <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.monitoreo.ayuda', { dias: String(monitoreo.frecuenciaDias) })}</Typography>
        <RejillaDatos columnas={3}>
          <Dato etiqueta={t('cumplimiento.monitoreo.estado')}>
            <ChipEstado estado={listas.estado} etiqueta={t(`cumplimiento.estados.${listas.estado}`)} />
          </Dato>
          <Dato etiqueta={t('cumplimiento.monitoreo.ultima')}>
            {listas.registradaEn ? `${formatearFechaHora(listas.registradaEn)} · ${quien(listas.registradaPor)}` : '—'}
          </Dato>
          <Dato etiqueta={t('cumplimiento.monitoreo.proxima')}>
            {monitoreo.proximaConsulta ? formatearFecha(monitoreo.proximaConsulta) : t('cumplimiento.monitoreo.detenido')}
          </Dato>
        </RejillaDatos>
        {listas.ultimoError && (
          <Alert severity="warning">
            {t('cumplimiento.panel.ultimoError', { error: listas.ultimoError, cuando: listas.reintentarDesde ? formatearFechaHora(listas.reintentarDesde) : '' })}
          </Alert>
        )}
        <div>
          <Typography sx={{ fontSize: 13, fontWeight: 500, mb: 0.5 }}>{t('cumplimiento.panel.coincidencias')}</Typography>
          {listas.coincidencias.length === 0 ? (
            <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.panel.sinCoincidencias')}</Typography>
          ) : (
            <Stack component="ul" spacing={1} sx={{ listStyle: 'none', p: 0, m: 0 }}>
              {listas.coincidencias.map((c) => (
                <CoincidenciaDeListas key={c.id} coincidencia={c} puedeDecidir={puedeDecidir} quien={quien} onResolver={(decision) => setDecidiendo({ coincidencia: c, decision })} />
              ))}
            </Stack>
          )}
        </div>
        {confirmada && <Alert severity="warning">{t('cumplimiento.monitoreo.confirmada')}</Alert>}
      </Stack>
      {decidiendo && (
        <DialogoResolverCoincidencia
          coincidencia={decidiendo.coincidencia}
          decision={decidiendo.decision}
          ocupado={resolver.isPending}
          error={resolver.error}
          onCerrar={() => {
            resolver.reset();
            setDecidiendo(null);
          }}
          onGuardar={(justificacion) =>
            resolver.mutate(
              { coincidenciaId: decidiendo.coincidencia.id, decision: decidiendo.decision, justificacion },
              {
                onSuccess: () => {
                  mostrar(t('cumplimiento.resolver.guardada'));
                  setDecidiendo(null);
                },
              },
            )
          }
        />
      )}
    </Tarjeta>
  );
}
