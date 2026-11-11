import { Alert, AlertTitle, Button, Chip, Stack, Typography } from '@mui/material';
import { FileSearch } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { usePropuestasDeFicha } from './api';
import { agruparPorDocumento, type PropuestasDeDocumento } from './propuestas';
import { enlaceSeccion } from './secciones';
import { TarjetaDeDocumento } from './TarjetaDeDocumento';

/**
 * «Actualizar desde mi RUT / Cámara…» en Mi información (F5, FM-14): cuando un documento más nuevo dice algo distinto de lo que
 * tiene una sección (o no se pudo leer y la sección no lo respalda), se avisa **una sola vez por documento**, cada uno en su
 * bloque con borde (UI-9): nombre y versión, su estado en un chip, las secciones que afecta como chips (cada una lleva a su
 * formulario) y una acción pequeña a la derecha que abre la primera. No muestra nada si no hay propuestas ni si la consulta
 * falla (es una ayuda, no un requisito).
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
      <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, mt: 1.25, listStyle: 'none' }}>
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

  return (
    <TarjetaDeDocumento
      documento={documento.documento}
      version={documento.version}
      datosDistintos={documento.datosDistintos}
      noLeido={documento.hayNoLeidas}
      secciones={
        <Stack component="ul" aria-label={t('ficha.propuesta.seccionesDeDocumento', { documento: documento.documento })} direction="row" useFlexGap sx={{ p: 0, m: 0, mt: 0.75, gap: 0.5, flexWrap: 'wrap', listStyle: 'none' }}>
          {documento.propuestas.map((propuesta, i) => (
            <li key={`${propuesta.seccion}-${String(propuesta.periodo ?? '')}`}>
              <Chip component={RouterLink} to={enlaceSeccion(propuesta.seccion, propuesta.periodo)} clickable size="small" variant="outlined" label={nombres[i]} />
            </li>
          ))}
        </Stack>
      }
      acciones={
        <Button
          component={RouterLink}
          to={enlaceSeccion(primera.seccion, primera.periodo)}
          size="small"
          variant="outlined"
          aria-label={t('ficha.propuesta.actualizarAccesible', { documento: documento.documento, secciones: nombres.join(', ') })}
        >
          {t('ficha.propuesta.actualizar')}
        </Button>
      }
    />
  );
}
