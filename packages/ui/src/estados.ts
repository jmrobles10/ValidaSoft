/* Validasoft: mapa canónico estado → tono, ícono y texto. Un mismo nombre se ve igual en cualquier entidad. */
import {
  Archive, Ban, CalendarX, CircleAlert, CircleCheck, CircleDashed, CircleDot, CircleEllipsis, CircleMinus, CirclePause, CircleX,
  Clock, Eye, FileUp, FolderOpen, History, Hourglass, Inbox, Lock, Mail, OctagonAlert, RefreshCw, Replace, ScanSearch,
  ShieldCheck, ShieldMinus, ShieldX, TimerOff, TriangleAlert, Undo2, Unlink, type LucideIcon,
} from 'lucide-react';

export type TonoEstado = 'encurso' | 'exito' | 'advertencia' | 'error' | 'info' | 'neutro';

export type EstadoVinculo = 'Invitado' | 'EnValidacion' | 'Activo' | 'PorVencer' | 'Vencido' | 'Bloqueado' | 'Desvinculado' | 'Anulado';
export type EstadoRequisito = 'Pendiente' | 'Cargado' | 'EnRevision' | 'Observado' | 'Aprobado' | 'Eximido' | 'NoAplica' | 'Vencido' | 'Desactualizado';
export type EstadoVersion = 'Recibida' | 'EnAnalisis' | 'Actual' | 'Superada' | 'Devuelta' | 'Retirada';
export type EstadoExpediente = 'Abierto' | 'EnRevision' | 'Observado' | 'Aprobado' | 'Rechazado' | 'Anulado' | 'Caducado';
export type EstadoCertificado = 'Vigente' | 'ConPendientes' | 'Suspendido' | 'Vencido' | 'Revocado' | 'Reemplazado';
export type EstadoAceptacion = 'AlDia' | 'Desactualizada' | 'PorVencer' | 'Vencida';
export type EstadoCuentaDesignada = 'Propuesta' | 'Aprobada' | 'Rechazada' | 'Reemplazada';

export type Estado =
  | EstadoVinculo | EstadoRequisito | EstadoVersion | EstadoExpediente | EstadoCertificado | EstadoAceptacion | EstadoCuentaDesignada;

export interface Apariencia {
  /** Clave i18n del texto visible (`estados.<Estado>`); el valor es-CO va en `etiqueta`. */
  etiqueta: string;
  tono: TonoEstado;
  icono: LucideIcon;
  /** Verbo del tooltip con fecha: "Venció el 28/09/2026". */
  verboFecha?: string;
}

