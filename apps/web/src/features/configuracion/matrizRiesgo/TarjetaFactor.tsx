import { Box, Button, Chip, IconButton, Stack, TextField, Typography } from '@mui/material';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { CampoNumero } from '../../../shared/camposTransitorios';
import { COLUMNAS_OPCION } from './estilos';
import { FilaOpcion } from './FilaOpcion';
import { erroresDe, esAutomatico, MAXIMO_OPCIONES, type BorradorFactor, type CambioFactor, type CambioOpcion, type ErrorMatriz } from './modelo';

export interface TarjetaFactorProps {
  factor: BorradorFactor;
  /** Parte del puntaje total que pesa este factor (0–100). */
  porcentaje: number;
  errores: readonly ErrorMatriz[];
  deshabilitado: boolean;
  onCambiar: (cambio: CambioFactor) => void;
  onQuitar: () => void;
  onCambiarOpcion: (opcion: string, cambio: CambioOpcion) => void;
  onAgregarOpcion: () => void;
  onQuitarOpcion: (opcion: string) => void;
}

/**
 * Un factor de la matriz como tarjeta: nombre, tipo (manual o automático), peso y sus opciones con puntaje y piso.
 * Solo los manuales se quitan y admiten opciones nuevas; los automáticos conservan las que calcula el sistema.
 */
export function TarjetaFactor({ factor: f, porcentaje, errores, deshabilitado, onCambiar, onQuitar, onCambiarOpcion, onAgregarOpcion, onQuitarOpcion }: TarjetaFactorProps) {
  const { t } = useTranslation();
  const automatico = esAutomatico(f);
  const delFactor = erroresDe(errores, { factor: f.clave });
  const nombreMostrado = f.nombre.trim() || t('matrizRiesgo.factores.sinNombre');
  const errorNombre = delFactor.find((e) => e.codigo === 'nombre-factor' || e.codigo === 'codigo-factor');

  return (
    <Box component="article" aria-label={nombreMostrado} sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.md)}px`, bgcolor: 'background.paper', p: 2, minWidth: 0, breakInside: 'avoid', mb: 1.5 }}>
      <Stack spacing={1.5}>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 88px 32px', gap: 1, alignItems: 'start' }}>
          <TextField
            label={t('matrizRiesgo.factores.nombre')}
            value={f.nombre}
            onChange={(e) => onCambiar({ nombre: e.target.value })}
            disabled={deshabilitado}
            error={Boolean(errorNombre)}
            helperText={errorNombre && t(`matrizRiesgo.validacion.${errorNombre.codigo}`)}
            slotProps={{ htmlInput: { maxLength: 120 } }}
          />
          <CampoNumero
            etiqueta={t('matrizRiesgo.factores.peso')}
            valor={f.peso}
            onCambiar={(peso) => onCambiar({ peso })}
            deshabilitado={deshabilitado}
            error={delFactor.some((e) => e.codigo === 'peso')}
            maxDigitos={3}
          />
          {!automatico && !deshabilitado ? (
            <IconButton size="small" aria-label={t('matrizRiesgo.factores.quitar', { nombre: nombreMostrado })} onClick={onQuitar} sx={{ mt: '22px' }}>
              <Trash2 size={16} aria-hidden />
            </IconButton>
          ) : (
            <Box />
          )}
        </Box>
        {delFactor.some((e) => e.codigo === 'peso') && (
          <Typography role="alert" sx={{ color: 'error.main', fontSize: 12, mt: '-8px' }}>
            {t('matrizRiesgo.validacion.peso')}
          </Typography>
        )}
        <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Chip size="small" variant="outlined" label={automatico ? t('matrizRiesgo.factores.automatico') : t('matrizRiesgo.factores.manual')} />
          <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
            {t('matrizRiesgo.factores.pesoRelativo', { porcentaje: String(porcentaje) })}
          </Typography>
        </Stack>
        {automatico ? (
          <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{f.ayuda || t('matrizRiesgo.factores.automaticoAyuda')}</Typography>
        ) : (
          <TextField
            label={t('matrizRiesgo.factores.ayudaCampo')}
            value={f.ayuda}
            onChange={(e) => onCambiar({ ayuda: e.target.value })}
            disabled={deshabilitado}
            slotProps={{ htmlInput: { maxLength: 300 } }}
          />
        )}
        <Box>
          <Box sx={{ display: { xs: 'none', sm: 'grid' }, gridTemplateColumns: COLUMNAS_OPCION, gap: 1, mb: 0.5 }} aria-hidden>
            {(['opcion', 'puntaje', 'piso'] as const).map((c) => (
              <Typography key={c} component="span" sx={{ color: 'text.secondary', fontSize: 12, fontWeight: 500 }}>
                {t(`matrizRiesgo.factores.columnas.${c}`)}
              </Typography>
            ))}
          </Box>
          <Stack spacing={1} component="ul" sx={{ listStyle: 'none', p: 0, m: 0 }}>
            {f.opciones.map((o) => (
              <Box component="li" key={o.clave}>
                <FilaOpcion
                  opcion={o}
                  errores={erroresDe(errores, { factor: f.clave, opcion: o.clave })}
                  puedeQuitar={!automatico}
                  deshabilitado={deshabilitado}
                  onCambiar={(cambio) => onCambiarOpcion(o.clave, cambio)}
                  onQuitar={() => onQuitarOpcion(o.clave)}
                />
              </Box>
            ))}
          </Stack>
          {delFactor.some((e) => e.codigo === 'opciones-cantidad') && (
            <Typography role="alert" sx={{ color: 'error.main', fontSize: 12, mt: 0.5 }}>
              {t('matrizRiesgo.validacion.opciones-cantidad')}
            </Typography>
          )}
          <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 1 }}>{t('matrizRiesgo.factores.pisoAyuda')}</Typography>
          {!automatico && !deshabilitado && (
            <Button size="small" variant="text" startIcon={<Plus size={16} aria-hidden />} onClick={onAgregarOpcion} disabled={f.opciones.length >= MAXIMO_OPCIONES} sx={{ mt: 0.5 }}>
              {t('matrizRiesgo.factores.agregarOpcion')}
            </Button>
          )}
        </Box>
      </Stack>
    </Box>
  );
}
