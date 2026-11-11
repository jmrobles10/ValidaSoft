import { Box, Button, Typography } from '@mui/material';
import type { ExpedienteDto, VerificacionDto } from '@validasoft/api-client';
import { Dato, formatearFecha, RejillaDatos } from '@validasoft/ui';
import { ClipboardCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionVerificacion } from '../api';
import { claveRiesgo } from '../modelo';
import { ArchivosGuardados } from './ArchivosGuardados';
import { ChipNivelRiesgo } from './ChipNivelRiesgo';
import { DialogoEstudio } from './DialogoEstudio';
import { EncabezadoVerificacion } from './EncabezadoVerificacion';
import { ResultadoMatriz } from './ResultadoMatriz';

/** Estudio del oficial de cumplimiento (SAGRILAFT): lo registrado o lo que falta, y el botón para registrarlo o renovarlo. */
export function EstudioDeCumplimiento({
  expediente,
  empresaId,
  verificacion: v,
  puedeDecidir,
  puedeDescargar,
  nombres,
}: {
  expediente: ExpedienteDto;
  empresaId: string;
  verificacion: VerificacionDto;
  puedeDecidir: boolean;
  puedeDescargar: boolean;
  nombres: ReadonlyMap<string, string>;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const accion = useAccionVerificacion(empresaId, expediente.id);
  const [abierto, setAbierto] = useState(false);
  const estudio = v.estudio;
  const matriz = estudio?.matriz ?? null;
  const ajustado = matriz !== null && estudio !== null && matriz.nivel !== estudio.nivelRiesgo;

  return (
    <Box component="section" aria-label={t('cumplimiento.tipos.EstudioDeCumplimiento')}>
      <EncabezadoVerificacion
        verificacion={v}
        acciones={
          puedeDecidir && (
            <Button size="small" variant={estudio ? 'outlined' : 'contained'} startIcon={<ClipboardCheck size={16} aria-hidden />} onClick={() => setAbierto(true)}>
              {estudio ? t('cumplimiento.panel.estudio.renovar') : t('cumplimiento.panel.estudio.registrar')}
            </Button>
          )
        }
      />
      {!estudio && <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.panel.pendienteEstudio')}</Typography>}
      {v.estado === 'Expirada' && <Typography sx={{ color: 'text.secondary', fontSize: 13, mb: 1 }}>{t('cumplimiento.panel.expiradaEstudio')}</Typography>}
      {estudio && (
        <>
          <RejillaDatos columnas={3}>
            <Dato etiqueta={t('cumplimiento.panel.estudio.riesgo')}>
              <ChipNivelRiesgo nivel={estudio.nivelRiesgo} />
            </Dato>
            <Dato etiqueta={t('cumplimiento.panel.estudio.debida')}>{t(`cumplimiento.debida.${estudio.debidaDiligencia}`)}</Dato>
            <Dato etiqueta={t('cumplimiento.panel.estudio.concepto')}>{t(`cumplimiento.conceptos.${estudio.concepto}`)}</Dato>
            <Dato etiqueta={t('cumplimiento.panel.estudio.conflicto')}>{estudio.conflictoDeInteres ? t('comun.si') : t('comun.no')}</Dato>
            <Dato etiqueta={t('cumplimiento.panel.estudio.verificadoEnListas')}>{estudio.verificadoEnListas ? t('comun.si') : t('comun.no')}</Dato>
            <Dato etiqueta={t('cumplimiento.panel.vigenteHasta')}>{formatearFecha(estudio.vigenteHasta)}</Dato>
            <Dato etiqueta={t('cumplimiento.panel.estudio.oficial')}>
              {`${nombres.get(estudio.oficial) ?? estudio.oficial.slice(0, 8)} · ${formatearFecha(estudio.fecha)}`}
            </Dato>
            <Dato etiqueta={t('cumplimiento.panel.estudio.evidencias')} ancho>
              {estudio.evidencias.length === 0 ? '—' : estudio.evidencias.map((e) => e.nombre ?? `${e.documentoId.slice(0, 8)} v${String(e.version)}`).join(' · ')}
            </Dato>
            {(estudio.adjuntos ?? []).length > 0 && (
              <Dato etiqueta={t('cumplimiento.panel.estudio.archivos')} ancho>
                <ArchivosGuardados empresaId={empresaId} expedienteId={expediente.id} archivos={estudio.adjuntos ?? []} puedeDescargar={puedeDescargar} />
              </Dato>
            )}
            {estudio.observaciones && (
              <Dato etiqueta={t('cumplimiento.panel.estudio.observaciones')} ancho>
                <Box component="span" sx={{ whiteSpace: 'pre-wrap' }}>
                  {estudio.observaciones}
                </Box>
              </Dato>
            )}
          </RejillaDatos>
          {matriz && (
            <Box sx={{ mt: 1.5 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 500, mb: 0.5 }}>{t('cumplimiento.panel.estudio.matriz')}</Typography>
              <ResultadoMatriz resultado={matriz} />
              {ajustado && (
                <Typography sx={{ fontSize: 13, mt: 1 }}>
                  <Typography component="span" sx={{ fontWeight: 500, fontSize: 13 }}>
                    {t('cumplimiento.matriz.ajustadaDe', { de: t(`cumplimiento.riesgos.${claveRiesgo(matriz.nivel)}`), a: t(`cumplimiento.riesgos.${claveRiesgo(estudio.nivelRiesgo)}`) })}
                  </Typography>
                  {estudio.justificacionDeNivel ? ` — “${estudio.justificacionDeNivel}”` : ''}
                </Typography>
              )}
            </Box>
          )}
        </>
      )}
      {abierto && (
        <DialogoEstudio
          expediente={expediente}
          empresaId={empresaId}
          vigenciaDias={v.vigenciaDias ?? null}
          ocupado={accion.isPending}
          error={accion.error}
          onCerrar={() => setAbierto(false)}
          onGuardar={(dto) =>
            accion.mutate(
              { tipo: 'estudio', estudio: dto },
              {
                onSuccess: () => {
                  mostrar(t('cumplimiento.estudio.guardado'));
                  setAbierto(false);
                },
              },
            )
          }
        />
      )}
    </Box>
  );
}
