import { Box, FormControl, FormControlLabel, FormHelperText, FormLabel, Radio, RadioGroup, TextField, Typography } from '@mui/material';
import { useId, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { escribirCifra, leerCifra } from './cifras';

/** Vacío ⇒ `null` (sin diligenciar); el servidor recorta y normaliza el resto. */
const textoONulo = (valor: string) => (valor.trim() === '' ? null : valor);

interface PropsCampoTexto {
  etiqueta: string;
  valor: string | null | undefined;
  onCambiar: (valor: string | null) => void;
  mensaje?: string | null;
  ayuda?: string;
  tipo?: 'text' | 'email' | 'tel' | 'url' | 'date';
  inputMode?: 'text' | 'numeric' | 'email' | 'tel' | 'url';
  autoComplete?: string;
  maxLength?: number;
  deshabilitado?: boolean;
}

/** Campo de texto a todo el ancho; el mensaje del servidor (faltante o error) va en su ayuda. */
export function CampoTexto({ etiqueta, valor, onCambiar, mensaje, ayuda, tipo = 'text', inputMode, autoComplete = 'off', maxLength = 300, deshabilitado = false }: PropsCampoTexto) {
  return (
    <TextField
      label={etiqueta}
      value={valor ?? ''}
      onChange={(e) => onCambiar(textoONulo(e.target.value))}
      type={tipo}
      error={Boolean(mensaje)}
      helperText={mensaje ?? ayuda}
      disabled={deshabilitado}
      fullWidth
      slotProps={{ htmlInput: { inputMode, autoComplete, maxLength }, inputLabel: tipo === 'date' ? { shrink: true } : undefined }}
    />
  );
}

interface PropsCampoCifra {
  etiqueta: string;
  valor: number | null | undefined;
  onCambiar: (valor: number | null) => void;
  mensaje?: string | null;
  ayuda?: string;
  entero?: boolean;
  deshabilitado?: boolean;
}

/**
 * Cifra escrita como en Colombia (puntos de miles, coma decimal). Guarda el texto mientras se escribe y entrega el
 * número; lo ilegible queda vacío para el servidor y se avisa en el campo.
 */
export function CampoCifra({ etiqueta, valor, onCambiar, mensaje, ayuda, entero = false, deshabilitado = false }: PropsCampoCifra) {
  const { t } = useTranslation();
  const [texto, setTexto] = useState(() => (entero && valor !== null && valor !== undefined ? String(valor) : escribirCifra(valor)));
  const leido = leerCifra(texto);
  const invalido = leido === undefined || (entero && leido !== null && !Number.isInteger(leido));
  return (
    <TextField
      label={etiqueta}
      value={texto}
      onChange={(e) => {
        setTexto(e.target.value);
        const numero = leerCifra(e.target.value);
        onCambiar(numero === undefined || (entero && numero !== null && !Number.isInteger(numero)) ? null : numero);
      }}
      error={invalido || Boolean(mensaje)}
      helperText={invalido ? t('ficha.numeroInvalido') : (mensaje ?? ayuda)}
      disabled={deshabilitado}
      fullWidth
      slotProps={{ htmlInput: { inputMode: entero ? 'numeric' : 'decimal', autoComplete: 'off', maxLength: 30 } }}
    />
  );
}

interface PropsCampoSiNo {
  etiqueta: string;
  valor: boolean | null | undefined;
  onCambiar: (valor: boolean) => void;
  mensaje?: string | null;
  deshabilitado?: boolean;
}

/** Pregunta sí/no con dos opciones grandes; sin responder es `null` (un «No» sí es respuesta). */
export function CampoSiNo({ etiqueta, valor, onCambiar, mensaje, deshabilitado = false }: PropsCampoSiNo) {
  const { t } = useTranslation();
  const id = useId();
  return (
    <FormControl error={Boolean(mensaje)} disabled={deshabilitado}>
      <FormLabel id={`${id}-etiqueta`}>{etiqueta}</FormLabel>
      <RadioGroup
        row
        aria-labelledby={`${id}-etiqueta`}
        value={valor === null || valor === undefined ? '' : String(valor)}
        onChange={(e) => onCambiar(e.target.value === 'true')}
      >
        <FormControlLabel value="true" control={<Radio />} label={t('ficha.si')} sx={{ minHeight: 44, mr: 3 }} />
        <FormControlLabel value="false" control={<Radio />} label={t('ficha.no')} sx={{ minHeight: 44 }} />
      </RadioGroup>
      {mensaje && <FormHelperText>{mensaje}</FormHelperText>}
    </FormControl>
  );
}

/** Campos apilados en el celular y en dos columnas desde tableta. */
export function Rejilla({ children }: { children: ReactNode }) {
  return <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 2 }}>{children}</Box>;
}

/** Subtítulo de un grupo de campos dentro de la sección. */
export function Subtitulo({ children, ayuda }: { children: ReactNode; ayuda?: ReactNode }) {
  return (
    <Box>
      <Typography variant="h3" component="h2" sx={{ fontSize: 16, lineHeight: '24px', fontWeight: 600 }}>
        {children}
      </Typography>
      {ayuda && <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px', mt: 0.25 }}>{ayuda}</Typography>}
    </Box>
  );
}
