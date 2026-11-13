import type { Locator } from '@playwright/test';

/** Quién hace lo que se documenta. Debe coincidir con `actores` de `catalogo.json`. */
export type Actor =
  | 'Plataforma'
  | 'Administrador de empresa'
  | 'Revisor/área'
  | 'Oficial de cumplimiento'
  | 'Tercero'
  | 'Sistema integrador'
  | 'Público';

/** Qué cambia en el paso según una condición (tipo de persona, configuración, rol, plan, nivel de IA…). */
export interface Variante {
  cuando: string;
  entonces: string;
}

/** Ajuste de configuración que controla el comportamiento del paso y dónde se cambia. */
export interface AjusteDeConfiguracion {
  /** Nombre del ajuste, como lo ve la persona («Reglas de vigencia»). */
  ajuste: string;
  /** Pantalla donde se cambia: ruta de la app y, si ayuda, su camino de menú («Configuración › Flujos», `/configuracion/flujos`). */
  ruta?: string;
  /** Qué provoca el ajuste en este paso. */
  efecto?: string;
}

export interface MetaDeRecorrido {
  /** Carpeta del recorrido (`e2e/manual/<dominio>/`); debe existir en `catalogo.json`. */
  dominio: string;
  actor: Actor;
  /** Título del recorrido en imperativo o infinitivo corto: «Acepta la invitación y completa tu información». */
  titulo: string;
  /** Una a tres frases: qué logra la persona y cuándo lo hace. */
  resumen: string;
  /** Qué debe existir antes (se siembra por API en la prueba): «Una empresa con un flujo de Proveedor publicado». */
  precondiciones?: string[];
  /** Otros recorridos relacionados, como `dominio/archivo` (sin `.manual.ts`). */
  relacionados?: string[];
}

export interface OpcionesDePaso {
  /** Imperativo corto: «Invita a un proveedor». */
  titulo: string;
  /** 2 a 5 frases para una persona de negocio: qué ve, qué hace, qué pasa después y qué reglas aplican. */
  explicacion: string;
  /** Qué cambia según el tipo de persona, la configuración, el rol, el plan, el nivel de IA… */
  variantes?: (Variante | string)[];
  /** Qué pantalla o ajuste controla este comportamiento, con su ruta en la app. */
  configuracion?: (AjusteDeConfiguracion | string)[] | AjusteDeConfiguracion | string;
  /** Elemento(s) del paso que se resaltan en el pantallazo. Con varios, salen numerados en el orden dado. */
  resaltar?: Locator | Locator[];
  /** También captura 390×844. Por omisión sí para el Tercero y el Público, no para los demás actores. */
  celular?: boolean;
  /** Captura la página completa en vez de solo la ventana (1280×800). Úsalo en pantallas largas, no en diálogos. */
  completa?: boolean;
  /** Si el paso muestra a propósito un indicador de carga, pon `false` para no esperar a que desaparezca. */
  esperarCarga?: boolean;
  /** Deja a la vista los avisos emergentes («Guardaste…»). Por omisión se ocultan para que no tapen la pantalla. */
  avisos?: boolean;
  /** Deja el puntero donde está (por omisión se lleva a una esquina para que ningún elemento salga en estado «hover»). Úsalo para mostrar un tooltip. */
  mantenerPuntero?: boolean;
}

export interface PasoDelManifiesto {
  orden: number;
  titulo: string;
  explicacion: string;
  variantes: Variante[];
  configuracion: AjusteDeConfiguracion[];
  /** Ruta de la app en el momento del paso, sin identificadores (`/e/{empresa}/bandeja`). */
  ruta: string | null;
  /** Rutas relativas a la carpeta del recorrido. */
  imagenes: { claro: string; celular: string | null };
  resaltados: number;
  completa: boolean;
}

export interface ManifiestoDeRecorrido {
  version: 1;
  /** `dominio/archivo`, sin `.manual.ts`. */
  id: string;
  dominio: string;
  archivo: string;
  titulo: string;
  actor: Actor;
  resumen: string;
  precondiciones: string[];
  relacionados: string[];
  generado: string;
  pasos: PasoDelManifiesto[];
}
