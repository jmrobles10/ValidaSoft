import type { ActualizarMatrizDeRiesgoDto, DebidaDiligencia, MatrizDeRiesgoDto, OrigenDelFactor } from '@validasoft/api-client';

/**
 * Reglas de la matriz de riesgo de la empresa (CU-17), las mismas que valida la API (`MatrizDeRiesgoEmpresa.Actualizar`):
 * se validan aquí antes de enviar y se calcula el puntaje de ejemplo con la misma fórmula que el servidor (promedio
 * ponderado de un decimal, nivel por límites y piso de las opciones).
 */

export const MAXIMO_FACTORES = 20;
export const MINIMO_OPCIONES = 2;
export const MAXIMO_OPCIONES = 8;
export const MESES_MAXIMOS = 60;
/** Intensidad creciente de la debida diligencia: en pantalla Simplificada, Ordinaria e Intensificada. */
export const DEBIDAS: readonly DebidaDiligencia[] = ['Simple', 'Ordinaria', 'Ampliada'];
export const NIVELES = [1, 2, 3] as const;
export type Nivel = (typeof NIVELES)[number];
/** Factores que el sistema evalúa solo: no se les agregan ni quitan opciones. */
export const CODIGOS_AUTOMATICOS: readonly string[] = ['jurisdiccion', 'coincidencias-listas', 'pep'];
const CODIGO_VALIDO = /^[a-z0-9][a-z0-9-]{0,39}$/;

export type Piso = '' | '2' | '3';

export interface BorradorOpcion {
  /** Identidad local (solo para las llaves de React). */
  clave: string;
  /** Vacío en una opción nueva: se genera del nombre al guardar. */
  codigo: string;
  nombre: string;
  puntaje: string;
  piso: Piso;
}

export interface BorradorFactor {
  clave: string;
  codigo: string;
  nombre: string;
  origen: OrigenDelFactor;
  peso: string;
  ayuda: string;
  opciones: BorradorOpcion[];
}

export interface BorradorNivel {
  nivel: Nivel;
  debidaDiligencia: DebidaDiligencia;
  meses: string;
}

export interface BorradorMatriz {
  activa: boolean;
  factores: BorradorFactor[];
  limiteBajo: string;
  limiteMedio: string;
  niveles: BorradorNivel[];
}

let secuencia = 0;
const nuevaClave = () => `k${String(++secuencia)}`;

const esNivel = (n: number): n is Nivel => n === 1 || n === 2 || n === 3;

export function esAutomatico(factor: Pick<BorradorFactor, 'origen'>): boolean {
  return factor.origen === 'Automatico';
}

export function borradorDeMatriz(matriz: MatrizDeRiesgoDto): BorradorMatriz {
  return {
    activa: matriz.activa,
    factores: matriz.factores.map((f) => ({
      clave: nuevaClave(),
      codigo: f.codigo,
      nombre: f.nombre,
      origen: f.origen,
      peso: String(f.peso),
      ayuda: f.ayuda ?? '',
      opciones: f.opciones.map((o) => ({
        clave: nuevaClave(),
        codigo: o.codigo,
        nombre: o.nombre,
        puntaje: String(o.puntaje),
        piso: o.nivelMinimo === 2 ? '2' : o.nivelMinimo === 3 ? '3' : '',
      })),
    })),
    limiteBajo: String(matriz.limiteBajo),
    limiteMedio: String(matriz.limiteMedio),
    niveles: NIVELES.map((nivel) => {
      const n = matriz.niveles.find((x) => x.nivel === nivel);
      return { nivel, debidaDiligencia: n?.debidaDiligencia ?? DEBIDAS[nivel - 1] ?? 'Simple', meses: String(n?.mesesRevision ?? 12) };
    }),
  };
}

// ── Códigos ────────────────────────────────────────────────────────────────────────────────────────────────────────

/** Del nombre a un código válido (minúsculas, números y guiones; hasta 40) que no esté en `usados`. */
export function codigoDeNombre(nombre: string, usados: ReadonlySet<string>, respaldo: string): string {
  const base =
    nombre
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .slice(0, 36)
      .replace(/-+$/g, '') || respaldo;
  let codigo = base;
  for (let n = 2; usados.has(codigo); n++) codigo = `${base}-${String(n)}`;
  return codigo;
}

// ── Del borrador al envío ──────────────────────────────────────────────────────────────────────────────────────────

const entero = (texto: string): number => {
  const n = Number.parseInt(texto, 10);
  return Number.isFinite(n) ? n : 0;
};

