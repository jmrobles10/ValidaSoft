import { Alert, AlertTitle, Box, Button, Card, LinearProgress, Stack, Typography } from '@mui/material';
import { FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { nombreEmpresaDeVinculo, useSesionTercero } from '../sesion/sesionTercero';
import { mensajeFallo, mensajeProblema } from './mensajesSubida';
import { formatearTamano, REGLAS_GLOBALES, type ReglasArchivo } from './subida';
import type { EstadoSubida } from './useSubida';
import { mensajeDevolucion } from './versiones';

/** Progreso y resultado de la subida con su siguiente paso: lista, duplicado, devuelta, límite, formato o sin red. */
export function EstadoDeSubida({
  estado,
  tipo,
  onCancelar,
  onReintentar,
  onOtroArchivo,
  volver = null,
  reglas = REGLAS_GLOBALES,
}: {
  estado: Exclude<EstadoSubida, { fase: 'inicio' }>;
  tipo: string;
  onCancelar: () => void;
  onReintentar?: () => void;
  onOtroArchivo: () => void;
  /** Ruta a la que regresar (la revisión de la que vino); si la hay, es la acción principal. */
  volver?: string | null;
  /** Reglas de archivo del tipo, para explicar un rechazo con sus límites. */
  reglas?: ReglasArchivo;
}) {
  const { t } = useTranslation();
  const sesion = useSesionTercero((s) => s.sesion);

  if (estado.fase === 'problema') {
    return (
      <Alert severity="error" role="alert">
        {mensajeProblema(estado.problema, t)}
      </Alert>
    );
  }

  const archivo = (
    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
      <Box sx={{ color: 'text.secondary' }} aria-hidden>
        <FileText size={20} />
      </Box>
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontWeight: 500, wordBreak: 'break-word' }}>{estado.archivo.name}</Typography>
        <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
          {formatearTamano(estado.archivo.size)}
        </Typography>
      </Box>
    </Stack>
  );

  if (estado.fase === 'en-curso') {
    const pct = Math.round(estado.progreso * 100);
    return (
      <Card sx={{ p: 2 }}>
        {archivo}
        <LinearProgress
          variant={estado.etapa === 'subiendo' ? 'determinate' : 'indeterminate'}
          value={pct}
          aria-label={t('subir.progreso')}
          sx={{ mt: 2, height: 6 }}
        />
        <Typography aria-live="polite" sx={{ color: 'text.secondary', fontSize: 14, mt: 1 }}>
          {t(`subir.fases.${estado.etapa}`, { pct: String(pct) })}
        </Typography>
        <Button variant="text" onClick={onCancelar} sx={{ mt: 1, color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
      </Card>
    );
  }

  const otro = (
    <Button variant="outlined" onClick={onOtroArchivo} sx={{ mt: 1.5 }}>
      {t('subir.otroArchivo')}
    </Button>
  );

  if (estado.fase === 'fallo') {
    const texto = mensajeFallo(estado.fallo, estado.error, t, reglas);
    const reintentable = estado.fallo === 'red' || estado.fallo === 'vencida' || estado.fallo === 'otro';
    return (
      <Alert severity="error" role="alert">
        {texto}
        <Stack direction="row" spacing={1} sx={{ mt: 1.5 }}>
          {reintentable && onReintentar && (
            <Button variant="contained" onClick={onReintentar}>
              {t('comun.reintentar')}
            </Button>
          )}
          <Button variant="outlined" onClick={onOtroArchivo}>
            {t('subir.otroArchivo')}
          </Button>
        </Stack>
      </Alert>
    );
  }

  const r = estado.resultado;
  if (r.tipo === 'duplicado') {
    return (
      <Alert severity="info">
        <AlertTitle>{t('subir.duplicadoTitulo')}</AlertTitle>
        {t('subir.duplicado', { n: String(r.numero) })}
        <Box>{otro}</Box>
      </Alert>
    );
  }
  if (r.tipo === 'devuelta') {
    return (
      <Alert severity="error" role="alert">
        <AlertTitle>{t('subir.devueltaTitulo')}</AlertTitle>
        {mensajeDevolucion(r.version, t)}
        <Box>{otro}</Box>
      </Alert>
    );
  }

  const empresas = [...new Set(r.compartidos.map((c) => nombreEmpresaDeVinculo(sesion, c.vinculoId)).filter((n): n is string => Boolean(n)))];
  return (
    <Alert severity="success">
      <AlertTitle>{t('subir.lista', { n: String(r.version.n), tipo })}</AlertTitle>
      {empresas.length > 0 ? t('subir.listaCompartida', { empresas: empresas.join(', ') }) : t('subir.listaSinCompartir')}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.5 }}>
        {volver && (
          <Button component={RouterLink} to={volver} variant="contained">
            {t('subir.volverRevision')}
          </Button>
        )}
        <Button component={RouterLink} to="/boveda" variant={volver ? 'outlined' : 'contained'}>
          {t('subir.irBoveda')}
        </Button>
        <Button variant="outlined" onClick={onOtroArchivo}>
          {t('subir.otroArchivo')}
        </Button>
      </Stack>
    </Alert>
  );
}
