import { Box, Button, Stack, Typography } from '@mui/material';
import type { ExpedientePublicoDto, RequisitoPublicoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { FileText, PencilLine, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { modoDe, reglasDe, textoReglas } from '../tipoDocumento/reglas';
import { Plantilla } from '../tipoDocumento/Plantilla';
import { requiereAtencion } from '../vigencia/vigencia';
import { AvisoVigencia, LineaVigencia } from '../vigencia/ChipVigencia';
import { ConfirmarRatificacion } from './ConfirmarRatificacion';
import { Observacion } from './Observacion';
import { accionDeRequisito, observacionesDeRequisito } from './requisitos';

/**
 * Un documento que pide la empresa, como fila de la lista de su grupo: nombre y estado, la versión con su vigencia y, si
 * le toca al tercero, qué admite (formatos, tamaño, páginas), la plantilla y la acción de tamaño natural para subirlo o
 * llenar el formulario.
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
    <Box component="li" sx={{ py: 1, px: { xs: 1.5, sm: 2 }, minHeight: 44 }}>
      <Stack direction="row" spacing={1.25} sx={{ alignItems: 'flex-start' }}>
        <Box sx={{ color: 'text.secondary', pt: '2px', display: 'flex' }} aria-hidden>
          <FileText size={16} />
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography component="h4" sx={{ fontWeight: 500, fontSize: 14, lineHeight: '20px' }}>
            {tipo.nombre}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {requisito.ratificadoEn
              ? t('expediente.ratificar.ratificado', { fecha: formatearFecha(requisito.ratificadoEn) })
              : version
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
        <Box sx={{ mt: 0.75, pl: 3.25 }}>
          {tipo.textoAyuda && <Typography sx={{ fontSize: 13, lineHeight: '18px' }}>{tipo.textoAyuda}</Typography>}
          {modo !== 'Formulario' && (
            <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px', mt: tipo.textoAyuda ? 0.25 : 0 }}>
              {textoReglas(reglasDe(tipo.archivo), t)}
            </Typography>
          )}
        </Box>
      )}

      {teToca && tipo.tienePlantilla && (
        <Box sx={{ mt: 1, pl: 3.25 }}>
          <Plantilla expedienteId={expediente.id} requisitoId={requisito.id} empresa={expediente.empresa.nombre} />
        </Box>
      )}

      {accion && enlace && (
        <Button
          component={RouterLink}
          to={enlace}
          variant={accion === 'reemplazar' ? 'text' : 'contained'}
          size="large"
          startIcon={modo === 'Formulario' ? <PencilLine size={16} aria-hidden /> : <Upload size={16} aria-hidden />}
          sx={{ mt: 1, ml: accion === 'reemplazar' ? 2.25 : 3.25 }}
        >
          {t(modo === 'Formulario' ? `expediente.documentos.accionesFormulario.${accion}` : `expediente.documentos.acciones.${accion}`)}
        </Button>
      )}

      {requisito.puedeRatificar && enlace && (
        <ConfirmarRatificacion expedienteId={expediente.id} requisitoId={requisito.id} documento={tipo.nombre} empresa={expediente.empresa.nombre} />
      )}
    </Box>
  );
}
