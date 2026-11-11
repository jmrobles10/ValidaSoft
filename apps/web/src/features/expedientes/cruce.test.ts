import { describe, expect, it } from 'vitest';
import { conDiscrepanciaCritica, justificacionSuficiente, porcentaje, requiereJustificacion, tieneDiscrepanciaCritica, tieneSinSoporteCritico, tonoDeCampo } from './cruce';
import { expediente, requisito, requisitoSeccion } from './datosPrueba';

const cruce = (discrepanciaCritica: boolean) => ({ estado: 'Cruzado', discrepanciaCritica, cruces: [], sinCruce: [] });
const soporte = (discrepanciaCritica: boolean) => ({ sinAnalisis: false, discrepanciaCritica, secciones: [] });

describe('cruce sección ↔ documento en la revisión (F5)', () => {
  it('cada campo tiene su tono: coincide verde, difiere ámbar (rojo si es crítico), sin verificar gris', () => {
    expect(tonoDeCampo({ resultado: 'Coincide', critico: true })).toBe('exito');
    expect(tonoDeCampo({ resultado: 'Discrepa', critico: false })).toBe('advertencia');
    expect(tonoDeCampo({ resultado: 'Discrepa', critico: true })).toBe('error');
    expect(tonoDeCampo({ resultado: 'NoVerificable', critico: true })).toBe('neutro');
  });

  it('la confianza de la lectura se escribe en porcentaje es-CO', () => {
    expect(porcentaje(0.97).replace(/\s/g, ' ')).toMatch(/^97 ?%$/);
  });

  it('una discrepancia crítica sale del cruce de la sección o del soporte que el documento da a las secciones', () => {
    expect(tieneDiscrepanciaCritica(requisitoSeccion({}, { cruce: cruce(true) }))).toBe(true);
    expect(tieneDiscrepanciaCritica(requisitoSeccion({}, { cruce: cruce(false) }))).toBe(false);
    expect(tieneDiscrepanciaCritica(requisito({ soporteDeSecciones: soporte(true) }))).toBe(true);
    expect(tieneDiscrepanciaCritica(requisito({ soporteDeSecciones: soporte(false) }))).toBe(false);
    expect(tieneDiscrepanciaCritica(requisito())).toBe(false);
  });

  it('de lo que se aprobaría de una vez, solo piden justificación los que tienen una discrepancia crítica', () => {
    const general = requisitoSeccion({}, { cruce: cruce(true) });
    const rut = requisito({ soporteDeSecciones: soporte(false) });
    const datos = { ...expediente('EnRevision'), requisitos: [general, rut] };

    expect(conDiscrepanciaCritica(datos, [{ requisitoId: general.id }, { requisitoId: rut.id }]).map((r) => r.id)).toEqual([general.id]);
    expect(conDiscrepanciaCritica(datos, [{ requisitoId: rut.id }])).toEqual([]);
  });

  it('un cambio crítico sin documento nuevo también pide justificación, pero uno que no es crítico no (FM-15)', () => {
    const cambios = [{ campo: 'razon_social', etiqueta: 'Razón social', anterior: 'A', nuevo: 'B', tipoCambio: 'Modificado' }];
    const critico = requisitoSeccion({}, { sinSoporte: { critico: true, cambios } });
    const comun = requisitoSeccion({ id: 'r-comun' }, { sinSoporte: { critico: false, cambios } });

    expect(tieneSinSoporteCritico(critico)).toBe(true);
    expect(tieneSinSoporteCritico(comun)).toBe(false);
    expect(tieneSinSoporteCritico(requisitoSeccion())).toBe(false);
    expect(requiereJustificacion(critico)).toBe(true);
    expect(requiereJustificacion(comun)).toBe(false);
    expect(requiereJustificacion(requisitoSeccion({}, { cruce: cruce(true) }))).toBe(true);

    const datos = { ...expediente('EnRevision'), requisitos: [critico, comun] };
    expect(conDiscrepanciaCritica(datos, [{ requisitoId: critico.id }, { requisitoId: comun.id }]).map((r) => r.id)).toEqual([critico.id]);
  });

  it('la justificación necesita al menos 10 caracteres de verdad (sin contar espacios a los lados)', () => {
    expect(justificacionSuficiente('corto')).toBe(false);
    expect(justificacionSuficiente('   123456789   ')).toBe(false);
    expect(justificacionSuficiente('La razón social cambió por fusión.')).toBe(true);
  });
});
