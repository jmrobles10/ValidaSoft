import { describe, expect, it } from 'vitest';
import { adjuntoDePrueba } from './datosPrueba';
import { coincidenciaVacia, consultaManualADto, consultaManualVacia, fechaIso, fuenteManual, restarDias, validarConsultaManual, type BorradorConsultaManual } from './consultaManual';

const HOY = '2026-11-11';

function listo(cambio: Partial<BorradorConsultaManual> = {}): BorradorConsultaManual {
  return { ...consultaManualVacia(HOY), adjunto: adjuntoDePrueba({ proposito: 'ConsultaManual' }), fuente: 'Infolaft web', ...cambio };
}

describe('Consulta de listas hecha por fuera (CU-16)', () => {
  it('sin archivo, fuente o fecha no se puede enviar', () => {
    expect(validarConsultaManual(consultaManualVacia(HOY), HOY)).toEqual(['archivo', 'fuente']);
    expect(validarConsultaManual(listo({ fecha: '' }), HOY)).toEqual(['fecha']);
    expect(validarConsultaManual(listo({ fuente: 'ab' }), HOY)).toEqual(['fuente']);
    expect(validarConsultaManual(listo({ fuente: 'x'.repeat(101) }), HOY)).toEqual(['fuente']);
    expect(validarConsultaManual(listo(), HOY)).toEqual([]);
  });

  it('la fecha es de los últimos 30 días y no futura', () => {
    expect(restarDias(HOY, 30)).toBe('2026-10-12');
    expect(restarDias('2026-03-02', 3)).toBe('2026-02-27');
    expect(validarConsultaManual(listo({ fecha: '2026-10-12' }), HOY)).toEqual([]);
    expect(validarConsultaManual(listo({ fecha: '2026-10-11' }), HOY)).toEqual(['fecha-fuera-de-plazo']);
    expect(validarConsultaManual(listo({ fecha: '2026-11-12' }), HOY)).toEqual(['fecha-fuera-de-plazo']);
    expect(fechaIso(new Date(2026, 0, 5))).toBe('2026-01-05');
  });

  it('la vigencia va de 1 a 60 meses o vacía', () => {
    expect(validarConsultaManual(listo({ vigenciaMeses: '12' }), HOY)).toEqual([]);
    expect(validarConsultaManual(listo({ vigenciaMeses: '0' }), HOY)).toEqual(['vigencia']);
    expect(validarConsultaManual(listo({ vigenciaMeses: '61' }), HOY)).toEqual(['vigencia']);
  });

  it('con coincidencias cada una lleva persona, lista y nombre; sin ellas, las filas no cuentan', () => {
    const fila = coincidenciaVacia();
    expect(validarConsultaManual(listo({ hubo: true, coincidencias: [] }), HOY)).toEqual(['coincidencias']);
    expect(validarConsultaManual(listo({ hubo: true, coincidencias: [fila] }), HOY)).toEqual(['coincidencias']);
    expect(validarConsultaManual(listo({ hubo: true, coincidencias: [{ ...fila, persona: 'RL|1', lista: 'OFAC', nombre: 'ANA' }] }), HOY)).toEqual([]);
    expect(validarConsultaManual(listo({ hubo: false, coincidencias: [fila] }), HOY)).toEqual([]);
  });

  it('el cuerpo va sin coincidencias si no hubo; hoy lleva la hora de ahora y un día anterior, el final de ese día', () => {
    const ahora = new Date('2026-11-11T15:30:00Z');
    expect(consultaManualADto(listo({ hubo: false, coincidencias: [coincidenciaVacia()] }), HOY, ahora)).toEqual({
      adjuntoId: 'adj-1',
      fuente: 'Infolaft web',
      consultadaEn: '2026-11-11T15:30:00.000Z',
      coincidencias: [],
      mesesVigencia: null,
    });
    const dto = consultaManualADto(
      listo({
        fecha: '2026-11-05',
        vigenciaMeses: '6',
        hubo: true,
        coincidencias: [{ ...coincidenciaVacia(), persona: 'RL|1', lista: ' OFAC ', nombre: ' ANA ', detalle: '  ', puntaje: '150' }],
      }),
      HOY,
      ahora,
    );
    expect(dto.consultadaEn).toBe('2026-11-05T23:59:00-05:00');
    expect(dto.mesesVigencia).toBe(6);
    expect(dto.coincidencias).toEqual([{ clavePersona: 'RL|1', lista: 'OFAC', nombreEncontrado: 'ANA', detalle: null, puntaje: 100 }]);
  });

  it('reconoce el proveedor de una consulta manual', () => {
    expect(fuenteManual('Manual · Infolaft web')).toBe('Infolaft web');
    expect(fuenteManual('Infolaft')).toBeNull();
    expect(fuenteManual(null)).toBeNull();
  });
});
