/**
 * Punto de entrada para escribir recorridos: `import { recorrido, paso, expect } from '../nucleo';`
 * (la plantilla completa está en e2e/manual/README.md).
 */
export { expect } from '@playwright/test';
export { mostrarCodigo, type BloqueDeCodigo } from './codigo';
export { paso, recorrido, type FixturesDeRecorrido } from './manual';
export type { Actor, AjusteDeConfiguracion, MetaDeRecorrido, OpcionesDePaso, Variante } from './tipos';
