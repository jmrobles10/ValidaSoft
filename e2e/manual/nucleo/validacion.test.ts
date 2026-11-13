import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { MetaDeRecorrido, OpcionesDePaso } from './tipos';
import { aSlug, normalizarConfiguracion, normalizarRuta, normalizarVariantes, validarMeta, validarPaso, type Catalogo } from './validacion';

const catalogo = JSON.parse(readFileSync(new URL('../catalogo.json', import.meta.url), 'utf8')) as Catalogo;

const META: MetaDeRecorrido = {
  dominio: 'tercero',
  actor: 'Tercero',
  titulo: 'Acepta la invitación',
  resumen: 'El tercero acepta la invitación de una empresa y queda listo.',
};

const PASO: OpcionesDePaso = {
  titulo: 'Abre el enlace',
  explicacion: 'Abre el enlace del correo. Verás quién te invita y los textos que debes aceptar antes de entregar nada.',
};

describe('catálogo del manual', () => {
  it('los siete actores y los dominios existen y no se repiten', () => {
    expect(catalogo.actores.map((a) => a.nombre)).toEqual([
      'Plataforma',
      'Administrador de empresa',
      'Revisor/área',
      'Oficial de cumplimiento',
      'Tercero',
      'Sistema integrador',
      'Público',
    ]);
    const slugs = catalogo.dominios.map((d) => d.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });
});

describe('validarMeta', () => {
  it('acepta un recorrido bien declarado', () => {
    expect(validarMeta(META, catalogo, 'tercero', 'aceptar-invitacion')).toEqual([]);
  });

  it('dice qué corregir: dominio, carpeta, actor, resumen y nombre de archivo', () => {
    const errores = validarMeta({ ...META, dominio: 'inventado', actor: 'Nadie' as MetaDeRecorrido['actor'], resumen: 'corto' }, catalogo, 'tercero', 'Aceptar Invitación');
    expect(errores.join('\n')).toMatch(/dominio «inventado» no existe/);
    expect(errores.join('\n')).toMatch(/carpeta «tercero» pero declara dominio «inventado»/);
    expect(errores.join('\n')).toMatch(/actor «Nadie» no existe/);
    expect(errores.join('\n')).toMatch(/resumen del recorrido es muy corto/);
    expect(errores.join('\n')).toMatch(/minúsculas con guiones/);
  });

  it('los relacionados deben ser dominio/archivo', () => {
    expect(validarMeta({ ...META, relacionados: ['tercero/otro'] }, catalogo, 'tercero', 'x-y')).toEqual([]);
    expect(validarMeta({ ...META, relacionados: ['otro'] }, catalogo, 'tercero', 'x-y')[0]).toMatch(/dominio\/archivo/);
  });
});

describe('validarPaso', () => {
  it('acepta un paso con explicación suficiente', () => {
    expect(validarPaso(PASO, 1)).toEqual([]);
  });

  it('rechaza explicaciones cortas, larguísimas o con texto pendiente', () => {
    expect(validarPaso({ ...PASO, explicacion: 'Muy corta.' }, 3)[0]).toMatch(/Paso 3 «Abre el enlace»: la explicación es muy corta/);
    expect(validarPaso({ ...PASO, explicacion: 'x'.repeat(1500) }, 1)[0]).toMatch(/muy larga/);
    expect(validarPaso({ ...PASO, explicacion: `${PASO.explicacion} TODO completar` }, 1)[0]).toMatch(/texto pendiente/);
    // «todo» como palabra común no cuenta como texto pendiente.
    expect(validarPaso({ ...PASO, explicacion: `${PASO.explicacion} Revisa todo lo que te piden.` }, 1)).toEqual([]);
  });

  it('exige completas las variantes y la ruta de configuración reconocible', () => {
    expect(validarPaso({ ...PASO, variantes: [{ cuando: 'Eres natural', entonces: '' }] }, 1)[0]).toMatch(/«cuando» y «entonces»/);
    expect(validarPaso({ ...PASO, configuracion: { ajuste: 'Flujos', ruta: 'sin ruta' } }, 1)[0]).toMatch(/ruta de configuración/);
    expect(validarPaso({ ...PASO, configuracion: { ajuste: 'Flujos', ruta: 'Configuración › Flujos aprobatorios (/flujos)' } }, 1)).toEqual([]);
  });
});

describe('normalización', () => {
  it('las variantes y los ajustes se pueden escribir como texto', () => {
    expect(normalizarVariantes(['Cuando eres natural: ves la cédula', 'Plan básico: sin IA'])).toEqual([
      { cuando: 'eres natural', entonces: 'ves la cédula' },
      { cuando: 'Plan básico', entonces: 'sin IA' },
    ]);
    expect(normalizarConfiguracion('Plazo de cargue')).toEqual([{ ajuste: 'Plazo de cargue' }]);
    expect(normalizarConfiguracion(undefined)).toEqual([]);
  });

  it('aSlug da nombres de archivo estables', () => {
    expect(aSlug('Abre «Mi información»')).toBe('abre-mi-informacion');
    expect(aSlug('¿¿??')).toBe('paso');
    expect(aSlug('a'.repeat(80))).toHaveLength(48);
  });

  it('normalizarRuta quita identificadores, el enlace de invitación y la consulta', () => {
    expect(normalizarRuta('http://localhost:5090/app/#/e/01a1d2d1-c2c6-761f-928b-23ec1226e09a/flujos/01a1d2d1-ff43-737c-9f51-f5241df2b1ee')).toBe('/e/{empresa}/flujos/{id}');
    expect(normalizarRuta('http://localhost:5090/e/abcDEF123token?x=1')).toBe('/e/{enlace}');
    expect(normalizarRuta('http://localhost:5090/ficha/general?secreto=1')).toBe('/ficha/general');
    expect(normalizarRuta('about:blank')).toBeNull();
  });
});
