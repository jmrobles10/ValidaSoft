import { alpha, Chip, Tooltip, useTheme } from '@mui/material';
import {
  Ban,
  CalendarX,
  CircleAlert,
  CircleCheck,
  CircleDashed,
  CircleX,
  Clock,
  Hourglass,
  Info,
  Lock,
  Mail,
  Sparkles,
  TriangleAlert,
  Unlink,
  type LucideIcon,
} from 'lucide-react';
import { formatearFecha } from './formato';
import type { TonoEstado } from './tema';

/** Estados canónicos de docs/06 (dueños: docs/03 y docs/05). */
export type EstadoVinculo = 'Invitado' | 'EnValidacion' | 'Activo' | 'PorVencer' | 'Vencido' | 'Bloqueado' | 'Desvinculado' | 'Anulado';
export type EstadoRequisito =
  | 'Pendiente'
  | 'Cargado'
  | 'EnRevision'
  | 'Observado'
  | 'Aprobado'
  | 'Eximido'
  | 'NoAplica'
  | 'Vencido'
  | 'Desactualizado';
export type EstadoExpediente = 'Abierto' | 'EnRevision' | 'Observado' | 'Aprobado' | 'Rechazado' | 'Anulado' | 'Caducado';
export type EstadoCertificado = 'Vigente' | 'ConPendientes' | 'Suspendido' | 'Vencido' | 'Revocado' | 'Reemplazado';
export type EstadoVersion = 'Recibida' | 'EnAnalisis' | 'Actual' | 'Superada' | 'Devuelta' | 'Retirada';
export type EstadoAceptacion = 'AlDia' | 'Desactualizada' | 'PorVencer' | 'Vencida';

export type Estado = EstadoVinculo | EstadoRequisito | EstadoExpediente | EstadoCertificado | EstadoVersion | EstadoAceptacion;

export interface Apariencia {
  etiqueta: string;
  tono: TonoEstado;
  icono: LucideIcon;
  /** Verbo para el tooltip con fecha: "Venció el 28/09/2026". */
  verboFecha?: string;
}

/** Mapa único estado → texto, tono e ícono. Un mismo nombre se ve igual en cualquier entidad. */
export const APARIENCIA_ESTADO: Readonly<Record<Estado, Apariencia>> = {
  // Vínculo
  Invitado: { etiqueta: 'Invitado', tono: 'enCurso', icono: Mail, verboFecha: 'Invitado el' },
  EnValidacion: { etiqueta: 'En validación', tono: 'enCurso', icono: Hourglass },
  Activo: { etiqueta: 'Activo', tono: 'exito', icono: CircleCheck, verboFecha: 'Activo desde el' },
  PorVencer: { etiqueta: 'Por vencer', tono: 'advertencia', icono: Clock, verboFecha: 'Vence el' },
  Vencido: { etiqueta: 'Vencido', tono: 'error', icono: CalendarX, verboFecha: 'Venció el' },
  Bloqueado: { etiqueta: 'Bloqueado', tono: 'error', icono: Lock, verboFecha: 'Bloqueado el' },
  Desvinculado: { etiqueta: 'Desvinculado', tono: 'neutro', icono: Unlink, verboFecha: 'Desvinculado el' },
  Anulado: { etiqueta: 'Anulado', tono: 'neutro', icono: Ban, verboFecha: 'Anulado el' },
  // Requisito
  Pendiente: { etiqueta: 'Pendiente', tono: 'neutro', icono: CircleDashed },
  Cargado: { etiqueta: 'Cargado', tono: 'enCurso', icono: CircleDashed, verboFecha: 'Cargado el' },
  EnRevision: { etiqueta: 'En revisión', tono: 'enCurso', icono: Hourglass },
  Observado: { etiqueta: 'Observado', tono: 'advertencia', icono: TriangleAlert, verboFecha: 'Observado el' },
  Aprobado: { etiqueta: 'Aprobado', tono: 'exito', icono: CircleCheck, verboFecha: 'Aprobado el' },
  Eximido: { etiqueta: 'Eximido', tono: 'neutro', icono: CircleDashed },
  NoAplica: { etiqueta: 'No aplica', tono: 'neutro', icono: CircleDashed },
  Desactualizado: { etiqueta: 'Desactualizado', tono: 'advertencia', icono: CircleAlert, verboFecha: 'Desactualizado desde el' },
  // Expediente
  Abierto: { etiqueta: 'Abierto', tono: 'enCurso', icono: CircleDashed },
  Rechazado: { etiqueta: 'Rechazado', tono: 'error', icono: CircleX, verboFecha: 'Rechazado el' },
  Caducado: { etiqueta: 'Caducado', tono: 'error', icono: CalendarX, verboFecha: 'Caducó el' },
  // Certificado
  Vigente: { etiqueta: 'Vigente', tono: 'exito', icono: CircleCheck, verboFecha: 'Vigente hasta el' },
  ConPendientes: { etiqueta: 'Con pendientes', tono: 'advertencia', icono: CircleAlert },
  Suspendido: { etiqueta: 'Suspendido', tono: 'error', icono: Lock, verboFecha: 'Suspendido el' },
  Revocado: { etiqueta: 'Revocado', tono: 'error', icono: CircleX, verboFecha: 'Revocado el' },
  Reemplazado: { etiqueta: 'Reemplazado', tono: 'neutro', icono: Info },
  // Versión
  Recibida: { etiqueta: 'Recibida', tono: 'enCurso', icono: CircleDashed, verboFecha: 'Recibida el' },
  EnAnalisis: { etiqueta: 'En análisis', tono: 'info', icono: Hourglass },
  Actual: { etiqueta: 'Actual', tono: 'exito', icono: CircleCheck },
  Superada: { etiqueta: 'Superada', tono: 'neutro', icono: Info },
  Devuelta: { etiqueta: 'Devuelta', tono: 'error', icono: CircleX, verboFecha: 'Devuelta el' },
  Retirada: { etiqueta: 'Retirada', tono: 'neutro', icono: Ban },
  // Aceptación
  AlDia: { etiqueta: 'Al día', tono: 'exito', icono: CircleCheck },
  Desactualizada: { etiqueta: 'Desactualizada', tono: 'advertencia', icono: CircleAlert },
  Vencida: { etiqueta: 'Vencida', tono: 'error', icono: CalendarX, verboFecha: 'Venció el' },
};

