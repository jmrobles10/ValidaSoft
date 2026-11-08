import { Alert, AlertTitle, Box, Button, Card, Stack, TextField, Typography } from '@mui/material';
import { esErrorApi } from '@validasoft/api-client';
import { ChipEstado, formatearFecha, PaginaEstado } from '@validasoft/ui';
import { SearchX, Upload } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router';
import { useExpedientePublico, useReenviarExpediente, useVinculosPublicos } from '../shared/api';
import { useAvisos } from '../shared/useAvisos';
import { CabeceraPagina, ErrorRecuperable, EsqueletoTarjetas } from '../shared/componentes';
import { mensajeDeError } from '../shared/errores';
import { avanceDelTercero } from '../vinculos/avance';
import { ChipAvance } from '../vinculos/ChipAvance';
import { useTextoAvance } from '../vinculos/useTextoAvance';
import { situacionDelVinculo } from '../vinculos/situacion';
import { observacionesGenerales } from './requisitos';
import { Observacion, RequisitosDelExpediente } from './RequisitosDelExpediente';

const MAXIMO_RESPUESTA = 4000;

/**
 * Mi revisión con una empresa (`ExpedientePublicoDto`): estado, los documentos que pide con lo que falta y lo
 * observado (con sus motivos, nunca revisores ni áreas internas, FL-10), subir directo desde cada documento y, si está
 * devuelto, «Enviar respuesta» (reenviar) con o sin versión nueva (RQ-7).
 */
export function PaginaExpediente() {
  const { t } = useTranslation();
  const { id = '' } = useParams();
  const consulta = useExpedientePublico(id);
  const vinculos = useVinculosPublicos();
  const reenvio = useReenviarExpediente(id);
  const mostrar = useAvisos((s) => s.mostrar);
  const textoAvance = useTextoAvance();
  const [respuesta, setRespuesta] = useState('');

  if (consulta.isPending) return <EsqueletoTarjetas />;
  if (consulta.isError) {
    if (esErrorApi(consulta.error) && consulta.error.esNoEncontrado) {
      return <PaginaEstado icono={SearchX} titulo={t('enlace.fallos.no-encontrado.titulo')} texto={t('empresas.vacioTexto')} />;
    }
    return <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />;
  }

  const expediente = consulta.data;
  const empresa = expediente.empresa.nombre;
  const conRequisitos = (expediente.requisitos?.length ?? 0) > 0;
  const faltantes = expediente.requisitosFaltantes ?? 0;
  // Con documentos, cada observación va en su tarjeta; aquí quedan las del expediente completo.
  const observaciones = (conRequisitos ? observacionesGenerales(expediente) : [...expediente.observaciones]).sort((a, b) => b.fecha.localeCompare(a.fecha));
  const vinculo = vinculos.data?.find((v) => v.expedienteId === expediente.id);
  const vinculoId = vinculo?.id ?? null;
  // Con el vínculo suspendido o terminado no se sube ni se reenvía para esta empresa (la API respondería 409).
  const situacion = situacionDelVinculo(vinculo?.estado);
  // Lo que le toca al tercero («Faltan 3 documentos», «En revisión»…) en vez del estado interno.
  const avance = avanceDelTercero(vinculo, expediente);

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    reenvio.mutate(respuesta, {
      onSuccess: () => {
        setRespuesta('');
        mostrar(t('expediente.reenviado', { empresa }));
      },
    });
  };

  return (
    <>
      <CabeceraPagina
        titulo={t('expediente.titulo', { empresa })}
        sub={
          <>
            {expediente.tipoVinculo.nombre} ·{' '}
            <Typography component="span" variant="mono">
              {t('expediente.folio', { folio: expediente.folio })}
            </Typography>
          </>
        }
      />

      <Card sx={{ p: 2, mb: 2 }}>
        <Stack direction="row" spacing={1} alignItems="center" useFlexGap flexWrap="wrap" sx={{ mb: 1 }}>
          {avance ? <ChipAvance avance={avance} /> : <ChipEstado estado={expediente.estado} fecha={expediente.actualizadoEn} size="medium" />}
          <Typography color="text.secondary" sx={{ fontSize: 13 }}>
            {t('expediente.actualizado', { fecha: formatearFecha(expediente.actualizadoEn) })}
          </Typography>
        </Stack>
        <Typography sx={{ fontSize: 15, lineHeight: '22px' }}>
          {avance
            ? textoAvance(avance, empresa)
            : expediente.estado === 'Abierto' && faltantes > 0
              ? t('expediente.faltan', { count: faltantes, empresa })
              : t(`expediente.estadoTexto.${expediente.estado}`, { empresa })}
        </Typography>
        {expediente.reenvios > 0 && (
          <Typography color="text.secondary" sx={{ fontSize: 14, mt: 1 }}>
            {t('expediente.reenvios', { count: expediente.reenvios })}
          </Typography>
        )}
      </Card>

      {situacion !== 'vigente' && (
        <Alert severity={situacion === 'suspendida' ? 'warning' : 'info'} sx={{ mb: 2 }}>
          <AlertTitle>{t(`vinculoSituacion.${situacion}.titulo`, { empresa })}</AlertTitle>
          {t(`vinculoSituacion.${situacion}.texto`, { empresa })}
        </Alert>
      )}

      {conRequisitos && <RequisitosDelExpediente expediente={expediente} vinculoId={vinculoId} soloLectura={situacion !== 'vigente'} />}

      {observaciones.length > 0 && (
        <Box component="section" aria-labelledby="titulo-observaciones" sx={{ mb: 2 }}>
          <Typography id="titulo-observaciones" component="h2" sx={{ fontWeight: 600, fontSize: 13, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'text.secondary', mb: 1 }}>
            {t('expediente.observaciones')}
          </Typography>
          <Stack spacing={1.5}>
            {observaciones.map((o) => (
              <Card key={o.fecha} sx={{ p: 2, pt: 0.5 }}>
                <Observacion observacion={o} />
              </Card>
            ))}
          </Stack>
        </Box>
      )}

      {expediente.puedeReenviar && situacion === 'vigente' && (
        <Card component="form" onSubmit={enviar} noValidate sx={{ p: 2 }}>
          <Stack spacing={1.5}>
            {reenvio.isError && <Alert severity="error">{mensajeDeError(reenvio.error, t)}</Alert>}
            <TextField
              label={t('expediente.respuesta')}
              helperText={conRequisitos ? t('expediente.respuestaAyudaRequisitos') : t('expediente.respuestaAyuda')}
              value={respuesta}
              onChange={(e) => setRespuesta(e.target.value.slice(0, MAXIMO_RESPUESTA))}
              multiline
              minRows={3}
              disabled={reenvio.isPending}
            />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <Button type="submit" variant="contained" size="large" disabled={reenvio.isPending}>
                {t('expediente.reenviar')}
              </Button>
              {!conRequisitos && (
                <Button component={RouterLink} to="/boveda" variant="outlined" size="large" startIcon={<Upload size={18} aria-hidden />}>
                  {t('expediente.subirEnBoveda')}
                </Button>
              )}
            </Stack>
          </Stack>
        </Card>
      )}
    </>
  );
}
