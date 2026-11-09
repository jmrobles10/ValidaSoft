import { describe, expect, it } from 'vitest';
import { camposExtraidos, confianzaDe, estadoRevision, separarCamelCase, tipoNoCoincide } from './revisionIa';

const TEXTOS = { nombres: { razonSocial: 'Razón social', nombre: 'Nombre' }, si: 'Sí', no: 'No', vacio: '—' };

describe('revisión automática: interpretación de los hechos', () => {
  it('separa camelCase de las claves desconocidas', () => {
    expect(separarCamelCase('fechaNacimiento')).toBe('Fecha nacimiento');
    expect(separarCamelCase('codigoCiiu2')).toBe('Codigo ciiu 2');
    expect(separarCamelCase('numero')).toBe('Numero');
  });

  it('formatea fechas, números, booleanos y vacíos; arreglos y objetos como listas', () => {
    const campos = camposExtraidos(
      {
        razonSocial: 'Constructora Norte S.A.S.',
        fechaExpedicion: '2026-11-01',
        activoTotal: 1234567.5,
        firmadoPorContador: true,
        dv: null,
        representantesLegales: [{ nombre: 'Ana Gómez', cargo: null }],
      },
      TEXTOS,
    );
    expect(campos.map((c) => c.etiqueta)).toEqual(['Razón social', 'Fecha expedicion', 'Activo total', 'Firmado por contador', 'Dv', 'Representantes legales']);
    expect(campos[1]?.valor).toEqual({ tipo: 'texto', texto: '01/11/2026' });
    expect(campos[2]?.valor).toEqual({ tipo: 'texto', texto: '1.234.567,5' });
    expect(campos[3]?.valor).toEqual({ tipo: 'texto', texto: 'Sí' });
    expect(campos[4]?.valor).toEqual({ tipo: 'texto', texto: '—' });
    expect(campos[5]?.valor).toEqual({
      tipo: 'lista',
      elementos: [
        {
          tipo: 'objeto',
          pares: [
            { clave: 'nombre', etiqueta: 'Nombre', valor: { tipo: 'texto', texto: 'Ana Gómez' } },
            { clave: 'cargo', etiqueta: 'Cargo', valor: { tipo: 'texto', texto: '—' } },
          ],
        },
      ],
    });
  });

  it('confianza como número o texto, acotada a [0, 1]', () => {
    expect(confianzaDe({ confianza: '0.91' })).toBe(0.91);
    expect(confianzaDe({ confianza: 1.2 })).toBe(1);
    expect(confianzaDe({ confianza: 'x' })).toBeNull();
  });

  it('compara el tipo detectado sin distinguir mayúsculas y solo si hay tipo esperado', () => {
    expect(tipoNoCoincide('RUT', 'rut')).toBe(false);
    expect(tipoNoCoincide('OTRO', 'RUT')).toBe(true);
    expect(tipoNoCoincide('OTRO', null)).toBe(false);
  });

  it('sin hechos no dice nada; sin IA visible pide revisión manual', () => {
    expect(estadoRevision(null)).toBe('nada');
    const base = { conIa: false, hashVerificado: true, antivirus: 'Limpio', mimeReal: 'application/pdf', paginas: 1, comparacion: 'SinCambios', camposCambiados: [], camposCriticosCambiados: [], pendienteRevisionHumana: true };
    expect(estadoRevision(base)).toBe('sinAnalisis');
  });
});