export interface ChipEstadoProps {
  estado: Estado;
  /** Fecha que da contexto al estado; aparece en el tooltip ("Venció el 28/09/2026"). */
  fecha?: string | Date | null;
  /** Lo decidió `Sistema (IA)`: muestra la chispa. */
  automatico?: boolean;
  /** Para `ConPendientes`: pasa de informativo (ámbar) a bloqueante (rojo). */
  bloqueante?: boolean;
  size?: 'small' | 'medium';
}

/** Tono final teniendo en cuenta `bloqueante` (A-2: `ConPendientes` bloqueante se pinta como error). */
export function tonoDe(estado: Estado, bloqueante = false): TonoEstado {
  const apariencia = APARIENCIA_ESTADO[estado];
  return bloqueante && apariencia.tono === 'advertencia' ? 'error' : apariencia.tono;
}

export function textoTooltip(estado: Estado, fecha?: string | Date | null, automatico?: boolean, bloqueante?: boolean): string | null {
  const apariencia = APARIENCIA_ESTADO[estado];
  const partes: string[] = [];
  if (fecha) partes.push(`${apariencia.verboFecha ?? 'Desde el'} ${formatearFecha(fecha)}`);
  if (bloqueante) partes.push('Bloqueante');
  if (automatico) partes.push('Decidido por Sistema (IA)');
  return partes.length > 0 ? partes.join(' · ') : null;
}

export function ChipEstado({ estado, fecha, automatico = false, bloqueante = false, size = 'small' }: ChipEstadoProps) {
  const tema = useTheme();
  const apariencia = APARIENCIA_ESTADO[estado];
  const color = tema.palette.estado[tonoDe(estado, bloqueante)];
  const Icono = automatico ? Sparkles : apariencia.icono;
  const tooltip = textoTooltip(estado, fecha, automatico, bloqueante);

  const chip = (
    <Chip
      size={size}
      label={apariencia.etiqueta}
      icon={<Icono size={14} strokeWidth={1.75} aria-hidden />}
      data-estado={estado}
      aria-label={tooltip ? `${apariencia.etiqueta}. ${tooltip}` : apariencia.etiqueta}
      sx={{
        color,
        backgroundColor: alpha(color, 0.12),
        border: `1px solid ${alpha(color, 0.32)}`,
        '& .MuiChip-icon': { color },
      }}
    />
  );

  return tooltip ? <Tooltip title={tooltip}>{chip}</Tooltip> : chip;
}
