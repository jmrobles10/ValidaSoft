import type { MatrizDeRiesgoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import {
  actualizarFactor,
  actualizarOpcion,
  agregarFactor,
  agregarOpcion,
  borradorDeMatriz,
  calcularEjemplo,
  cambiarDebida,
  cambiarNivel,
  codigoDeNombre,
  erroresDe,
  hayCambios,
  matrizADto,
  pesoRelativo,
  quitarFactor,
  quitarOpcion,
  tramos,
  validarMatriz,
  type BorradorMatriz,
} from './modelo';

/** La plantilla que ofrece el servidor a una empresa sin matriz (`MatrizDeRiesgoEmpresa.PorDefecto`). */
const PLANTILLA: MatrizDeRiesgoDto = {
  activa: false,
  esPorDefecto: true,
  version: 0,
  actualizadaEn: null,
  limiteBajo: 34,
  limiteMedio: 67,
  niveles: [
    { nivel: 1, nombre: 'Bajo', debidaDiligencia: 'Simple', mesesRevision: 24 },
    { nivel: 2, nombre: 'Medio', debidaDiligencia: 'Ordinaria', mesesRevision: 12 },
    { nivel: 3, nombre: 'Alto', debidaDiligencia: 'Ampliada', mesesRevision: 6 },
  ],
  factores: [
    {
      codigo: 'jurisdiccion',
      nombre: 'Jurisdicción del tercero',
      origen: 'Automatico',
      peso: 15,
      ayuda: 'País de la identificación del tercero.',
      opciones: [
        { codigo: 'nacional', nombre: 'Colombia', puntaje: 0 },
        { codigo: 'extranjero', nombre: 'Otro país', puntaje: 70 },
      ],
    },
    {
      codigo: 'coincidencias-listas',
      nombre: 'Coincidencias en listas restrictivas',
      origen: 'Automatico',
      peso: 30,
      opciones: [
        { codigo: 'sin-coincidencias', nombre: 'Sin coincidencias', puntaje: 0 },
        { codigo: 'descartadas', nombre: 'Coincidencias descartadas', puntaje: 30 },
        { codigo: 'por-analizar', nombre: 'Coincidencias por analizar', puntaje: 70, nivelMinimo: 2 },
        { codigo: 'confirmadas', nombre: 'Coincidencia confirmada', puntaje: 100, nivelMinimo: 3 },
      ],
    },
    {
      codigo: 'sector',
      nombre: 'Sector o actividad económica',
      origen: 'Manual',
      peso: 55,
      opciones: [
        { codigo: 'bajo', nombre: 'Bajo', puntaje: 0 },
        { codigo: 'medio', nombre: 'Medio', puntaje: 50 },
        { codigo: 'alto', nombre: 'Alto', puntaje: 100 },
      ],
    },
  ],
};

const plantilla = (): BorradorMatriz => borradorDeMatriz(PLANTILLA);
const factor = (b: BorradorMatriz, codigo: string) => {
  const f = b.factores.find((x) => x.codigo === codigo);
  if (!f) throw new Error(`Falta ${codigo}`);
  return f;
};
const opcion = (b: BorradorMatriz, codigoFactor: string, codigo: string) => {
  const o = factor(b, codigoFactor).opciones.find((x) => x.codigo === codigo);
  if (!o) throw new Error(`Falta ${codigo}`);
  return o;
};

describe('Matriz de riesgo: del servidor al borrador y de vuelta', () => {
  it('la plantilla vuelve igual al enviarla (los pisos ausentes son null y las ayudas vacías, null)', () => {
    const dto = matrizADto(plantilla());
    expect(dto.activa).toBe(false);
    expect(dto.limiteBajo).toBe(34);
    expect(dto.niveles.map((n) => [n.nivel, n.nombre, n.debidaDiligencia, n.mesesRevision])).toEqual([
      [1, 'Bajo', 'Simple', 24],
      [2, 'Medio', 'Ordinaria', 12],
      [3, 'Alto', 'Ampliada', 6],
    ]);
    expect(dto.factores[1]?.opciones.map((o) => o.nivelMinimo)).toEqual([null, null, 2, 3]);
    expect(dto.factores[1]?.ayuda).toBeNull();
    expect(dto.factores[0]?.ayuda).toBe('País de la identificación del tercero.');
  });

  it('los factores y opciones nuevos reciben un código del nombre, sin repetir ni usar los de los automáticos', () => {
    let b = agregarFactor(plantilla());
    const nuevo = b.factores.at(-1);
    if (!nuevo) throw new Error('Sin factor nuevo');
    b = actualizarFactor(b, nuevo.clave, { nombre: 'PEP' });
    b = actualizarOpcion(b, nuevo.clave, nuevo.opciones[0]?.clave ?? '', { nombre: 'Sí, hay' });
    b = actualizarOpcion(b, nuevo.clave, nuevo.opciones[1]?.clave ?? '', { nombre: 'Sí, hay' });
    const dto = matrizADto(b);
    const enviado = dto.factores.at(-1);
    expect(enviado?.codigo).toBe('pep-2');
    expect(enviado?.opciones.map((o) => o.codigo)).toEqual(['si-hay', 'si-hay-2']);
  });

  it('codigoDeNombre quita tildes, signos y se corta a 36 caracteres', () => {
    expect(codigoDeNombre('Tipo y volumen de la operación', new Set(), 'factor')).toBe('tipo-y-volumen-de-la-operacion');
    expect(codigoDeNombre('¿Operación en efectivo?', new Set(['operacion-en-efectivo']), 'factor')).toBe('operacion-en-efectivo-2');
    expect(codigoDeNombre('!!!', new Set(), 'opcion')).toBe('opcion');
    expect(codigoDeNombre('a'.repeat(80), new Set(), 'x')).toHaveLength(36);
  });

  it('hayCambios compara lo que se enviaría', () => {
    const inicial = plantilla();
    expect(hayCambios(inicial, plantilla())).toBe(false);
    expect(hayCambios(inicial, { ...plantilla(), activa: true })).toBe(true);
    const b = plantilla();
    expect(hayCambios(b, actualizarFactor(b, factor(b, 'sector').clave, { peso: '40' }))).toBe(true);
  });
});

describe('Matriz de riesgo: validación local', () => {
  it('la plantilla es válida', () => {
    expect(validarMatriz(plantilla())).toEqual([]);
  });

  it('el peso va de 1 a 100 y el puntaje de 0 a 100', () => {
    let b = plantilla();
    const sector = factor(b, 'sector');
    b = actualizarFactor(b, sector.clave, { peso: '0' });
    b = actualizarOpcion(b, sector.clave, opcion(b, 'sector', 'alto').clave, { puntaje: '101' });
    const errores = validarMatriz(b);
    expect(erroresDe(errores, { factor: sector.clave }).map((e) => e.codigo)).toEqual(['peso']);
    expect(erroresDe(errores, { factor: sector.clave, opcion: opcion(b, 'sector', 'alto').clave }).map((e) => e.codigo)).toEqual(['puntaje']);
  });

  it('un factor manual lleva de 2 a 8 opciones y los automáticos no cambian las suyas', () => {
    let b = plantilla();
    const sector = factor(b, 'sector');
    for (let i = 0; i < 10; i++) b = agregarOpcion(b, sector.clave);
    expect(factor(b, 'sector').opciones).toHaveLength(8);
    b = quitarOpcion(b, sector.clave, factor(b, 'sector').opciones[0]?.clave ?? '');
    expect(factor(b, 'sector').opciones).toHaveLength(7);
    const jurisdiccion = factor(b, 'jurisdiccion');
    expect(agregarOpcion(b, jurisdiccion.clave)).toEqual(b);
    expect(quitarOpcion(b, jurisdiccion.clave, jurisdiccion.opciones[0]?.clave ?? '')).toEqual(b);
    expect(quitarFactor(b, jurisdiccion.clave).factores).toHaveLength(3);
    expect(quitarFactor(b, sector.clave).factores).toHaveLength(2);
  });

  it('con una sola opción o sin nombre se marca, y una matriz activa necesita factores', () => {
    let b = plantilla();
    const sector = factor(b, 'sector');
    b = quitarOpcion(quitarOpcion(b, sector.clave, opcion(b, 'sector', 'bajo').clave), sector.clave, opcion(b, 'sector', 'medio').clave);
    b = actualizarFactor(b, sector.clave, { nombre: '  ' });
    expect(erroresDe(validarMatriz(b), { factor: sector.clave }).map((e) => e.codigo)).toEqual(['nombre-factor', 'opciones-cantidad']);
    expect(validarMatriz({ ...plantilla(), activa: true, factores: [] }).map((e) => e.codigo)).toEqual(['sin-factores']);
    let muchos = plantilla();
    for (let i = 0; i < 20; i++) muchos = agregarFactor(muchos);
    expect(muchos.factores).toHaveLength(20);
  });

  it('los límites son enteros de 1 a 99 y de 2 a 100, con Bajo menor que Medio', () => {
    expect(validarMatriz({ ...plantilla(), limiteBajo: '70', limiteMedio: '60' }).map((e) => e.codigo)).toEqual(['limites']);
    expect(validarMatriz({ ...plantilla(), limiteBajo: '', limiteMedio: '67' }).map((e) => e.codigo)).toEqual(['limites']);
    expect(validarMatriz({ ...plantilla(), limiteBajo: '99', limiteMedio: '100' })).toEqual([]);
  });

  it('el nivel Alto exige intensificada y la intensidad no baja al subir el nivel', () => {
    const b = plantilla();
    const alto = { ...b, niveles: b.niveles.map((n) => (n.nivel === 3 ? { ...n, debidaDiligencia: 'Ordinaria' as const } : n)) };
    expect(validarMatriz(alto).map((e) => e.codigo)).toEqual(['alto-intensificada']);
    const descendente = { ...b, niveles: b.niveles.map((n) => (n.nivel === 1 ? { ...n, debidaDiligencia: 'Ampliada' as const } : n)) };
    expect(validarMatriz(descendente).map((e) => e.codigo)).toEqual(['debida-descendente']);
    expect(validarMatriz(cambiarNivel(b, 1, { meses: '61' })).map((e) => [e.codigo, e.nivel])).toEqual([['meses', 1]]);
  });

  it('cambiarDebida arrastra a los demás niveles para no romper el orden', () => {
    const niveles = plantilla().niveles;
    expect(cambiarDebida(niveles, 1, 'Ampliada').map((n) => n.debidaDiligencia)).toEqual(['Ampliada', 'Ampliada', 'Ampliada']);
    expect(cambiarDebida(niveles, 2, 'Simple').map((n) => n.debidaDiligencia)).toEqual(['Simple', 'Simple', 'Ampliada']);
    expect(cambiarDebida(niveles, 2, 'Ampliada').map((n) => n.debidaDiligencia)).toEqual(['Simple', 'Ampliada', 'Ampliada']);
  });
});

describe('Matriz de riesgo: tramos, pesos y puntaje de ejemplo', () => {
  it('los tres tramos suman 100 y se ajustan con límites imposibles', () => {
    expect(tramos('34', '67')).toEqual([
      { nivel: 1, desde: 0, hasta: 34 },
      { nivel: 2, desde: 34, hasta: 67 },
      { nivel: 3, desde: 67, hasta: 100 },
    ]);
    const roto = tramos('90', '20');
    expect(roto[1].hasta).toBeGreaterThan(roto[1].desde);
    expect(roto[2].hasta).toBe(100);
  });

  it('cada peso es una parte del total', () => {
    const b = plantilla();
    expect(factor(b, 'jurisdiccion').peso).toBe('15');
    expect(pesoRelativo(b, factor(b, 'jurisdiccion'))).toBe(15);
    expect(pesoRelativo(b, factor(b, 'sector'))).toBe(55);
    expect(pesoRelativo({ ...b, factores: [] }, factor(b, 'sector'))).toBe(0);
  });

  it('sin elegir nada toma la primera opción de cada factor: puntaje 0, nivel Bajo y su debida diligencia', () => {
    const resultado = calcularEjemplo(plantilla(), {});
    expect(resultado).toMatchObject({ puntaje: 0, nivel: 1, nivelPorPuntaje: 1, forzadoPor: null, debidaDiligencia: 'Simple', mesesRevision: 24 });
  });

  it('el promedio ponderado va a un decimal y el .05 sube (igual que el servidor)', () => {
    const b = plantilla();
    // (15·70 + 30·0 + 55·50) / 100 = 38,0 → Medio.
    const medio = calcularEjemplo(b, { [factor(b, 'jurisdiccion').clave]: opcion(b, 'jurisdiccion', 'extranjero').clave, [factor(b, 'sector').clave]: opcion(b, 'sector', 'medio').clave });
    expect(medio).toMatchObject({ puntaje: 38, nivel: 2, debidaDiligencia: 'Ordinaria', mesesRevision: 12 });
    // Pesos 15, 30, 55 con puntajes 70, 30, 100 → (1050 + 900 + 5500) / 100 = 74,5 → Alto por puntaje.
    const alto = calcularEjemplo(b, {
      [factor(b, 'jurisdiccion').clave]: opcion(b, 'jurisdiccion', 'extranjero').clave,
      [factor(b, 'coincidencias-listas').clave]: opcion(b, 'coincidencias-listas', 'descartadas').clave,
      [factor(b, 'sector').clave]: opcion(b, 'sector', 'alto').clave,
    });
    expect(alto).toMatchObject({ puntaje: 74.5, nivel: 3, forzadoPor: null });
    // (1·5 + 3·0 + 3·0) / 7 = 0,714… → 0,7.
    const ponderado: BorradorMatriz = {
      ...b,
      factores: b.factores.map((f, i) => ({ ...f, peso: i === 0 ? '1' : '3', opciones: f.opciones.map((o) => ({ ...o, puntaje: i === 0 ? '5' : '0' })) })),
    };
    expect(calcularEjemplo(ponderado, {})?.puntaje).toBe(0.7);
    // (7·3 + 13·0) / 20 = 1,05 → 1,1: el .05 sube.
    const mitad: BorradorMatriz = {
      ...b,
      factores: b.factores.slice(0, 2).map((f, i) => ({ ...f, peso: i === 0 ? '7' : '13', opciones: f.opciones.map((o) => ({ ...o, puntaje: i === 0 ? '3' : '0', piso: '' as const })) })),
    };
    expect(calcularEjemplo(mitad, {})?.puntaje).toBe(1.1);
  });

  it('el piso de una opción sube el nivel aunque el puntaje sea bajo y dice cuál fue', () => {
    const b = plantilla();
    const resultado = calcularEjemplo(b, { [factor(b, 'coincidencias-listas').clave]: opcion(b, 'coincidencias-listas', 'confirmadas').clave });
    // (30·100) / 100 = 30 → Bajo por puntaje, pero «confirmada» fija piso 3.
    expect(resultado).toMatchObject({ puntaje: 30, nivelPorPuntaje: 1, nivel: 3, debidaDiligencia: 'Ampliada', mesesRevision: 6 });
    expect(resultado?.forzadoPor).toBe('Coincidencias en listas restrictivas: Coincidencia confirmada');
    // Si hay dos pisos gana el más alto.
    const porAnalizar = calcularEjemplo(b, { [factor(b, 'coincidencias-listas').clave]: opcion(b, 'coincidencias-listas', 'por-analizar').clave });
    expect(porAnalizar).toMatchObject({ nivel: 2, nivelPorPuntaje: 1 });
  });

  it('sin factores no hay ejemplo', () => {
    expect(calcularEjemplo({ ...plantilla(), factores: [] }, {})).toBeNull();
  });
});
