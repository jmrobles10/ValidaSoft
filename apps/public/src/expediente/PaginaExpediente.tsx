import { Alert, Box, Button, Card, Stack, TextField, Typography } from '@mui/material';
import { esErrorApi } from '@validasoft/api-client';
import { ChipEstado, formatearFecha, PaginaEstado } from '@validasoft/ui';
import { SearchX, Upload } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router';
import { useExpedientePublico, useReenviarExpediente } from '../shared/api';
import { useAvisos } from '../shared/useAvisos';
import { CabeceraPagina, ErrorRecuperable, EsqueletoTarjetas } from '../shared/componentes';
import { mensajeDeError } from '../shared/errores';

const MAXIMO_RESPUESTA = 4000;

/**
 * Mi revisión con una empresa (`ExpedientePublicoDto`): estado, observaciones con los textos de sus motivos (nunca
 * revisores ni áreas internas, FL-10) y, si está devuelto, la respuesta con «Enviar respuesta» (reenviar).
 */
export function PaginaExpediente() {
  const { t } = useTranslation();
  const { id = '' } = useParams();
  const consulta = useExpedientePublico(id);
  const reenvio = useReenviarExpediente(id);
  const mostrar = useAvisos((s) => s.mostrar);
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
  const observaciones = [...expediente.observaciones].sort((a, b) => b.fecha.localeCompare(a.fecha));

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
          <ChipEstado estado={expediente.estado} fecha={expediente.actualizadoEn} size="medium" />
          <Typography color="text.secondary" sx={{ fontSize: 13 }}>
            {t('expediente.actualizado', { fecha: formatearFecha(expediente.actualizadoEn) })}
          </Typography>
        </Stack>
        <Typography sx={{ fontSize: 15, lineHeight: '22px' }}>{t(`expediente.estadoTexto.${expediente.estado}`, { empresa })}</Typography>
        {expediente.reenvios > 0 && (
          <Typography color="text.secondary" sx={{ fontSize: 14, mt: 1 }}>
            {t('expediente.reenvios', { count: expediente.reenvios })}
          </Typography>
        )}
      </Card>

      {observaciones.length > 0 && (
        <Box component="section" aria-labelledby="titulo-observaciones" sx={{ mb: 2 }}>
          <Typography id="titulo-observaciones" component="h2" sx={{ fontWeight: 600, fontSize: 13, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'text.secondary', mb: 1 }}>
            {t('expediente.observaciones')}
          </Typography>
          <Stack spacing={1.5}>
            {observaciones.map((o) => (
              <Card key={o.fecha} sx={{ p: 2 }}>
                <Typography color="text.secondary" sx={{ fontSize: 13, mb: 1 }}>
                  {t('expediente.observacionDel', { fecha: formatearFecha(o.fecha) })}
                </Typography>
                <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, listStyle: 'none' }}>
                  {o.motivos.map((m) => (
                    <Typography component="li" key={m.codigo} sx={{ fontSize: 15, lineHeight: '22px' }}>
                      {m.texto}
                    </Typography>
                  ))}
                </Stack>
                {o.observacion && (
                  <Typography sx={{ mt: 1.5, p: 1.5, borderRadius: 1, bgcolor: 'superficie.nivel2', fontSize: 15, lineHeight: '22px', whiteSpace: 'pre-wrap' }}>
                    “{o.observacion}”
                  </Typography>
                )}
              </Card>
            ))}
          </Stack>
        </Box>
      )}

      {expediente.puedeReenviar && (
        <Card component="form" onSubmit={enviar} noValidate sx={{ p: 2 }}>
          <Stack spacing={1.5}>
            {reenvio.isError && <Alert severity="error">{mensajeDeError(reenvio.error, t)}</Alert>}
            <TextField
              label={t('expediente.respuesta')}
              helperText={t('expediente.respuestaAyuda')}
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
              <Button component={RouterLink} to="/boveda" variant="outlined" size="large" startIcon={<Upload size={18} aria-hidden />}>
                {t('expediente.subirEnBoveda')}
              </Button>
            </Stack>
          </Stack>
        </Card>
      )}
    </>
  );
}
