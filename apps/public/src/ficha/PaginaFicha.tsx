import { Alert, AlertTitle, Box, Button, Chip, LinearProgress, Paper, Stack, Typography } from '@mui/material';
import type { EstadoSeccionFichaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { ArrowRight, CheckCircle2, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { CabeceraPagina, ErrorRecuperable, EsqueletoTarjetas } from '../shared/componentes';
import { useEstadoFicha } from './api';
import { PropuestasEnResumen } from './PropuestasEnResumen';
import { enlaceSeccion, estadoDeSeccion, filasDelResumen, periodoPorOmision } from './secciones';

/**
 * Mi información (docs/14 §4.1): las secciones en el orden de la API con el estado que calculó el servidor (completa,
 * con sus faltantes o sin diligenciar) y «Continuar», que lleva a la siguiente sección incompleta (`siguienteSeccion`).
 */
export function PaginaFicha() {
  const { t } = useTranslation();
  const consulta = useEstadoFicha();

  if (consulta.isPending) {
    return (
      <>
        <CabeceraPagina titulo={t('ficha.titulo')} sub={t('ficha.sub')} />
        <EsqueletoTarjetas cantidad={3} />
      </>
    );
  }
  if (consulta.isError) {
    return (
      <>
        <CabeceraPagina titulo={t('ficha.titulo')} sub={t('ficha.sub')} />
        <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />
      </>
    );
  }

  const estado = consulta.data;
  const filas = filasDelResumen(estado);
  const completas = filas.filter((f) => estadoDeSeccion(f) === 'completa').length;
  const siguiente = estado.siguienteSeccion;
  const continuar = siguiente && (
    <Button
      component={RouterLink}
      to={enlaceSeccion(siguiente, siguiente === 'FICHA_FINANCIERA' ? periodoPorOmision(estado) : null)}
      variant="contained"
      size="large"
      endIcon={<ArrowRight size={18} aria-hidden />}
      sx={{ width: { xs: '100%', sm: 'auto' }, minHeight: 48 }}
    >
      {t('ficha.continuar')}
    </Button>
  );

  return (
    <>
      <CabeceraPagina titulo={t('ficha.titulo')} sub={t('ficha.sub')} accion={continuar} />
      {estado.completa && (
        <Alert severity="success" icon={<CheckCircle2 size={20} aria-hidden />} sx={{ mb: 2 }}>
          <AlertTitle>{t('ficha.completaTitulo')}</AlertTitle>
          {t('ficha.completaTexto')}
        </Alert>
      )}
      <PropuestasEnResumen />
      <Box sx={{ mb: 2 }}>
        <Typography sx={{ color: 'text.secondary', fontSize: 14, mb: 0.5 }}>{t('ficha.avance', { completas: String(completas), total: String(filas.length) })}</Typography>
        <LinearProgress variant="determinate" value={filas.length > 0 ? (completas / filas.length) * 100 : 0} aria-label={t('ficha.avance', { completas: String(completas), total: String(filas.length) })} />
      </Box>
      <Paper variant="outlined" component="ol" aria-label={t('ficha.titulo')} sx={{ p: 0, m: 0, listStyle: 'none', overflow: 'hidden' }}>
        {filas.map((fila) => (
          <FilaSeccion key={`${fila.seccion}-${String(fila.periodo)}`} fila={fila} />
        ))}
      </Paper>
    </>
  );
}

/** Una sección del resumen: nombre (y periodo), estado del servidor y el botón para abrirla. */
function FilaSeccion({ fila }: { fila: EstadoSeccionFichaDto }) {
  const { t } = useTranslation();
  const estado = estadoDeSeccion(fila);
  const nombre = fila.periodo ? t('ficha.nombreConPeriodo', { nombre: fila.nombre, periodo: String(fila.periodo) }) : fila.nombre;
  const chip =
    estado === 'completa' ? (
      <Chip size="small" color="success" variant="outlined" label={t('ficha.estados.completa')} />
    ) : estado === 'incompleta' ? (
      <Chip size="small" color="warning" variant="outlined" label={t('ficha.estados.incompleta', { count: fila.faltantes.length })} />
    ) : (
      <Chip size="small" variant="outlined" label={t('ficha.estados.sin-diligenciar')} />
    );

  return (
    <Box component="li" aria-label={nombre} sx={{ borderTop: 1, borderColor: 'divider', '&:first-of-type': { borderTop: 0 } }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', px: { xs: 1.5, sm: 2 }, py: 1.5, minHeight: 64 }}>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography sx={{ fontWeight: 500, fontSize: 15, lineHeight: '22px' }}>{nombre}</Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mt: 0.5 }}>
            {chip}
            {fila.actualizada && (
              <Typography component="span" sx={{ color: 'text.secondary', fontSize: 13 }}>
                {t('ficha.actualizada', { fecha: formatearFecha(fila.actualizada) })}
              </Typography>
            )}
          </Stack>
        </Box>
        <Button
          component={RouterLink}
          to={enlaceSeccion(fila.seccion, fila.periodo)}
          variant={estado === 'completa' ? 'text' : 'outlined'}
          endIcon={<ChevronRight size={18} aria-hidden />}
          aria-label={t('ficha.abrirSeccion', { seccion: nombre })}
          sx={{ flex: 'none', minHeight: 44 }}
        >
          {estado === 'sin-diligenciar' ? t('ficha.diligenciar') : t('ficha.abrir')}
        </Button>
      </Stack>
    </Box>
  );
}
