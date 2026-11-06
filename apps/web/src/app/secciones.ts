import { SCOPES, type Scope } from '@validasoft/api-client';
import { Plug, Users, Workflow, type LucideIcon } from 'lucide-react';

export interface SeccionConfiguracion {
  ruta: 'usuarios' | 'areas' | 'sistemas';
  scope: Scope;
  icono: LucideIcon;
  /** Las áreas son de la empresa activa; usuarios y sistemas son del grupo. */
  alcance: 'grupo' | 'empresa';
}

/** Secciones de Configuración en el orden de su barra lateral. */
export const SECCIONES_CONFIGURACION: readonly SeccionConfiguracion[] = [
  { ruta: 'usuarios', scope: SCOPES.usuariosAdministrar, icono: Users, alcance: 'grupo' },
  { ruta: 'sistemas', scope: SCOPES.sistemasAdministrar, icono: Plug, alcance: 'grupo' },
  { ruta: 'areas', scope: SCOPES.configuracionLeer, icono: Workflow, alcance: 'empresa' },
];
