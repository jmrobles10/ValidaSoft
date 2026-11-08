import { Box, Button, Card, Stack, Typography } from '@mui/material';
import type { ExpedientePublicoDto, RequisitoPublicoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { FileText, PencilLine, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { modoDe, reglasDe, textoReglas } from '../tipoDocumento/reglas';
import { Plantilla } from '../tipoDocumento/Plantilla';
import { requiereAtencion } from '../vigencia/vigencia';
import { AvisoVigencia, LineaVigencia } from '../vigencia/ChipVigencia';
import { Observacion } from './Observacion';
import { accionDeRequisito, observacionesDeRequisito } from './requisitos';

/**
 * Un documento que pide la empresa: su estado, la versión con su vigencia y, si le toca al tercero, qué admite (formatos,
 * tamaño, páginas), la plantilla para descargar y la acción para subirlo o llenar el formulario.
 */
export function TarjetaRequisito({ requisito, expediente, enlace }: { requisito: RequisitoPublicoDto; expediente: ExpedientePublicoDto; enlace: string | null }) {
  const { t } = useTranslation();
  const accion = accionDeRequisito(requisito, expediente);
  const observaciones = requisito.estado === 'Observado' ? observacionesDeRequisito(requisito, expediente) : [];
  const version = requisito.versionActual;
  const tipo = requisito.tipoDocumento;
  const modo = modoDe(tipo);
  // Le toca subirlo (o renovarlo porque vence): entonces se dice qué admite y se ofrece la plantilla.
  const teToca = Boolean(accion && enlace) && (accion !== 'reemplazar' || requiereAtencion(version?.vigencia));

  return (
    <Card component="li" sx={{ p: 2, borderColor: requisito.estado === 'Observado' ? 'estado.advertencia.borde' : undefined }}>
      <Stack direction="row" spacing={1.5} alignItems="flex-start">
        <Box sx={{ color: 'text.secondary', pt: 0.25 }} aria-hidden>
          <FileText size={20} />
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography component="h4" sx={{ fontWeight: 600, fontSize: 16, lineHeight: '22px' }}>
            {tipo.nombre}
          </Typography>
          <Typography color="text.secondary" sx={{ fontSize: 14 }}>
            {version
              ? t(version.clase === 'Formulario' ? 'expediente.documentos.versionFormulario' : 'expediente.documentos.version', {
                  n: String(version.n),
                  fecha: formatearFecha(version.cargadaEn),
                })
              : requisito.obligatorio
                ? t('expediente.documentos.faltaObligatorio')
                : t('expediente.documentos.faltaOpcional')}
          </Typography>
          <LineaVigencia vigencia={version?.vigencia} />
        </Box>
        <ChipEstado estado={requisito.estado} size="medium" />
      </Stack>

      <AvisoVigencia vigencia={version?.vigencia} />

      {observaciones.map((o) => (
        <Observacion key={o.fecha} observacion={o} />
      ))}

      {teToca && (Boolean(tipo.textoAyuda) || modo !== 'Formulario') && (
        <Box sx={{ mt: 1.5 }}>
          {tipo.textoAyuda && <Typography sx={{ fontSize: 15, lineHeight: '22px' }}>{tipo.textoAyuda}</Typography>}
          {modo !== 'Formulario' && (
            <Typography color="text.secondary" sx={{ fontSize: 14, mt: tipo.textoAyuda ? 0.5 : 0 }}>
              {textoReglas(reglasDe(tipo.archivo), t)}
            </Typography>
          )}
        </Box>
      )}

      {teToca && tipo.tienePlantilla && (
        <Box sx={{ mt: 1.5 }}>
          <Plantilla expedienteId={expediente.id} requisitoId={requisito.id} empresa={expediente.empresa.nombre} />
        </Box>
      )}

      {accion && enlace && (
        <Button
          component={RouterLink}
          to={enlace}
          variant={accion === 'reemplazar' ? 'text' : 'contained'}
          size={accion === 'reemplazar' ? 'medium' : 'large'}
          startIcon={modo === 'Formulario' ? <PencilLine size={18} aria-hidden /> : <Upload size={18} aria-hidden />}
          fullWidth={accion !== 'reemplazar'}
          sx={{ mt: 1.5 }}
        >
          {t(modo === 'Formulario' ? `expediente.documentos.accionesFormulario.${accion}` : `expediente.documentos.acciones.${accion}`)}
        </Button>
      )}
    </Card>
  );
}
