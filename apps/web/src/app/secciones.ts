import { SCOPES, type Scope } from '@validasoft/api-client';
import {
  Building2,
  Sparkles,
  FileText,
  KeyRound,
  Link2,
  ListChecks,
  LockKeyhole,
  Mail,
  Palette,
  Plug,
  Route,
  ShieldCheck,
  Users,
  Webhook,
  Workflow,
  type LucideIcon,
} from 'lucide-react';

export interface SeccionConfiguracion {
  ruta:
    | 'empresas'
    | 'usuarios'
    | 'areas'
    | 'sistemas'
    | 'api-keys'
    | 'webhooks'
    | 'motivos'
    | 'tipos-documento'
    | 'tipos-vinculo'
    | 'flujos'
    | 'datos-marca'
    | 'plantillas'
    | 'uso-ia'
    | 'listas-restrictivas'
    | 'accesos-soporte';
  scope: Scope;
  icono: LucideIcon;
  /** Datos y marca, plantillas de correo, plan y uso de IA, áreas y flujos son de la empresa activa; el resto (empresas, usuarios, sistemas, llaves, tipos, soporte…), del grupo. */
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
  { ruta: 'accesos-soporte', scope: SCOPES.empresasAdministrar, icono: LockKeyhole, alcance: 'grupo' },
  { ruta: 'datos-marca', scope: SCOPES.empresasAdministrar, icono: Palette, alcance: 'empresa' },
  { ruta: 'plantillas', scope: SCOPES.configuracionLeer, icono: Mail, alcance: 'empresa' },
  // El aviso de cupo de IA (IA-9) enlaza a `/e/{empresa}/uso-ia`: no cambiar la ruta sin cambiar el backend.
  { ruta: 'uso-ia', scope: SCOPES.configuracionLeer, icono: Sparkles, alcance: 'empresa' },
  // Proveedor de listas restrictivas, credenciales de la empresa y consumo del mes (VE-7, VE-11).
  { ruta: 'listas-restrictivas', scope: SCOPES.configuracionLeer, icono: ShieldCheck, alcance: 'empresa' },
  { ruta: 'areas', scope: SCOPES.configuracionLeer, icono: Workflow, alcance: 'empresa' },
  { ruta: 'flujos', scope: SCOPES.flujosLeer, icono: Route, alcance: 'empresa' },
];
