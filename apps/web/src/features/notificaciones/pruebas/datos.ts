import type { NotificacionDto } from '@validasoft/api-client';

let secuencia = 0;

/** Notificación de prueba: nueva en el área, sin leer, de hace 5 minutos respecto a 2026-11-08 12:00 UTC. */
export function notificacion(cambios: Partial<NotificacionDto> = {}): NotificacionDto {
  secuencia += 1;
  return {
    id: `n-${String(secuencia)}`,
    tipo: 'expediente.nuevo_en_area',
    titulo: 'Nuevo expediente en tu área',
    mensaje: 'VERIF-E-2026-000002: Proveedor S.A.S. (Proveedor) espera tu revisión.',
    empresaId: 'emp',
    expedienteId: 'exp-1',
    vinculoId: 'vin-1',
    folio: 'VERIF-E-2026-000002',
    enlace: '/e/emp/expedientes/exp-1',
    creadaEn: '2026-11-08T11:55:00Z',
    leidaEn: null,
    ...cambios,
  };
}
