import { Alert, AlertTitle, Box, Button, Chip, Stack, Typography } from '@mui/material';
import { FileSearch } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { usePropuestasDeFicha } from './api';
import { agruparPorDocumento, type PropuestasDeDocumento } from './propuestas';
import { enlaceSeccion } from './secciones';

/**
 * «Actualizar desde mi RUT / Cámara…» en Mi información (F5, FM-14): cuando un documento más nuevo dice algo distinto de lo que
 * tiene una sección (o no se pudo leer y la sección no lo respalda), se avisa **una sola vez por documento** con las secciones que
 * afecta como chips (cada una lleva a su formulario) y una acción pequeña que abre la primera (UI-6). No muestra nada si no hay
 * propuestas ni si la consulta falla (es una ayuda, no un requisito).
 */
export function PropuestasEnResumen() {
  const { t } = useTranslation();
  const consulta = usePropuestasDeFicha();
  const documentos = agruparPorDocumento(consulta.data ?? []);
  if (documentos.length === 0) return null;

  return (
    <Alert severity="info" icon={<FileSearch size={20} aria-hidden />} aria-label={t('ficha.propuesta.resumenTitulo')} sx={{ mb: 2, '& .MuiAlert-message': { minWidth: 0, flex: 1 } }}>
      <AlertTitle>{t('ficha.propuesta.resumenTitulo')}</AlertTitle>
      <Typography sx={{ fontSize: 14 }}>{t('ficha.propuesta.resumenTexto')}</Typography>
      <Stack component="ul" spacing={1.25} sx={{ p: 0, m: 0, mt: 1, listStyle: 'none' }}>
        {documentos.map((documento) => (
          <FilaDeDocumento key={documento.clave} documento={documento} />
        ))}
      </Stack>
    </Alert>
  );
}

function FilaDeDocumento({ documento }: { documento: PropuestasDeDocumento }) {
  const { t } = useTranslation();
  const primera = documento.propuestas[0];
  if (!primera) return null;
  const nombres = documento.propuestas.map((p) => (p.periodo ? t('ficha.nombreConPeriodo', { nombre: p.nombreSeccion, periodo: String(p.periodo) }) : p.nombreSeccion));
  const resumen = [
    documento.datosDistintos > 0 ? t('ficha.propuesta.datosDistintos', { count: documento.datosDistintos }) : null,
    documento.hayNoLeidas ? t('ficha.propuesta.noLeido') : null,
  ]
    .filter(Boolean)
    .join(' · ');

  return (
    <Stack component="li" direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 0.75 }}>
      <Box sx={{ minWidth: 0, flex: '1 1 260px' }}>
        <Typography sx={{ fontSize: 14 }}>
          <Box component="span" sx={{ fontWeight: 500 }}>
            {t('ficha.propuesta.linea', { documento: documento.documento, n: String(documento.version) })}
          </Box>
          {resumen && <Box component="span" sx={{ color: 'text.secondary' }}>{` — ${resumen}`}</Box>}
        </Typography>
        <Stack component="ul" aria-label={t('ficha.propuesta.seccionesDeDocumento', { documento: documento.documento })} direction="row" useFlexGap sx={{ p: 0, m: 0, mt: 0.5, gap: 0.5, flexWrap: 'wrap', listStyle: 'none' }}>
          {documento.propuestas.map((propuesta, i) => (
            <li key={`${propuesta.seccion}-${String(propuesta.periodo ?? '')}`}>
              <Chip component={RouterLink} to={enlaceSeccion(propuesta.seccion, propuesta.periodo)} clickable size="small" variant="outlined" label={nombres[i]} />
            </li>
          ))}
        </Stack>
      </Box>
      <Button
        component={RouterLink}
        to={enlaceSeccion(primera.seccion, primera.periodo)}
        size="small"
        variant="outlined"
        aria-label={t('ficha.propuesta.actualizarAccesible', { documento: documento.documento, secciones: nombres.join(', ') })}
        sx={{ flex: 'none' }}
      >
        {t('ficha.propuesta.actualizarDesde', { documento: documento.documento })}
      </Button>
    </Stack>
  );
}
