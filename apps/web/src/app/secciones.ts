import { SCOPES, type Scope } from '@validasoft/api-client';
import {
  Building2,
  ClipboardCheck,
  Gauge,
  BookOpenText,
  Sparkles,
  FileText,
  KeyRound,
  Layers,
  Link2,
  LogIn,
  ListChecks,
  LockKeyhole,
  Mail,
  MailCheck,
  Palette,
  Plug,
  Route,
  ScrollText,
  ShieldCheck,
  Timer,
  Users,
  UsersRound,
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
    | 'ingreso'
    | 'flujos'
    | 'datos-marca'
    | 'plantillas'
    | 'uso-ia'
    | 'revision-automatica'
    | 'listas-restrictivas'
    | 'matriz-riesgo'
    | 'accesos-soporte'
    | 'delegaciones'
    | 'plazos-de-revision'
    | 'ayuda-de-documentos'
    | 'plan-grupo'
    | 'habeas-data'
    | 'dominio-correo';
  scope: Scope;
  icono: LucideIcon;
  /** Datos y marca, plantillas de correo, plan y uso de IA, áreas y flujos son de la empresa activa; el resto (empresas, usuarios, sistemas, llaves, tipos, soporte…), del grupo. */
  alcance: 'grupo' | 'empresa';
}

/** Secciones de Configuración en el orden de su barra lateral. */
export const SECCIONES_CONFIGURACION: readonly SeccionConfiguracion[] = [
  { ruta: 'empresas', scope: SCOPES.empresasAdministrar, icono: Building2, alcance: 'grupo' },
  { ruta: 'usuarios', scope: SCOPES.usuariosAdministrar, icono: Users, alcance: 'grupo' },
  // Cómo entran las personas del grupo (ING-1): modos, duración de la sesión e inactividad, passkey para todos.
  { ruta: 'ingreso', scope: SCOPES.usuariosAdministrar, icono: LogIn, alcance: 'grupo' },
  { ruta: 'tipos-vinculo', scope: SCOPES.configuracionLeer, icono: Link2, alcance: 'grupo' },
  { ruta: 'tipos-documento', scope: SCOPES.configuracionLeer, icono: FileText, alcance: 'grupo' },
  { ruta: 'motivos', scope: SCOPES.configuracionLeer, icono: ListChecks, alcance: 'grupo' },
  { ruta: 'webhooks', scope: SCOPES.webhooksAdministrar, icono: Webhook, alcance: 'grupo' },
  { ruta: 'sistemas', scope: SCOPES.sistemasAdministrar, icono: Plug, alcance: 'grupo' },
  { ruta: 'api-keys', scope: SCOPES.apiKeysAdministrar, icono: KeyRound, alcance: 'grupo' },
  { ruta: 'accesos-soporte', scope: SCOPES.empresasAdministrar, icono: LockKeyhole, alcance: 'grupo' },
  // Plan y uso del grupo (OD-14, OD-17): el aviso de límite del backend enlaza a `/e/{empresa}/plan-grupo`; no cambiar la ruta sin cambiar el backend.
  { ruta: 'plan-grupo', scope: SCOPES.configuracionLeer, icono: Layers, alcance: 'grupo' },
  { ruta: 'datos-marca', scope: SCOPES.empresasAdministrar, icono: Palette, alcance: 'empresa' },
  { ruta: 'plantillas', scope: SCOPES.configuracionLeer, icono: Mail, alcance: 'empresa' },
  // Remitente propio con registros DNS verificados (OD-18…OD-22).
  { ruta: 'dominio-correo', scope: SCOPES.empresasAdministrar, icono: MailCheck, alcance: 'empresa' },
  // El aviso de cupo de IA (IA-9) enlaza a `/e/{empresa}/uso-ia`: no cambiar la ruta sin cambiar el backend.
  { ruta: 'uso-ia', scope: SCOPES.configuracionLeer, icono: Sparkles, alcance: 'empresa' },
  // Nivel de la revisión automática con IA (Manual, Asistido, Automático) y su uso del mes (ADR-0012).
  { ruta: 'revision-automatica', scope: SCOPES.configuracionLeer, icono: ClipboardCheck, alcance: 'empresa' },
  // Proveedor de listas restrictivas, credenciales de la empresa y consumo del mes (VE-7, VE-11).
  { ruta: 'listas-restrictivas', scope: SCOPES.configuracionLeer, icono: ShieldCheck, alcance: 'empresa' },
  // Factores, límites y niveles con los que se calcula el riesgo SAGRILAFT en el estudio de cumplimiento (CU-17).
  { ruta: 'matriz-riesgo', scope: SCOPES.configuracionLeer, icono: Gauge, alcance: 'empresa' },
  // Solicitudes de consulta, corrección y supresión de los terceros (Ley 1581, OD-1…OD-8); los avisos de plazo enlazan a `/e/{empresa}/habeas-data`.
  { ruta: 'habeas-data', scope: SCOPES.supresionesResolver, icono: ScrollText, alcance: 'empresa' },
  { ruta: 'areas', scope: SCOPES.configuracionLeer, icono: Workflow, alcance: 'empresa' },
  { ruta: 'flujos', scope: SCOPES.flujosLeer, icono: Route, alcance: 'empresa' },
  // Plazos de revisión (OP-10, OP-11), ayuda de cada documento por empresa (OP-15, OP-16) y delegación de la revisión por ausencias (OP-7…OP-9).
  { ruta: 'plazos-de-revision', scope: SCOPES.configuracionLeer, icono: Timer, alcance: 'empresa' },
  { ruta: 'ayuda-de-documentos', scope: SCOPES.configuracionLeer, icono: BookOpenText, alcance: 'empresa' },
  { ruta: 'delegaciones', scope: SCOPES.expedientesRevisar, icono: UsersRound, alcance: 'empresa' },
];