export function matrizADto(b: BorradorMatriz): ActualizarMatrizDeRiesgoDto {
  const codigosDeFactor = new Set(b.factores.map((f) => f.codigo).filter(Boolean));
  for (const automatico of CODIGOS_AUTOMATICOS) codigosDeFactor.add(automatico);
  return {
    activa: b.activa,
    factores: b.factores.map((f) => {
      const codigo = f.codigo || codigoDeNombre(f.nombre, codigosDeFactor, 'factor');
      codigosDeFactor.add(codigo);
      const codigosDeOpcion = new Set(f.opciones.map((o) => o.codigo).filter(Boolean));
      return {
        codigo,
        nombre: f.nombre.trim(),
        origen: f.origen,
        peso: entero(f.peso),
        ayuda: f.ayuda.trim() || null,
        opciones: f.opciones.map((o) => {
          const codigoOpcion = o.codigo || codigoDeNombre(o.nombre, codigosDeOpcion, 'opcion');
          codigosDeOpcion.add(codigoOpcion);
          return { codigo: codigoOpcion, nombre: o.nombre.trim(), puntaje: entero(o.puntaje), nivelMinimo: o.piso === '' ? null : Number(o.piso) };
        }),
      };
    }),
    limiteBajo: entero(b.limiteBajo),
    limiteMedio: entero(b.limiteMedio),
    niveles: b.niveles.map((n) => ({
      nivel: n.nivel,
      nombre: n.nivel === 1 ? 'Bajo' : n.nivel === 2 ? 'Medio' : 'Alto',
      debidaDiligencia: n.debidaDiligencia,
      mesesRevision: entero(n.meses),
    })),
  };
}

// ── Validación local ───────────────────────────────────────────────────────────────────────────────────────────────

export type CodigoErrorMatriz =
  | 'sin-factores'
  | 'demasiados-factores'
  | 'nombre-factor'
  | 'codigo-factor'
  | 'peso'
  | 'opciones-cantidad'
  | 'nombre-opcion'
  | 'codigo-opcion'
  | 'puntaje'
  | 'limites'
  | 'meses'
  | 'debida-descendente'
  | 'alto-intensificada';

export interface ErrorMatriz {
  codigo: CodigoErrorMatriz;
  /** `clave` del factor, de la opción o el nivel al que se refiere (sin esto, es de toda la matriz). */
  factor?: string;
  opcion?: string;
  nivel?: Nivel;
}

const enteroValido = (texto: string, minimo: number, maximo: number): boolean => /^\d+$/.test(texto) && Number(texto) >= minimo && Number(texto) <= maximo;

export function validarMatriz(b: BorradorMatriz): ErrorMatriz[] {
  const errores: ErrorMatriz[] = [];
  if (b.factores.length > MAXIMO_FACTORES) errores.push({ codigo: 'demasiados-factores' });
  if (b.activa && b.factores.length === 0) errores.push({ codigo: 'sin-factores' });

  const codigos = new Set<string>();
  for (const f of b.factores) {
    const nombre = f.nombre.trim();
    if (nombre === '' || nombre.length > 120) errores.push({ codigo: 'nombre-factor', factor: f.clave });
    // Un código ya usado, mal escrito o reservado a los automáticos en uno manual (los nuevos lo generan `matrizADto`).
    if (f.codigo !== '' && (!CODIGO_VALIDO.test(f.codigo) || codigos.has(f.codigo) || (!esAutomatico(f) && CODIGOS_AUTOMATICOS.includes(f.codigo))))
      errores.push({ codigo: 'codigo-factor', factor: f.clave });
    codigos.add(f.codigo || codigoDeNombre(nombre, new Set([...codigos, ...CODIGOS_AUTOMATICOS]), 'factor'));
    if (!enteroValido(f.peso, 1, 100)) errores.push({ codigo: 'peso', factor: f.clave });
    if (f.opciones.length < MINIMO_OPCIONES || f.opciones.length > MAXIMO_OPCIONES) errores.push({ codigo: 'opciones-cantidad', factor: f.clave });
    const usadas = new Set<string>();
    for (const o of f.opciones) {
      const nombreOpcion = o.nombre.trim();
      if (nombreOpcion === '' || nombreOpcion.length > 160) errores.push({ codigo: 'nombre-opcion', factor: f.clave, opcion: o.clave });
      if (!enteroValido(o.puntaje, 0, 100)) errores.push({ codigo: 'puntaje', factor: f.clave, opcion: o.clave });
      if (o.codigo !== '' && (usadas.has(o.codigo) || !CODIGO_VALIDO.test(o.codigo))) errores.push({ codigo: 'codigo-opcion', factor: f.clave, opcion: o.clave });
      usadas.add(o.codigo || codigoDeNombre(nombreOpcion, usadas, 'opcion'));
    }
  }

  const bajo = Number(b.limiteBajo);
  const medio = Number(b.limiteMedio);
  if (!enteroValido(b.limiteBajo, 1, 99) || !enteroValido(b.limiteMedio, 2, 100) || bajo >= medio) errores.push({ codigo: 'limites' });

  for (const n of b.niveles) {
    if (!enteroValido(n.meses, 1, MESES_MAXIMOS)) errores.push({ codigo: 'meses', nivel: n.nivel });
  }
  const intensidad = (n: BorradorNivel) => DEBIDAS.indexOf(n.debidaDiligencia);
  if (b.niveles.some((n, i) => i > 0 && intensidad(n) < intensidad(b.niveles[i - 1] ?? n))) errores.push({ codigo: 'debida-descendente' });
  const alto = b.niveles.find((n) => n.nivel === 3);
  if (alto && alto.debidaDiligencia !== 'Ampliada') errores.push({ codigo: 'alto-intensificada', nivel: 3 });
  return errores;
}

