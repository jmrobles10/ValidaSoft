import { Alert, AlertTitle, Box, Button, Stack, Typography } from '@mui/material';
import type { PropuestaDeSeccionDto } from '@validasoft/api-client';
import { FileSearch } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { usePropuestasDeFicha } from './api';
import { clavePropuesta, nombreCortoDelDocumento } from './propuestas';
import { enlaceSeccion } from './secciones';

/**
 * «Actualizar desde mi RUT / Cámara…» en Mi información (F5, FM-14): cuando un documento más nuevo dice algo distinto de lo que
 * tiene una sección (o no se pudo leer y la sección no lo respalda), la ofrece con un enlace a esa sección, donde el tercero ve la
 * propuesta campo por campo y decide. No muestra nada si no hay propuestas ni si la consulta falla (es una ayuda, no un requisito).
 */
export function PropuestasEnResumen() {
  const { t } = useTranslation();
  const consulta = usePropuestasDeFicha();
  const propuestas = consulta.data ?? [];
  if (propuestas.length === 0) return null;

  return (
    <Alert severity="info" icon={<FileSearch size={20} aria-hidden />} aria-label={t('ficha.propuesta.resumenTitulo')} sx={{ mb: 2 }}>
      <AlertTitle>{t('ficha.propuesta.resumenTitulo')}</AlertTitle>
      <Typography sx={{ fontSize: 14 }}>{t('ficha.propuesta.resumenTexto')}</Typography>
      <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, mt: 1, listStyle: 'none' }}>
        {propuestas.map((propuesta) => (
          <FilaDePropuesta key={clavePropuesta(propuesta)} propuesta={propuesta} />
        ))}
      </Stack>
    </Alert>
  );
}

function FilaDePropuesta({ propuesta }: { propuesta: PropuestaDeSeccionDto }) {
  const { t } = useTranslation();
  const documento = nombreCortoDelDocumento(propuesta);
  const nombre = propuesta.periodo ? t('ficha.nombreConPeriodo', { nombre: propuesta.nombreSeccion, periodo: String(propuesta.periodo) }) : propuesta.nombreSeccion;
  return (
    <Stack component="li" direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
      <Box sx={{ minWidth: 0, flex: '1 1 220px' }}>
        <Typography sx={{ fontWeight: 500, fontSize: 14 }}>{nombre}</Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
          {propuesta.disponible
            ? t('ficha.propuesta.cambios', { count: propuesta.campos.length, documento, n: String(propuesta.documento.version) })
            : t('ficha.propuesta.sinLecturaResumen', { documento, n: String(propuesta.documento.version) })}
        </Typography>
      </Box>
      <Button
        component={RouterLink}
        to={enlaceSeccion(propuesta.seccion, propuesta.periodo)}
        size="small"
        variant="outlined"
        aria-label={t('ficha.propuesta.actualizarAccesible', { documento, seccion: nombre })}
        sx={{ flex: 'none', minHeight: 44 }}
      >
        {t('ficha.propuesta.actualizarDesde', { documento })}
      </Button>
    </Stack>
  );
}