export const APARIENCIA_ESTADO: Readonly<Record<Estado, Apariencia>> = {
  // Vínculo
  Invitado: { etiqueta: 'Invitado', tono: 'encurso', icono: Mail, verboFecha: 'Invitado el' },
  EnValidacion: { etiqueta: 'En validación', tono: 'encurso', icono: Hourglass, verboFecha: 'En validación desde el' },
  Activo: { etiqueta: 'Activo', tono: 'exito', icono: CircleCheck, verboFecha: 'Activo desde el' },
  PorVencer: { etiqueta: 'Por vencer', tono: 'advertencia', icono: Clock, verboFecha: 'Vence el' },
  Vencido: { etiqueta: 'Vencido', tono: 'error', icono: CalendarX, verboFecha: 'Venció el' },
  Bloqueado: { etiqueta: 'Bloqueado', tono: 'error', icono: Lock, verboFecha: 'Bloqueado el' },
  Desvinculado: { etiqueta: 'Desvinculado', tono: 'neutro', icono: Unlink, verboFecha: 'Desvinculado el' },
  Anulado: { etiqueta: 'Anulado', tono: 'neutro', icono: Ban, verboFecha: 'Anulado el' },
  // Requisito
  Pendiente: { etiqueta: 'Pendiente', tono: 'neutro', icono: CircleDashed },
  Cargado: { etiqueta: 'Cargado', tono: 'encurso', icono: FileUp, verboFecha: 'Cargado el' },
  EnRevision: { etiqueta: 'En revisión', tono: 'encurso', icono: Eye, verboFecha: 'En revisión desde el' },
  Observado: { etiqueta: 'Observado', tono: 'advertencia', icono: TriangleAlert, verboFecha: 'Observado el' },
  Aprobado: { etiqueta: 'Aprobado', tono: 'exito', icono: CircleCheck, verboFecha: 'Aprobado el' },
  Eximido: { etiqueta: 'Eximido', tono: 'neutro', icono: ShieldMinus, verboFecha: 'Eximido el' },
  NoAplica: { etiqueta: 'No aplica', tono: 'neutro', icono: CircleMinus },
  Desactualizado: { etiqueta: 'Desactualizado', tono: 'advertencia', icono: RefreshCw, verboFecha: 'Revalidando desde el' },
  // Versión
  Recibida: { etiqueta: 'Recibida', tono: 'encurso', icono: Inbox, verboFecha: 'Recibida el' },
  EnAnalisis: { etiqueta: 'En análisis', tono: 'info', icono: ScanSearch },
  Actual: { etiqueta: 'Actual', tono: 'encurso', icono: CircleDot, verboFecha: 'Actual desde el' },
  Superada: { etiqueta: 'Superada', tono: 'neutro', icono: History, verboFecha: 'Superada el' },
  Devuelta: { etiqueta: 'Devuelta', tono: 'error', icono: Undo2, verboFecha: 'Devuelta el' },
  Retirada: { etiqueta: 'Retirada', tono: 'neutro', icono: Archive, verboFecha: 'Retirada el' },
  // Expediente
  Abierto: { etiqueta: 'Abierto', tono: 'encurso', icono: FolderOpen, verboFecha: 'Abierto el' },
  Rechazado: { etiqueta: 'Rechazado', tono: 'error', icono: CircleX, verboFecha: 'Rechazado el' },
  Caducado: { etiqueta: 'Caducado', tono: 'error', icono: TimerOff, verboFecha: 'Caducó el' },
  // Certificado
  Vigente: { etiqueta: 'Vigente', tono: 'exito', icono: ShieldCheck, verboFecha: 'Vigente hasta el' },
  ConPendientes: { etiqueta: 'Con pendientes', tono: 'advertencia', icono: CircleAlert },
  Suspendido: { etiqueta: 'Suspendido', tono: 'error', icono: CirclePause, verboFecha: 'Suspendido el' },
  Revocado: { etiqueta: 'Revocado', tono: 'error', icono: ShieldX, verboFecha: 'Revocado el' },
  Reemplazado: { etiqueta: 'Reemplazado', tono: 'neutro', icono: Replace, verboFecha: 'Reemplazado el' },
  // Aceptación
  AlDia: { etiqueta: 'Al día', tono: 'exito', icono: CircleCheck },
  Desactualizada: { etiqueta: 'Desactualizada', tono: 'advertencia', icono: RefreshCw, verboFecha: 'Desactualizada desde el' },
  Vencida: { etiqueta: 'Vencida', tono: 'error', icono: CalendarX, verboFecha: 'Venció el' },
  // Cuenta designada
  Propuesta: { etiqueta: 'Propuesta', tono: 'encurso', icono: CircleEllipsis, verboFecha: 'Propuesta el' },
  Aprobada: { etiqueta: 'Aprobada', tono: 'exito', icono: CircleCheck, verboFecha: 'Aprobada el' },
  Rechazada: { etiqueta: 'Rechazada', tono: 'error', icono: CircleX, verboFecha: 'Rechazada el' },
  Reemplazada: { etiqueta: 'Reemplazada', tono: 'neutro', icono: Replace, verboFecha: 'Reemplazada el' },
};

/** `ConPendientes` bloqueante pasa a tono error y cambia el círculo por el octágono (se distingue sin color). */
export function aparienciaDe(estado: Estado, bloqueante = false): Apariencia {
  const base = APARIENCIA_ESTADO[estado];
  if (!bloqueante || base.tono !== 'advertencia') return base;
  return { ...base, tono: 'error', icono: estado === 'ConPendientes' ? OctagonAlert : base.icono };
}