export function erroresDe(errores: readonly ErrorMatriz[], donde: { factor?: string; opcion?: string; nivel?: Nivel }): ErrorMatriz[] {
  return errores.filter((e) => e.factor === donde.factor && e.opcion === donde.opcion && e.nivel === donde.nivel);
}

// ── Cambios inmutables del borrador ────────────────────────────────────────────────────────────────────────────────

export function actualizarFactor(b: BorradorMatriz, clave: string, cambio: CambioFactor): BorradorMatriz {
  return { ...b, factores: b.factores.map((f) => (f.clave === clave ? { ...f, ...cambio } : f)) };
}

export type CambioOpcion = Partial<Pick<BorradorOpcion, 'nombre' | 'puntaje' | 'piso'>>;
export type CambioFactor = Partial<Pick<BorradorFactor, 'nombre' | 'peso' | 'ayuda'>>;

export function actualizarOpcion(b: BorradorMatriz, factor: string, opcion: string, cambio: CambioOpcion): BorradorMatriz {
  return { ...b, factores: b.factores.map((f) => (f.clave === factor ? { ...f, opciones: f.opciones.map((o) => (o.clave === opcion ? { ...o, ...cambio } : o)) } : f)) };
}

export function nuevaOpcion(): BorradorOpcion {
  return { clave: nuevaClave(), codigo: '', nombre: '', puntaje: '0', piso: '' };
}

export function agregarOpcion(b: BorradorMatriz, factor: string): BorradorMatriz {
  return {
    ...b,
    factores: b.factores.map((f) => (f.clave === factor && !esAutomatico(f) && f.opciones.length < MAXIMO_OPCIONES ? { ...f, opciones: [...f.opciones, nuevaOpcion()] } : f)),
  };
}

export function quitarOpcion(b: BorradorMatriz, factor: string, opcion: string): BorradorMatriz {
  return {
    ...b,
    factores: b.factores.map((f) => (f.clave === factor && !esAutomatico(f) ? { ...f, opciones: f.opciones.filter((o) => o.clave !== opcion) } : f)),
  };
}

export function agregarFactor(b: BorradorMatriz): BorradorMatriz {
  if (b.factores.length >= MAXIMO_FACTORES) return b;
  const factor: BorradorFactor = { clave: nuevaClave(), codigo: '', nombre: '', origen: 'Manual', peso: '10', ayuda: '', opciones: [nuevaOpcion(), nuevaOpcion()] };
  return { ...b, factores: [...b.factores, factor] };
}

export function quitarFactor(b: BorradorMatriz, clave: string): BorradorMatriz {
  return { ...b, factores: b.factores.filter((f) => !(f.clave === clave && !esAutomatico(f))) };
}

/**
 * Cambia la debida diligencia de un nivel sin romper la regla «la intensidad no baja al subir el riesgo»: al subir un nivel
 * arrastra hacia arriba a los siguientes; al bajarlo, a los anteriores. El nivel Alto siempre es intensificada.
 */
export function cambiarDebida(niveles: readonly BorradorNivel[], nivel: Nivel, valor: DebidaDiligencia): BorradorNivel[] {
  const posicion = DEBIDAS.indexOf(valor);
  return niveles.map((n) => {
    const actual = DEBIDAS.indexOf(n.debidaDiligencia);
    if (n.nivel === nivel) return { ...n, debidaDiligencia: valor };
    if (n.nivel > nivel && actual < posicion) return { ...n, debidaDiligencia: valor };
    if (n.nivel < nivel && actual > posicion) return { ...n, debidaDiligencia: valor };
    return n;
  });
}

export function cambiarNivel(b: BorradorMatriz, nivel: Nivel, cambio: Partial<Pick<BorradorNivel, 'meses'>> & { debidaDiligencia?: DebidaDiligencia }): BorradorMatriz {
  const { debidaDiligencia, ...resto } = cambio;
  const niveles = debidaDiligencia ? cambiarDebida(b.niveles, nivel, debidaDiligencia) : b.niveles;
  return { ...b, niveles: niveles.map((n) => (n.nivel === nivel ? { ...n, ...resto } : n)) };
}

