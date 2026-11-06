import type { AccionVinculo } from '@validasoft/api-client';
import { Ban, Lock, LockOpen, RotateCcw, Send, Unlink, type LucideIcon } from 'lucide-react';

/** Ícono de cada acción del vínculo (botón, menú y confirmación usan el mismo). */
export const ICONO_ACCION: Readonly<Record<AccionVinculo, LucideIcon>> = {
  invitar: Send,
  bloquear: Lock,
  desbloquear: LockOpen,
  desvincular: Unlink,
  reintegrar: RotateCcw,
  anular: Ban,
};

/** Máximo de caracteres del motivo o la justificación (lo que cabe con holgura en el historial). */
export const MAXIMO_TEXTO = 500;

/** Palabra que se escribe para confirmar una anulación (irreversible). */
export const PALABRA_ANULAR = 'ANULAR';
