import { Box, Button, Typography } from '@mui/material';
import type { ExpedienteDto, VerificacionDto } from '@validasoft/api-client';
import { Dato, formatearFecha, RejillaDatos } from '@validasoft/ui';
import { ClipboardCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionVerificacion } from '../api';
import { claveRiesgo } from '../modelo';
import { DialogoEstudio } from './DialogoEstudio';
import { EncabezadoVerificacion } from './EncabezadoVerificacion';

/** Estudio del oficial de cumplimiento (SAGRILAFT): lo registrado o lo que falta, y el botón para registrarlo o renovarlo. */
export function EstudioDeCumplimiento({
  expediente,
  empresaId,
  verificacion: v,
  puedeDecidir,
  nombres,
}: {
  expediente: ExpedienteDto;
  empresaId: string;
  verificacion: VerificacionDto;
  puedeDecidir: boolean;
  nombres: ReadonlyMap<string, string>;
}) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const accion = useAccionVerificacion(empresaId, expediente.id);
  const [abierto, setAbierto] = useState(false);
  const estudio = v.estudio;

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
        <RejillaDatos columnas={3}>
          <Dato etiqueta={t('cumplimiento.panel.estudio.riesgo')}>{t(`cumplimiento.riesgos.${claveRiesgo(estudio.nivelRiesgo)}`)}</Dato>
          <Dato etiqueta={t('cumplimiento.panel.estudio.debida')}>{t(`cumplimiento.debida.${estudio.debidaDiligencia as 'Simple'}`)}</Dato>
          <Dato etiqueta={t('cumplimiento.panel.estudio.concepto')}>{t(`cumplimiento.conceptos.${estudio.concepto as 'Favorable'}`)}</Dato>
          <Dato etiqueta={t('cumplimiento.panel.estudio.conflicto')}>{estudio.conflictoDeInteres ? t('comun.si') : t('comun.no')}</Dato>
          <Dato etiqueta={t('cumplimiento.panel.estudio.verificadoEnListas')}>{estudio.verificadoEnListas ? t('comun.si') : t('comun.no')}</Dato>
          <Dato etiqueta={t('cumplimiento.panel.vigenteHasta')}>{formatearFecha(estudio.vigenteHasta)}</Dato>
          <Dato etiqueta={t('cumplimiento.panel.estudio.oficial')}>
            {`${nombres.get(estudio.oficial) ?? estudio.oficial.slice(0, 8)} · ${formatearFecha(estudio.fecha)}`}
          </Dato>
          <Dato etiqueta={t('cumplimiento.panel.estudio.evidencias')} ancho>
            {estudio.evidencias.length === 0 ? '—' : estudio.evidencias.map((e) => e.nombre ?? `${e.documentoId.slice(0, 8)} v${String(e.version)}`).join(' · ')}
          </Dato>
          {estudio.observaciones && (
            <Dato etiqueta={t('cumplimiento.panel.estudio.observaciones')} ancho>
              <Box component="span" sx={{ whiteSpace: 'pre-wrap' }}>
                {estudio.observaciones}
              </Box>
            </Dato>
          )}
        </RejillaDatos>
      )}
      {abierto && (
        <DialogoEstudio
          expediente={expediente}
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