/** Importancia relativa de un factor en el puntaje: su peso entre la suma de los pesos (0–100, sin decimales). */
export function pesoRelativo(b: BorradorMatriz, factor: BorradorFactor): number {
  const total = b.factores.reduce((suma, f) => suma + entero(f.peso), 0);
  return total === 0 ? 0 : Math.round((entero(factor.peso) * 100) / total);
}

/** Cambios sin guardar: compara lo que se enviaría (las llaves locales no cuentan). */
export function hayCambios(inicial: BorradorMatriz, actual: BorradorMatriz): boolean {
  return JSON.stringify(matrizADto(inicial)) !== JSON.stringify(matrizADto(actual));
}

// ── Tramos de la barra de límites ──────────────────────────────────────────────────────────────────────────────────

export interface Tramo {
  nivel: Nivel;
  /** Incluido. */
  desde: number;
  /** Excluido (el último llega a 100). */
  hasta: number;
}

/** Los tres tramos del puntaje; con límites inválidos se ajustan a lo posible para que la barra siempre se pueda dibujar. */
export function tramos(limiteBajo: string, limiteMedio: string): [Tramo, Tramo, Tramo] {
  const bajo = Math.min(Math.max(entero(limiteBajo), 1), 98);
  const medio = Math.min(Math.max(entero(limiteMedio), bajo + 1), 99);
  return [
    { nivel: 1, desde: 0, hasta: bajo },
    { nivel: 2, desde: bajo, hasta: medio },
    { nivel: 3, desde: medio, hasta: 100 },
  ];
}

// ── Puntaje de ejemplo ─────────────────────────────────────────────────────────────────────────────────────────────

export interface DesgloseEjemplo {
  factor: string;
  nombre: string;
  opcion: string;
  puntaje: number;
  peso: number;
}

export interface ResultadoEjemplo {
  puntaje: number;
  nivel: Nivel;
  nivelPorPuntaje: Nivel;
  /** «Factor: opción» cuando el piso de una opción subió el nivel por encima del que daba el puntaje. */
  forzadoPor: string | null;
  debidaDiligencia: DebidaDiligencia;
  mesesRevision: number;
  desglose: DesgloseEjemplo[];
}

/**
 * Aplica la matriz del borrador a una elección de opciones (`clave` del factor → `clave` de la opción; la primera opción si
 * falta). `null` si no hay ningún factor con opciones.
 */
export function calcularEjemplo(b: BorradorMatriz, elecciones: Readonly<Record<string, string>>): ResultadoEjemplo | null {
  const elegidos = b.factores.flatMap((f) => {
    const opcion = f.opciones.find((o) => o.clave === elecciones[f.clave]) ?? f.opciones[0];
    return opcion ? [{ factor: f, opcion }] : [];
  });
  if (elegidos.length === 0) return null;

  const desglose: DesgloseEjemplo[] = elegidos.map(({ factor, opcion }) => ({
    factor: factor.clave,
    nombre: factor.nombre.trim() || '—',
    opcion: opcion.nombre.trim() || '—',
    puntaje: entero(opcion.puntaje),
    peso: entero(factor.peso),
  }));
  const pesos = desglose.reduce((suma, d) => suma + d.peso, 0);
  const suma = desglose.reduce((total, d) => total + d.peso * d.puntaje, 0);
  // Promedio ponderado con un decimal, redondeando .05 hacia arriba, todo en enteros (igual que el decimal del servidor).
  const decimas = pesos === 0 ? 0 : Math.floor((suma * 20 + pesos) / (2 * pesos));
  const puntaje = decimas / 10;

  const bajo = entero(b.limiteBajo);
  const medio = entero(b.limiteMedio);
  const nivelPorPuntaje: Nivel = puntaje < bajo ? 1 : puntaje < medio ? 2 : 3;
  const piso = elegidos
    .flatMap(({ factor, opcion }, i) => (opcion.piso === '' ? [] : [{ minimo: Number(opcion.piso), puntaje: desglose[i]?.puntaje ?? 0, factor, opcion }]))
    .sort((a, c) => c.minimo - a.minimo || c.puntaje - a.puntaje)[0];
  const minimo = piso?.minimo ?? 1;
  const nivel: Nivel = esNivel(minimo) && minimo > nivelPorPuntaje ? minimo : nivelPorPuntaje;
  const configuracion = b.niveles.find((n) => n.nivel === nivel);
  return {
    puntaje,
    nivel,
    nivelPorPuntaje,
    forzadoPor: piso && nivel > nivelPorPuntaje ? `${piso.factor.nombre.trim()}: ${piso.opcion.nombre.trim()}` : null,
    debidaDiligencia: configuracion?.debidaDiligencia ?? 'Simple',
    mesesRevision: entero(configuracion?.meses ?? '12'),
    desglose,
  };
}
