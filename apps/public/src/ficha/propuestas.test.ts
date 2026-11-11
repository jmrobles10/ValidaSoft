import type { PropuestaDeSeccionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { agruparPorDocumento } from './propuestas';

const documento = (id: string, tipo: string, version = 2) => ({ documentoId: id, version, tipoDocumento: tipo, nombreDocumento: tipo, cargadaEn: '2026-11-09T15:00:00Z' });
const propuesta = (seccion: string, doc: ReturnType<typeof documento>, cambios: Partial<PropuestaDeSeccionDto> = {}): PropuestaDeSeccionDto => ({
  seccion,
  nombreSeccion: seccion,
  periodo: null,
  documento: doc,
  disponible: true,
  motivo: null,
  campos: [{ campo: 'x', etiqueta: 'X', actual: 'a', propuesto: 'b' }],
  ...cambios,
});

describe('agruparPorDocumento', () => {
  it('junta las secciones de un mismo documento y versión, en el orden de la API', () => {
    const rut = documento('d1', 'RUT');
    const camara = documento('d2', 'CAMARA_COMERCIO');
    const grupos = agruparPorDocumento([propuesta('FICHA_GENERAL', rut), propuesta('FICHA_GENERAL', camara), propuesta('FICHA_TRIBUTARIA', rut), propuesta('FICHA_LEGAL', camara)]);

    expect(grupos.map((g) => g.documento)).toEqual(['RUT', 'Cámara de Comercio']);
    expect(grupos[0]?.propuestas.map((p) => p.seccion)).toEqual(['FICHA_GENERAL', 'FICHA_TRIBUTARIA']);
    expect(grupos[1]?.propuestas.map((p) => p.seccion)).toEqual(['FICHA_GENERAL', 'FICHA_LEGAL']);
  });

  it('suma los datos distintos de lo que se pudo leer y marca lo que no', () => {
    const rut = documento('d1', 'RUT');
    const [grupo] = agruparPorDocumento([
      propuesta('FICHA_GENERAL', rut, { campos: [{ campo: 'a', etiqueta: 'A', actual: null, propuesto: '1' }, { campo: 'b', etiqueta: 'B', actual: null, propuesto: '2' }] }),
      propuesta('FICHA_TRIBUTARIA', rut, { disponible: false, campos: [] }),
    ]);

    expect(grupo).toMatchObject({ datosDistintos: 2, hayNoLeidas: true, version: 2 });
  });

  it('dos versiones del mismo documento son dos avisos y sin propuestas no hay nada', () => {
    expect(agruparPorDocumento([propuesta('FICHA_GENERAL', documento('d1', 'RUT', 2)), propuesta('FICHA_GENERAL', documento('d1', 'RUT', 3))])).toHaveLength(2);
    expect(agruparPorDocumento([])).toEqual([]);
  });
});
