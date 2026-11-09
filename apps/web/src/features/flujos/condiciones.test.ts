import type { CondicionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import { atajoDe, condicionDe, describirCondicion } from './condiciones';
import { borradorDesdeVersion, requisitoNuevo, requisitosADto, validarRequisitos } from './editor';

const predicado = (atributo: string, operador: string, ...valores: string[]): CondicionDto => ({ tipo: 'Predicado', nodos: null, atributo, operador, valores });

const t = i18n.t.bind(i18n);

describe('«Se pide a» (RQ-12)', () => {
  it('reconoce los atajos sin distinguir mayúsculas y lo demás es personalizado', () => {
    expect(atajoDe(null)).toBe('siempre');
    expect(atajoDe({ tipo: 'Y', nodos: [] })).toBe('siempre');
    expect(atajoDe(predicado('tercero.tipoPersona', 'Igual', 'Juridica'))).toBe('juridica');
    expect(atajoDe(predicado('tercero.tipoPersona', 'Igual', 'natural'))).toBe('natural');
    expect(atajoDe(predicado('tercero.tipoPersona', 'En', 'Juridica', 'Consorcio'))).toBe('personalizada');
    expect(atajoDe(predicado('tercero.pais', 'Igual', 'CO'))).toBe('personalizada');
  });

  it('un atajo arma su condición y «personalizada» conserva la que había', () => {
    const pais = predicado('tercero.pais', 'Igual', 'CO');
    expect(condicionDe('siempre', pais)).toBeNull();
    expect(condicionDe('juridica', null)).toEqual(predicado('tercero.tipoPersona', 'Igual', 'Juridica'));
    expect(condicionDe('personalizada', pais)).toBe(pais);
  });

  it('describe una condición compuesta en palabras', () => {
    const condicion: CondicionDto = {
      tipo: 'Y',
      nodos: [
        predicado('tercero.tipoPersona', 'Igual', 'Juridica'),
        { tipo: 'O', nodos: [predicado('vinculo.atributos.montoAnual', 'Mayor', '500000000'), predicado('tercero.pais', 'En', 'PE', 'EC')] },
      ],
    };
    expect(describirCondicion(condicion, t)).toBe('tipo de persona es Jurídica y (montoAnual mayor que 500000000 o país es uno de PE, EC)');
  });

  it('la condición sobrevive a la ida y vuelta del editor y al menos un documento se pide siempre', () => {
    const pais = predicado('tercero.pais', 'Igual', 'CO');
    const borrador = borradorDesdeVersion({ codigo: 'P', nombre: 'P', tipoVinculo: 'PROVEEDOR' }, {
      id: 'v', version: 1, estado: 'Borrador', etapas: [], politicaDevolucion: 'Inmediata', separacionDeFunciones: false, creadaEn: '2026-11-09T00:00:00Z', publicadaEn: null, retiradaEn: null,
      requisitos: [{ id: 'r1', tipoDocumentoTenantId: 'rut', obligatorio: true, areasQueRevisan: [], condicion: pais }],
    });
    expect(requisitosADto(borrador.requisitos)[0]?.condicion).toEqual(pais);

    const errores = validarRequisitos({ etapas: [], requisitos: borrador.requisitos }, []);
    expect(errores).toContainEqual({ codigo: 'requisito-siempre-requerido', campo: 'requisito.0' });
    expect(validarRequisitos({ etapas: [], requisitos: [...borrador.requisitos, requisitoNuevo('camara')] }, []).map((e) => e.codigo))
      .not.toContain('requisito-siempre-requerido');
  });
});
