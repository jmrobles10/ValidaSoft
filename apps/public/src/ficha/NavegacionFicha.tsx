import { Box, Button, ButtonBase, Collapse, LinearProgress, Paper, Typography, useMediaQuery, useTheme } from '@mui/material';
import type { EstadoFichaDto, EstadoSeccionFichaDto } from '@validasoft/api-client';
import { ArrowLeft, CheckCircle2, ChevronDown, Circle, CircleAlert } from 'lucide-react';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { ALTO_CABECERA, cambioDeEstado, MARGEN_SUPERIOR } from './movimiento';
import { enlaceSeccion, estadoDeSeccion, filaActual, filasDelResumen, type EstadoDeSeccion } from './secciones';

interface Props {
  estado: EstadoFichaDto;
  /** Sección abierta (código) y su periodo, para marcarla. */
  seccion: string;
  periodo: number | null;
}

/**
 * Panel del wizard que acompaña cada sección: el avance y todas las secciones con el estado que calculó el servidor
 * (completa, con faltantes o sin diligenciar), marcando la abierta. En escritorio va fijo a la izquierda; en el celular
 * es una barra arriba con el paso actual que se despliega. Se actualiza solo: al guardar se vuelve a pedir el estado.
 */
export function NavegacionFicha({ estado, seccion, periodo }: Props) {
  const { t } = useTranslation();
  const escritorio = useMediaQuery(useTheme().breakpoints.up('md'));
  const [abierta, setAbierta] = useState(false);
  const idLista = useId();
  const filas = filasDelResumen(estado);
  const completas = filas.filter((f) => estadoDeSeccion(f) === 'completa').length;
  const actual = filaActual(filas, seccion, periodo);
  const avance = t('ficha.avance', { completas: String(completas), total: String(filas.length) });
  const paso = estado.orden.indexOf(seccion) + 1;
  const nombre = (fila: EstadoSeccionFichaDto) => (fila.periodo ? t('ficha.nombreConPeriodo', { nombre: fila.nombre, periodo: String(fila.periodo) }) : fila.nombre);

  return (
    <Paper
      variant="outlined"
      component="nav"
      aria-label={t('ficha.navegacion')}
      sx={{ position: 'sticky', top: { xs: ALTO_CABECERA + MARGEN_SUPERIOR.xs, sm: ALTO_CABECERA + MARGEN_SUPERIOR.sm }, zIndex: 2, overflow: 'auto', maxHeight: { md: `calc(100dvh - ${String(ALTO_CABECERA + MARGEN_SUPERIOR.sm + 16)}px)` } }}
    >
      <Box sx={{ px: 2, pt: 1.5, pb: 1.5 }}>
        <Button component={RouterLink} to="/ficha" variant="text" size="small" startIcon={<ArrowLeft size={16} aria-hidden />} sx={{ ml: -1, mb: 1, color: 'text.secondary' }}>
          {t('ficha.volverResumen')}
        </Button>
        <Typography sx={{ color: 'text.secondary', fontSize: 13, mb: 0.75 }}>{avance}</Typography>
        <LinearProgress variant="determinate" value={filas.length > 0 ? (completas / filas.length) * 100 : 0} aria-label={avance} />
      </Box>
      {!escritorio && (
        <ButtonBase
          onClick={() => setAbierta((a) => !a)}
          aria-expanded={abierta}
          aria-controls={idLista}
          sx={{ width: '100%', justifyContent: 'space-between', gap: 1, px: 2, py: 1.25, borderTop: 1, borderColor: 'divider', textAlign: 'left' }}
        >
          <Box sx={{ minWidth: 0 }}>
            {paso > 0 && <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>{t('ficha.paso', { n: String(paso), total: String(estado.orden.length) })}</Typography>}
            <Typography sx={{ fontWeight: 600, fontSize: 15, lineHeight: '22px' }}>{actual ? nombre(actual) : ''}</Typography>
          </Box>
          <Box component="span" sx={{ display: 'inline-flex', flex: 'none', transition: 'transform 150ms', transform: abierta ? 'rotate(180deg)' : 'none' }}>
            <ChevronDown size={20} aria-hidden />
          </Box>
        </ButtonBase>
      )}
      <Collapse in={escritorio || abierta} id={idLista}>
        <Box component="ol" sx={{ listStyle: 'none', m: 0, p: 0, borderTop: 1, borderColor: 'divider' }}>
          {filas.map((fila) => (
            <ItemSeccion key={`${fila.seccion}-${String(fila.periodo)}`} fila={fila} nombre={nombre(fila)} actual={fila === actual} onElegir={() => setAbierta(false)} />
          ))}
        </Box>
      </Collapse>
    </Paper>
  );
}

const COLOR: Record<EstadoDeSeccion, string> = { completa: 'success.main', incompleta: 'warning.main', 'sin-diligenciar': 'text.disabled' };
const ICONO = { completa: CheckCircle2, incompleta: CircleAlert, 'sin-diligenciar': Circle } as const;

function ItemSeccion({ fila, nombre, actual, onElegir }: { fila: EstadoSeccionFichaDto; nombre: string; actual: boolean; onElegir: () => void }) {
  const { t } = useTranslation();
  const estado = estadoDeSeccion(fila);
  const Icono = ICONO[estado];
  const detalle = estado === 'incompleta' ? t('ficha.estados.incompleta', { count: fila.faltantes.length }) : t(`ficha.estados.${estado}`);

  return (
    <Box component="li" sx={{ borderTop: 1, borderColor: 'divider', '&:first-of-type': { borderTop: 0 } }}>
      <ButtonBase
        component={RouterLink}
        to={enlaceSeccion(fila.seccion, fila.periodo)}
        onClick={onElegir}
        aria-current={actual ? 'step' : undefined}
        aria-label={t('ficha.itemNavegacion', { seccion: nombre, estado: detalle })}
        sx={{
          width: '100%',
          justifyContent: 'flex-start',
          alignItems: 'flex-start',
          gap: 1.25,
          px: 2,
          py: 1.25,
          textAlign: 'left',
          borderLeft: 3,
          borderLeftColor: actual ? 'primary.main' : 'transparent',
          bgcolor: actual ? 'action.selected' : 'transparent',
          '&:hover': { bgcolor: actual ? 'action.selected' : 'action.hover' },
          transition: 'background-color 200ms, border-color 200ms',
        }}
      >
        <Box key={estado} component="span" sx={{ display: 'inline-flex', flex: 'none', mt: '1px', color: COLOR[estado], animation: `${cambioDeEstado} 320ms ease-out` }}>
          <Icono size={18} aria-hidden />
        </Box>
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: 14, lineHeight: '20px', fontWeight: actual ? 600 : 500, transition: 'font-weight 200ms' }}>{nombre}</Typography>
          <Typography sx={{ fontSize: 12, lineHeight: '16px', color: estado === 'incompleta' ? 'warning.dark' : 'text.secondary' }}>{detalle}</Typography>
        </Box>
      </ButtonBase>
    </Box>
  );
}
