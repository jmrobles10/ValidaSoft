import { Box, Chip, Stack, Typography } from '@mui/material';
import { SCOPES, type AreaExpedienteDto, type ExpedienteDto, type RequisitoExpedienteDto, type RevisionAutomaticaDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { ContextoRevisor } from '../../expedientes/reglas';
import { accionesDeMuestreo, vistaDeRevision, type VistaDeRevision } from '../modelo';
import { BotonesDeMuestreo } from './BotonesDeMuestreo';
import { ChipsDeMotivos } from './ChipsDeMotivos';

export interface RevisionesAutomaticasDelRequisitoProps {
  expediente: ExpedienteDto;
  requisito: RequisitoExpedienteDto;
  areas: ReadonlyMap<string, AreaExpedienteDto>;
  contexto: (area: AreaExpedienteDto | null) => ContextoRevisor;
  nombres: ReadonlyMap<string, string>;
}

/**
 * Lo que la revisión automática hizo con un documento, por área (ADR-0012): la sugerencia del nivel Asistido, la aprobación
 * del sistema (con «Confirmar» y «Revertir» si falta que una persona la confirme por muestreo) o el pase a una persona con
 * sus motivos. Un área fuera del alcance de la revisión automática no dice nada. No cambia la decisión humana: es lo que
 * decidió el sistema, al lado de lo que decide cada área.
 */
export function RevisionesAutomaticasDelRequisito({ expediente, requisito, areas, contexto, nombres }: RevisionesAutomaticasDelRequisitoProps) {
  const { t } = useTranslation();
  const filas = (requisito.revisionesAutomaticas ?? []).flatMap((revision) => {
    const vista = vistaDeRevision(revision);
    return vista ? [{ revision, vista }] : [];
  });
  if (filas.length === 0) return null;

  return (
    <Box
      component="section"
      aria-label={t('revisionAutomatica.panel.titulo')}
      sx={{ mt: 1, py: 1, px: 1.5, border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel1' }}
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 0.75 }}>
        <Sparkles size={14} strokeWidth={1.75} aria-hidden />
        <Typography variant="overline" component="h3" sx={{ color: 'text.secondary' }}>
          {t('revisionAutomatica.panel.titulo')}
        </Typography>
      </Stack>
      <Stack component="ul" spacing={0.75} sx={{ listStyle: 'none', m: 0, p: 0 }}>
        {filas.map(({ revision, vista }) => {
          const area = areas.get(revision.areaId) ?? null;
          return (
            <FilaDeRevision
              key={revision.areaId}
              expediente={expediente}
              requisito={requisito}
              revision={revision}
              vista={vista}
              nombreArea={area?.nombre ?? revision.areaCodigo}
              contexto={contexto(area)}
              nombres={nombres}
            />
          );
        })}
      </Stack>
    </Box>
  );
}

interface FilaProps extends Pick<RevisionesAutomaticasDelRequisitoProps, 'expediente' | 'requisito' | 'nombres'> {
  revision: RevisionAutomaticaDto;
  vista: VistaDeRevision;
  nombreArea: string;
  contexto: ContextoRevisor;
}

function FilaDeRevision({ expediente, requisito, revision, vista, nombreArea, contexto, nombres }: FilaProps) {
  const { t } = useTranslation();
  const persona = revision.resueltaPor ? (nombres.get(revision.resueltaPor) ?? null) : null;
  const acciones =
    vista.tipo === 'aprobada' && vista.muestreo
      ? accionesDeMuestreo({
          pendiente: vista.pendiente,
          estadoExpediente: expediente.estado,
          puedeRevisar: contexto.scopes.has(SCOPES.expedientesRevisar),
          esMiembro: contexto.esMiembro,
        })
      : { confirmar: false, revertir: false };

  return (
    <Box component="li" sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', columnGap: 1, rowGap: 0.5 }}>
      <Typography component="span" sx={{ fontWeight: 500, fontSize: 13, lineHeight: '20px' }}>
        {nombreArea}
      </Typography>
      <Typography component="span" sx={{ fontSize: 13, lineHeight: '20px', color: vista.tipo === 'excepcion' && !vista.abierta ? 'text.secondary' : 'text.primary' }}>
        {textoDeLaVista(vista, t)}
      </Typography>
      {(vista.tipo === 'sugerida' && vista.sugerencia === 'Revisar') || vista.tipo === 'excepcion' ? (
        <ChipsDeMotivos motivos={vista.motivos} etiqueta={t('revisionAutomatica.excepciones.motivos')} />
      ) : null}
      {vista.tipo === 'aprobada' && vista.muestreo && (
        <>
          {vista.pendiente && <Chip size="small" variant="outlined" label={t('revisionAutomatica.panel.pendienteConfirmar')} sx={{ borderRadius: (th) => `${String(th.radio.xs)}px` }} />}
          {vista.resolucion && (
            <Typography component="span" sx={{ flexBasis: '100%', fontSize: 13, lineHeight: '20px', color: 'text.secondary' }}>
              {textoDeLaResolucion(vista.resolucion, persona, t)}
              {vista.resolucion === 'Revertida' && revision.justificacionResolucion ? ` · ${t('revisionAutomatica.panel.justificacion', { texto: revision.justificacionResolucion })}` : ''}
            </Typography>
          )}
          <BotonesDeMuestreo
            empresaId={expediente.empresaId}
            acciones={acciones}
            destino={{
              expedienteId: expediente.id,
              requisitoId: requisito.id,
              areaId: revision.areaId,
              documento: requisito.tipoDocumento.nombre,
              area: nombreArea,
              folio: expediente.folio,
              tercero: expediente.tercero.nombre,
            }}
          />
        </>
      )}
    </Box>
  );
}

function textoDeLaVista(vista: VistaDeRevision, t: TFunction): string {
  switch (vista.tipo) {
    case 'sugerida':
      return vista.sugerencia === 'Aprobar' ? t('revisionAutomatica.panel.sugiereAprobar') : `${t('revisionAutomatica.panel.sugiereRevisar')}:`;
    case 'aprobada':
      return vista.confianza ? t('revisionAutomatica.panel.aprobado', { porcentaje: vista.confianza }) : t('revisionAutomatica.panel.aprobadoSinConfianza');
    case 'excepcion':
      return `${vista.abierta ? t('revisionAutomatica.panel.requierePersona') : t('revisionAutomatica.panel.paso')}:`;
  }
}

function textoDeLaResolucion(resolucion: 'Confirmada' | 'Revertida', persona: string | null, t: TFunction): string {
  if (resolucion === 'Confirmada') return persona ? t('revisionAutomatica.panel.confirmada', { persona }) : t('revisionAutomatica.panel.confirmadaSinPersona');
  return persona ? t('revisionAutomatica.panel.revertida', { persona }) : t('revisionAutomatica.panel.revertidaSinPersona');
}
