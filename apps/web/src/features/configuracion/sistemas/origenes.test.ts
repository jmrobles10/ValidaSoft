import { describe, expect, it } from 'vitest';
import { agregarOrigen, MAXIMO_ORIGENES, normalizarOrigen, origenesDeTexto } from './origenes';

describe('orígenes permitidos (misma regla que la API)', () => {
  it('normaliza como el navegador reporta el origen: minúsculas, sin barra final ni puerto por defecto', () => {
    expect(normalizarOrigen('  https://SRM.Cliente.co/ ')).toEqual({ origen: 'https://srm.cliente.co' });
    expect(normalizarOrigen('https://srm.cliente.co:443')).toEqual({ origen: 'https://srm.cliente.co' });
    expect(normalizarOrigen('https://srm.cliente.co:8443')).toEqual({ origen: 'https://srm.cliente.co:8443' });
    expect(normalizarOrigen('http://localhost:5173')).toEqual({ origen: 'http://localhost:5173' });
    expect(normalizarOrigen('http://127.0.0.1:8080')).toEqual({ origen: 'http://127.0.0.1:8080' });
  });

  it('rechaza rutas, consultas, comodines, usuarios y http fuera de localhost', () => {
    expect(normalizarOrigen('https://srm.cliente.co/compras')).toEqual({ error: 'formato' });
    expect(normalizarOrigen('https://srm.cliente.co?x=1')).toEqual({ error: 'formato' });
    expect(normalizarOrigen('https://*.cliente.co')).toEqual({ error: 'formato' });
    expect(normalizarOrigen('https://ana@srm.cliente.co')).toEqual({ error: 'formato' });
    expect(normalizarOrigen('srm.cliente.co')).toEqual({ error: 'formato' });
    expect(normalizarOrigen('ftp://srm.cliente.co')).toEqual({ error: 'formato' });
    expect(normalizarOrigen('http://srm.cliente.co')).toEqual({ error: 'solo-https' });
    expect(normalizarOrigen('   ')).toEqual({ error: 'vacio' });
  });

  it('no repite orígenes y no pasa de 20', () => {
    expect(agregarOrigen(['https://srm.cliente.co'], 'https://SRM.cliente.co/')).toEqual({ error: 'repetido' });
    const llena = Array.from({ length: MAXIMO_ORIGENES }, (_, i) => `https://s${String(i)}.cliente.co`);
    expect(agregarOrigen(llena, 'https://otro.cliente.co')).toEqual({ error: 'excesivos' });
    expect(agregarOrigen([], 'https://srm.cliente.co')).toEqual({ lista: ['https://srm.cliente.co'] });
  });

  it('al registrar un sistema lee uno por línea (o separados por coma) y señala la primera línea inválida', () => {
    expect(origenesDeTexto('https://srm.cliente.co\nhttps://SRM.cliente.co/, http://localhost:5173')).toEqual({
      origenes: ['https://srm.cliente.co', 'http://localhost:5173'],
      error: null,
    });
    expect(origenesDeTexto('https://srm.cliente.co\nhttp://marco.cliente.co')).toEqual({
      origenes: ['https://srm.cliente.co'],
      error: { linea: 'http://marco.cliente.co', error: 'solo-https' },
    });
  });
});
