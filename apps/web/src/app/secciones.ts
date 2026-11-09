import { SCOPES, type Scope } from '@validasoft/api-client';
import { FileText, ListChecks, Plug, Route, Users, Webhook, Workflow, type LucideIcon } from 'lucide-react';

export interface SeccionConfiguracion {
  ruta: 'usuarios' | 'areas' | 'sistemas' | 'webhooks' | 'motivos' | 'tipos-documento' | 'flujos';
  scope: Scope;
  icono: LucideIcon;
  /** Áreas y flujos son de la empresa activa; usuarios, sistemas, motivos y tipos de documento son del grupo. */
  alcance: 'grupo' | 'empresa';
}

/** Secciones de Configuración en el orden de su barra lateral. */
export const SECCIONES_CONFIGURACION: readonly SeccionConfiguracion[] = [
  { ruta: 'usuarios', scope: SCOPES.usuariosAdministrar, icono: Users, alcance: 'grupo' },
  { ruta: 'sistemas', scope: SCOPES.sistemasAdministrar, icono: Plug, alcance: 'grupo' },
  { ruta: 'webhooks', scope: SCOPES.webhooksAdministrar, icono: Webhook, alcance: 'grupo' },
  { ruta: 'motivos', scope: SCOPES.configuracionLeer, icono: ListChecks, alcance: 'grupo' },
  { ruta: 'tipos-documento', scope: SCOPES.configuracionLeer, icono: FileText, alcance: 'grupo' },
  { ruta: 'areas', scope: SCOPES.configuracionLeer, icono: Workflow, alcance: 'empresa' },
  { ruta: 'flujos', scope: SCOPES.flujosLeer, icono: Route, alcance: 'empresa' },
];
