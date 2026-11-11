import { useTheme, type SxProps, type Theme } from '@mui/material';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { esES } from '@mui/x-date-pickers/locales';
import dayjs, { type Dayjs } from 'dayjs';
import 'dayjs/locale/es';
import { CalendarDays, ChevronDown, ChevronLeft, ChevronRight } from 'lucide-react';
import { useState, type ReactNode } from 'react';

/**
 * Selector de fecha de toda la aplicación (UI-7): ningún `<input type="date">` del navegador. Un campo con las secciones
 * `dd/mm/aaaa` que se escriben con el teclado y un calendario en español con el estilo del tema (`@mui/x-date-pickers` +
 * `dayjs`). El valor que entra y sale es siempre ISO `aaaa-mm-dd` (o `''` sin fecha), igual que el de un `<input type="date">`,
 * así que la API no cambia de formato.
 */

const FORMATO_ISO = 'YYYY-MM-DD';
const FORMATO_CAMPO = 'DD/MM/YYYY';
const ISO = /^\d{4}-\d{2}-\d{2}/;

/** Textos del selector en español (los del paquete, con las tildes y el `dd/mm/aaaa` que se ve en la aplicación). */
const TEXTOS = {
  ...esES.components.MuiLocalizationProvider.defaultProps.localeText,
  fieldDayPlaceholder: () => 'dd',
  fieldMonthPlaceholder: (parametros: { contentType: string }) => (parametros.contentType === 'letter' ? 'mmmm' : 'mm'),
  fieldYearPlaceholder: (parametros: { digitAmount: number }) => 'a'.repeat(parametros.digitAmount),
  openDatePickerDialogue: (fecha: string | null) => (fecha ? `Elegir fecha, la fecha elegida es ${fecha}` : 'Elegir fecha'),
  fieldClearLabel: 'Quitar la fecha',
  day: 'Día',
  weekDay: 'Día de la semana',
};

/**
 * Los íconos del campo y del calendario (lucide, como el resto de la app). Van **a nivel de módulo**: si fueran funciones en línea
 * serían un componente nuevo en cada render y el ícono se volvería a montar al salir del campo —al hacer clic en el calendario
 * justo después de escribir, el primer clic se perdía porque el botón cambiaba entre el clic y el soltar—.
 */
const ICONOS = {
  openPickerIcon: () => <CalendarDays size={16} strokeWidth={1.75} aria-hidden />,
  leftArrowIcon: () => <ChevronLeft size={18} aria-hidden />,
  rightArrowIcon: () => <ChevronRight size={18} aria-hidden />,
  switchViewIcon: () => <ChevronDown size={16} aria-hidden />,
};

const TEXTO_INVALIDA = 'Escribe una fecha válida (dd/mm/aaaa).';
const TEXTO_FUERA_DE_RANGO = 'La fecha está fuera del rango permitido.';

/** `aaaa-mm-dd` → fecha local; lo que no es una fecha real (`2026-02-31`) es `null`. */
export function fechaDeIso(iso: string | null | undefined): Dayjs | null {
  if (!iso || !ISO.test(iso)) return null;
  const dia = iso.slice(0, 10);
  const fecha = dayjs(dia);
  return fecha.isValid() && fecha.format(FORMATO_ISO) === dia ? fecha : null;
}

/** Fecha → `aaaa-mm-dd`; vacía o incompleta es `''`. */
export function isoDeFecha(fecha: Dayjs | null): string {
  return fecha?.isValid() ? fecha.format(FORMATO_ISO) : '';
}

export interface CampoFechaProps {
  etiqueta: string;
  /** Fecha ISO `aaaa-mm-dd`; `''`, `null` o sin definir es «sin fecha». */
  valor: string | null | undefined;
  /** Recibe la fecha ISO, o `''` si el campo quedó vacío o con una fecha incompleta o inexistente. */
  onCambiar: (iso: string) => void;
  requerido?: boolean;
  deshabilitado?: boolean;
  /** Marca el campo con error (la regla es del formulario); el texto va en `ayuda`. */
  error?: boolean;
  ayuda?: ReactNode;
  /** Primera y última fecha elegibles (ISO). */
  minimo?: string;
  maximo?: string;
  /** Se llama al salir del campo (para marcarlo «tocado»). */
  onBlur?: () => void;
  /** Ocupa todo el ancho de su contenedor (por omisión mide lo que su contenido, como un `TextField`). */
  fullWidth?: boolean;
  id?: string;
  name?: string;
  sx?: SxProps<Theme>;
}

/** Selector de fecha con el estilo de la aplicación: `dd/mm/aaaa`, calendario en español y valor ISO. */
export function CampoFecha({ etiqueta, valor, onCambiar, requerido = false, deshabilitado = false, error = false, ayuda, minimo, maximo, onBlur, fullWidth = false, id, name, sx }: CampoFechaProps) {
  const { app } = useTheme();
  const ISOEntrante = valor ?? '';
  // Lo que se ve (el valor de MUI X, que mientras se escribe puede estar incompleto) y el ISO que lo originó: solo cuando
  // el padre cambia el valor por su cuenta (limpiar filtros, otra fila) se vuelve a leer; lo que el propio campo emitió no.
  const [interno, setInterno] = useState<Dayjs | null>(() => fechaDeIso(ISOEntrante));
  const [conocido, setConocido] = useState(ISOEntrante);
  const [motivo, setMotivo] = useState<string | null>(null);
  const [tocado, setTocado] = useState(false);
  if (ISOEntrante !== conocido) {
    setConocido(ISOEntrante);
    setInterno(fechaDeIso(ISOEntrante));
  }

  const alCambiar = (fecha: Dayjs | null) => {
    const iso = isoDeFecha(fecha);
    setInterno(fecha);
    setConocido(iso);
    onCambiar(iso);
  };

  const textoDeError = motivo === null ? null : motivo === 'invalidDate' ? TEXTO_INVALIDA : TEXTO_FUERA_DE_RANGO;
  const conError = error || (tocado && textoDeError !== null);

  return (
    <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="es" localeText={TEXTOS}>
      <DatePicker
        value={interno}
        onChange={(fecha) => alCambiar(fecha)}
        onError={(m) => setMotivo(m)}
        format={FORMATO_CAMPO}
        label={etiqueta}
        disabled={deshabilitado}
        minDate={fechaDeIso(minimo) ?? undefined}
        maxDate={fechaDeIso(maximo) ?? undefined}
        name={name}
        slots={ICONOS}
        slotProps={{
          textField: {
            id,
            required: requerido,
            error: conError,
            helperText: error ? ayuda : tocado && textoDeError !== null ? textoDeError : ayuda,
            onBlur: () => {
              setTocado(true);
              onBlur?.();
            },
            fullWidth,
            size: app === 'web' ? 'small' : 'medium',
            // Etiqueta fija encima del campo (como el `TextField` del tema): el formato se ve siempre, no solo al enfocar.
            slotProps: { inputLabel: { shrink: true } },
            sx,
          },
          actionBar: { actions: requerido ? ['today'] : ['clear', 'today'] },
        }}
      />
    </LocalizationProvider>
  );
}
