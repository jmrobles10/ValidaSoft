import { Box, Button, Card, Stack, Typography } from '@mui/material';
import type { ExpedientePublicoDto, ObservacionPublicaDto, RequisitoPublicoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { FileText, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { accionDeRequisito, enlaceDeSubida, observacionesDeRequisito, requisitosPorGrupo, type GrupoRequisito } from './requisitos';

const GRUPOS: readonly GrupoRequisito[] = ['te-toca', 'en-revision', 'listo'];

/** Los documentos que pide la empresa, agrupados en «Te toca», «Lo revisa la empresa» y «Listo». */
export function RequisitosDelExpediente({ expediente, vinculoId, soloLectura = false }: { expediente: ExpedientePublicoDto; vinculoId: string | null; soloLectura?: boolean }) {
  const { t } = useTranslation();
  const grupos = requisitosPorGrupo(expediente);
  const volver = `/expediente/${expediente.id}`;
  const empresa = expediente.empresa.nombre;

  return (
    <Box component="section" aria-labelledby="titulo-documentos" sx={{ mb: 2 }}>
      <Typography id="titulo-documentos" component="h2" variant="h4" sx={{ mb: 1.5 }}>
        {t('expediente.documentos.titulo', { empresa })}
      </Typography>
      <Stack spacing={2}>
        {GRUPOS.filter((g) => grupos[g].length > 0).map((grupo) => (
          <Box key={grupo}>
            <Typography component="h3" sx={{ fontWeight: 600, fontSize: 13, letterSpacing: '0.04em', textTransform: 'uppercase', color: 'text.secondary', mb: 1 }}>
              {t(`expediente.documentos.grupos.${grupo}`, { empresa })}
            </Typography>
            <Stack component="ul" spacing={1.25} sx={{ p: 0, m: 0, listStyle: 'none' }}>
              {grupos[grupo].map((r) => (
                <TarjetaRequisitoPublico key={r.id} requisito={r} expediente={expediente} enlace={soloLectura ? null : enlaceDeSubida(r, vinculoId, volver)} />
              ))}
            </Stack>
          </Box>
        ))}
      </Stack>
    </Box>
  );
}

function TarjetaRequisitoPublico({ requisito, expediente, enlace }: { requisito: RequisitoPublicoDto; expediente: ExpedientePublicoDto; enlace: string | null }) {
  const { t } = useTranslation();
  const accion = accionDeRequisito(requisito, expediente);
  const observaciones = requisito.estado === 'Observado' ? observacionesDeRequisito(requisito, expediente) : [];
  const version = requisito.versionActual;

  return (
    <Card component="li" sx={{ p: 2, borderColor: requisito.estado === 'Observado' ? 'estado.advertencia.borde' : undefined }}>
      <Stack direction="row" spacing={1.5} alignItems="flex-start">
        <Box sx={{ color: 'text.secondary', pt: 0.25 }} aria-hidden>
          <FileText size={20} />
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography component="h4" sx={{ fontWeight: 600, fontSize: 16, lineHeight: '22px' }}>
            {requisito.tipoDocumento.nombre}
          </Typography>
          <Typography color="text.secondary" sx={{ fontSize: 14 }}>
            {version
              ? t('expediente.documentos.version', { n: String(version.n), fecha: formatearFecha(version.cargadaEn) })
              : requisito.obligatorio
                ? t('expediente.documentos.faltaObligatorio')
                : t('expediente.documentos.faltaOpcional')}
          </Typography>
        </Box>
        <ChipEstado estado={requisito.estado} size="medium" />
      </Stack>

      {observaciones.map((o) => (
        <Observacion key={o.fecha} observacion={o} />
      ))}

      {accion && enlace && (
        <Button
          component={RouterLink}
          to={enlace}
          variant={accion === 'reemplazar' ? 'text' : 'contained'}
          size={accion === 'reemplazar' ? 'medium' : 'large'}
          startIcon={<Upload size={18} aria-hidden />}
          fullWidth={accion !== 'reemplazar'}
          sx={{ mt: 1.5 }}
        >
          {t(`expediente.documentos.acciones.${accion}`)}
        </Button>
      )}
    </Card>
  );
}

/** Una observación: los textos de sus motivos y, si la hay, la observación escrita (nunca quién ni qué área). */
export function Observacion({ observacion }: { observacion: ObservacionPublicaDto }) {
  const { t } = useTranslation();
  return (
    <Box sx={{ mt: 1.5, p: 1.5, borderRadius: 1, bgcolor: 'superficie.nivel2' }}>
      <Typography color="text.secondary" sx={{ fontSize: 13, mb: 0.5 }}>
        {t('expediente.observacionDel', { fecha: formatearFecha(observacion.fecha) })}
      </Typography>
      <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {observacion.motivos.map((m) => (
          <Typography component="li" key={m.codigo} sx={{ fontSize: 15, lineHeight: '22px' }}>
            {m.texto}
          </Typography>
        ))}
      </Stack>
      {observacion.observacion && <Typography sx={{ mt: 1, fontSize: 15, lineHeight: '22px', whiteSpace: 'pre-wrap' }}>“{observacion.observacion}”</Typography>}
    </Box>
  );
}
