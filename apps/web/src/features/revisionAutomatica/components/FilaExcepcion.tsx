import { Box, Button, Stack, Typography } from '@mui/material';
import type { ExcepcionDeRevisionDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { identificacionResumida } from '../../vinculos/tercero';
import { accionesDeMuestreo, haceCuantoEspera, porcentajeDeConfianza, resolucionDe, tipoDe, type DestinoDeMuestreo } from '../modelo';
import { BotonesDeMuestreo } from './BotonesDeMuestreo';
import { ChipsDeMotivos } from './ChipsDeMotivos';
import { ChipTipoExcepcion } from './ChipTipoExcepcion';

export interface FilaExcepcionProps {
  fila: ExcepcionDeRevisionDto;
  empresaId: string;
  /** Instante (ms) con el que se cuenta cuánto lleva esperando. */
  ahora: number;
  /** Nombre del área (si no se puede leer la configuración, su código). */
  area: string;
  puedeRevisar: boolean;
  /** ¿La persona es miembro del área? `null` = no se sabe (la API decide). */
  esMiembro: boolean | null;
  /** Nombre de quien resolvió el muestreo. */
  resolvio: string | null;
}

/**
 * Una excepción de la revisión automática: quién (tercero y folio), qué documento y área, por qué no se aprobó solo (o con qué
 * confianza se aprobó) y qué hacer: abrir el expediente o, en un muestreo, confirmarlo o revertirlo.
 */
export function FilaExcepcion({ fila, empresaId, ahora, area, puedeRevisar, esMiembro, resolvio }: FilaExcepcionProps) {
  const { t } = useTranslation();
  const tipo = tipoDe(fila);
  const documento = fila.requisito;
  const confianza = porcentajeDeConfianza(fila.confianza);
  const destino: DestinoDeMuestreo = { expedienteId: fila.expedienteId, requisitoId: fila.requisitoId, areaId: fila.areaId, documento, area, folio: fila.folio, tercero: fila.tercero.nombre };
  const acciones = accionesDeMuestreo({ pendiente: tipo === 'Muestreo' && !fila.resuelta, estadoExpediente: fila.estadoExpediente, puedeRevisar, esMiembro });
  const sinPermisoParaConfirmar = tipo === 'Muestreo' && !fila.resuelta && !acciones.confirmar && esMiembro === false;

  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'minmax(0, 1fr) auto' },
        columnGap: 2,
        rowGap: 1.25,
        py: 1.5,
        px: 2,
        border: 1,
        borderColor: 'divider',
        borderRadius: (th) => `${String(th.radio.md)}px`,
        bgcolor: 'background.paper',
      }}
    >
      <Stack spacing={0.75} sx={{ minWidth: 0 }}>
        <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', minWidth: 0 }}>
          <ChipTipoExcepcion tipo={tipo} />
          <Typography component="h3" noWrap sx={{ fontWeight: 600, fontSize: 14, lineHeight: '20px', minWidth: 0 }}>
            {fila.tercero.nombre}
          </Typography>
          <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px', fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
            {identificacionResumida(fila.tercero.identificacion)}
          </Typography>
        </Stack>
        <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
          <Typography component="span" variant="monoTabla">
            {fila.folio}
          </Typography>
          {` · ${fila.tipoVinculo} · ${t('revisionAutomatica.excepciones.documento', { documento, n: String(fila.version) })} · ${t('revisionAutomatica.excepciones.area', { area })}`}
          {confianza && ` · ${t('revisionAutomatica.excepciones.confianza', { porcentaje: confianza })}`}
        </Typography>
        {tipo === 'Excepcion' ? (
          <ChipsDeMotivos motivos={fila.motivos} etiqueta={t('revisionAutomatica.excepciones.motivos')} />
        ) : (
          <Typography sx={{ fontSize: 13, lineHeight: '18px' }}>{t('revisionAutomatica.excepciones.aprobadoPorElSistema')}</Typography>
        )}
      </Stack>

      <Stack spacing={1} sx={{ alignItems: { xs: 'flex-start', md: 'flex-end' }, justifyContent: 'space-between', minWidth: 0 }}>
        <Typography variant="caption" sx={{ color: 'text.secondary' }}>
          {fila.resuelta ? textoDeResolucion(fila, resolvio, t) : sinPermisoParaConfirmar ? t('revisionAutomatica.excepciones.soloMiembros') : t('revisionAutomatica.excepciones.esperando', { cuando: haceCuantoEspera(fila.desde, ahora) })}
        </Typography>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', justifyContent: { xs: 'flex-start', md: 'flex-end' } }}>
          <BotonesDeMuestreo empresaId={empresaId} destino={destino} acciones={acciones} />
          <Button
            size="small"
            variant={tipo === 'Excepcion' && !fila.resuelta ? 'contained' : 'outlined'}
            component={RouterLink}
            to={`/e/${empresaId}/expedientes/${fila.expedienteId}`}
            aria-label={t('revisionAutomatica.excepciones.abrirAccesible', { folio: fila.folio, tercero: fila.tercero.nombre })}
          >
            {t('revisionAutomatica.excepciones.abrir')}
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
}

/** «Confirmada por Ana · 10/11/2026», «Revertida · …» o, de una excepción, que ya no espera a nadie. */
function textoDeResolucion(fila: ExcepcionDeRevisionDto, resolvio: string | null, t: TFunction): string {
  const decision = resolucionDe(fila.resolucion);
  if (!decision) return t('revisionAutomatica.excepciones.resolucion.excepcion');
  const nombre = t(`revisionAutomatica.excepciones.resolucion.${decision}`);
  const quien = resolvio ? t('revisionAutomatica.excepciones.resolucion.porPersona', { decision: nombre, persona: resolvio }) : nombre;
  return [quien, fila.resueltaEn ? formatearFecha(fila.resueltaEn) : null].filter(Boolean).join(' · ');
}
