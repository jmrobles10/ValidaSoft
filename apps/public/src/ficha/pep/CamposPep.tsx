import { Box, Checkbox, FormControlLabel, MenuItem, Paper, TextField, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { CampoFechaFicha, Rejilla } from '../campos';
import { TIPOS_IDENTIFICACION_PERSONA, type DeclaracionPep } from '../tipos';
import type { DatosBasicos } from './declaracion';

/** Mensaje de un campo de la declaración por su ruta (el error local traducido o el del servidor). */
export type MensajeDeCampo = (ruta: string) => string | null;

/** Lo que recibe cada sección del diálogo: el borrador, cómo cambiarlo, el mensaje de cada ruta y los datos básicos. */
export interface PropsSeccion {
  d: DeclaracionPep;
  cambiar: (cambio: Partial<DeclaracionPep>) => void;
  mensaje: MensajeDeCampo;
  basicos: DatosBasicos;
  hoy: string;
}

/** Una sección del diálogo: región con su encabezado, para que cada una se encuentre por su nombre. */
export function SeccionDeclaracion({ titulo, ayuda, children }: { titulo: string; ayuda?: string; children: ReactNode }) {
  const id = useId();
  return (
    <Box component="section" aria-labelledby={id} sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box>
        <Typography id={id} variant="h3" component="h2" sx={{ fontSize: 16, lineHeight: '24px', fontWeight: 600 }}>
          {titulo}
        </Typography>
        {ayuda && <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px', mt: 0.25 }}>{ayuda}</Typography>}
      </Box>
      {children}
    </Box>
  );
}

interface PropsTipoDocumento {
  etiqueta: string;
  valor: string | null | undefined;
  onCambiar: (valor: string | null) => void;
  mensaje?: string | null;
}

/** Tipo de documento (CC, CE, NIT…): lista de MUI con la flecha dentro. */
export function CampoTipoDocumento({ etiqueta, valor, onCambiar, mensaje }: PropsTipoDocumento) {
  return (
    <TextField select label={etiqueta} value={valor ?? ''} onChange={(e) => onCambiar(e.target.value || null)} error={Boolean(mensaje)} helperText={mensaje} fullWidth>
      {TIPOS_IDENTIFICACION_PERSONA.map((tipo) => (
        <MenuItem key={tipo} value={tipo}>
          {tipo}
        </MenuItem>
      ))}
    </TextField>
  );
}

interface PropsPeriodo {
  valor: { fecha_vinculacion?: string | null; fecha_desvinculacion?: string | null; vigente?: boolean | null };
  onCambiar: (cambio: { fecha_vinculacion?: string | null; fecha_desvinculacion?: string | null; vigente?: boolean }) => void;
  /** Mensaje de cada campo (`fecha_vinculacion`, `fecha_desvinculacion`). */
  mensaje: (campo: 'fecha_vinculacion' | 'fecha_desvinculacion') => string | null;
  hoy: string;
}

/** Fecha de vinculación y, salvo que siga vigente, de desvinculación. «Vigente» esconde la segunda. */
export function CampoPeriodo({ valor, onCambiar, mensaje, hoy }: PropsPeriodo) {
  const { t } = useTranslation();
  const vigente = valor.vigente === true;
  return (
    <Rejilla>
      <CampoFechaFicha etiqueta={t('ficha.pep.periodo.fechaVinculacion')} valor={valor.fecha_vinculacion} onCambiar={(v) => onCambiar({ fecha_vinculacion: v })} mensaje={mensaje('fecha_vinculacion')} maximo={hoy} />
      {!vigente && (
        <CampoFechaFicha etiqueta={t('ficha.pep.periodo.fechaDesvinculacion')} valor={valor.fecha_desvinculacion} onCambiar={(v) => onCambiar({ fecha_desvinculacion: v })} mensaje={mensaje('fecha_desvinculacion')} maximo={hoy} />
      )}
      <FormControlLabel
        control={<Checkbox checked={vigente} onChange={(e) => onCambiar({ vigente: e.target.checked, fecha_desvinculacion: e.target.checked ? null : valor.fecha_desvinculacion })} />}
        label={t('ficha.pep.periodo.vigente')}
        sx={{ minHeight: 44, mr: 0 }}
      />
    </Rejilla>
  );
}

/** Una fila repetible (familiar, sociedad): tarjeta con su nombre accesible. */
export function FilaDeclaracion({ nombre, children }: { nombre: string; children: ReactNode }) {
  return (
    <Paper component="li" variant="outlined" aria-label={nombre} sx={{ p: 1.5, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      {children}
    </Paper>
  );
}
