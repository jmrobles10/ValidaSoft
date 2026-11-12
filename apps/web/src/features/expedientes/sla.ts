import type { EstadoDeSla } from '@validasoft/api-client';
import type { Estado } from '@validasoft/ui';

/**
 * Estado canónico con el que se dibuja cada estado del plazo de revisión (el texto sale de `sla.indicador.estados`): a tiempo es
 * «Al día» (verde), por vencer es advertencia y vencido es error, igual que en el resto del sistema.
 */
export const ESTADO_CANONICO_SLA: Readonly<Record<EstadoDeSla, Estado>> = { EnTiempo: 'AlDia', PorVencer: 'PorVencer', Vencido: 'Vencido' };
