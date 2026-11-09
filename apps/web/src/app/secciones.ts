import { SCOPES, type Scope } from '@validasoft/api-client';
import { Building2, FileText, KeyRound, Link2, ListChecks, Plug, Route, Users, Webhook, Workflow, type LucideIcon } from 'lucide-react';

export interface SeccionConfiguracion {
  ruta: 'empresas' | 'usuarios' | 'areas' | 'sistemas' | 'api-keys' | 'webhooks' | 'motivos' | 'tipos-documento' | 'tipos-vinculo' | 'flujos';
  scope: Scope;
  icono: LucideIcon;
  /** Áreas y flujos son de la empresa activa; empresas, usuarios, sistemas, API keys, motivos y tipos son del grupo. */
  alcance: 'grupo' | 'empresa';
}

/** Secciones de Configuración en el orden de su barra lateral. */
export const SECCIONES_CONFIGURACION: readonly SeccionConfiguracion[] = [
  { ruta: 'empresas', scope: SCOPES.empresasAdministrar, icono: Building2, alcance: 'grupo' },
  { ruta: 'usuarios', scope: SCOPES.usuariosAdministrar, icono: Users, alcance: 'grupo' },
  { ruta: 'tipos-vinculo', scope: SCOPES.configuracionLeer, icono: Link2, alcance: 'grupo' },
  { ruta: 'tipos-documento', scope: SCOPES.configuracionLeer, icono: FileText, alcance: 'grupo' },
  { ruta: 'motivos', scope: SCOPES.configuracionLeer, icono: ListChecks, alcance: 'grupo' },
  { ruta: 'webhooks', scope: SCOPES.webhooksAdministrar, icono: Webhook, alcance: 'grupo' },
  { ruta: 'sistemas', scope: SCOPES.sistemasAdministrar, icono: Plug, alcance: 'grupo' },
  { ruta: 'api-keys', scope: SCOPES.apiKeysAdministrar, icono: KeyRound, alcance: 'grupo' },
  { ruta: 'areas', scope: SCOPES.configuracionLeer, icono: Workflow, alcance: 'empresa' },
  { ruta: 'flujos', scope: SCOPES.flujosLeer, icono: Route, alcance: 'empresa' },
];
