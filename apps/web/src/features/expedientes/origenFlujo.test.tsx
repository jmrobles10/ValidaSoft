import { screen } from '@testing-library/react';
import { reiniciarCliente, SCOPES, type CondicionDto, type FlujoDeExpedienteDto, type VinculoDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../i18n';
import { montarPantalla } from '../../pruebas/montar';
import { simularApi } from '../../pruebas/simularApi';
import { DatosExpediente } from './components/PanelesExpediente';
import { TarjetaExpedienteVinculo } from './components/TarjetaExpedienteVinculo';
import { expediente } from './datosPrueba';
import { explicarOrigenDelFlujo } from './origenFlujo';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const t = i18n.t.bind(i18n);

const riesgoAlto: CondicionDto = { tipo: 'Predicado', nodos: null, atributo: 'vinculo.atributos.nivelRiesgo', operador: 'En', valores: ['Alto'] };

const flujo = (cambios: Partial<FlujoDeExpedienteDto> = {}): FlujoDeExpedienteDto => ({
  id: 'f1',
  versionId: 'fv1',
  codigo: 'CRITICO',
  nombre: 'Proveedor crítico',
  version: 3,
  origen: 'Regla',
  regla: { prioridad: 2, condicion: riesgoAlto, reglasEvaluadas: 2 },
  ...cambios,
});

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('Por qué el expediente tiene su flujo (FR-1)', () => {
  it('por regla dice qué condición se cumplió, su prioridad y en qué posición se evaluó', () => {
    expect(explicarOrigenDelFlujo(flujo(), t)).toBe(
      'Se escogió por regla: nivelRiesgo es uno de Alto (prioridad 2). Gana la primera que se cumple; esta fue la 2.ª que se evaluó.',
    );
  });

  it('describe una condición compuesta en palabras', () => {
    const compuesta: CondicionDto = {
      tipo: 'Y',
      nodos: [{ tipo: 'Predicado', nodos: null, atributo: 'tercero.tipoPersona', operador: 'Igual', valores: ['Juridica'] }, riesgoAlto],
    };
    const texto = explicarOrigenDelFlujo(flujo({ regla: { prioridad: 1, condicion: compuesta, reglasEvaluadas: 1 } }), t);
    expect(texto).toContain('tipo de persona es Jurídica y nivelRiesgo es uno de Alto');
    expect(texto).toContain('prioridad 1');
  });

  it('un expediente anterior a las reglas, o sin el detalle, se explica sin condición', () => {
    expect(explicarOrigenDelFlujo(flujo({ regla: null }), t)).toBe('Se escogió por una regla de selección.');
    expect(explicarOrigenDelFlujo(flujo({ regla: undefined }), t)).toBe('Se escogió por una regla de selección.');
  });

  it('explícito y por defecto', () => {
    expect(explicarOrigenDelFlujo(flujo({ origen: 'Explicito', regla: null }), t)).toBe('Se eligió el flujo al abrir el expediente o al crear el vínculo.');
    expect(explicarOrigenDelFlujo(flujo({ origen: 'PorDefecto', regla: null }), t)).toBe(
      'Ninguna regla de selección se cumplió, así que se usó el flujo por defecto del tipo de vínculo.',
    );
  });

  it('el detalle del expediente muestra el flujo, su origen y la explicación', () => {
    montarPantalla(<DatosExpediente expediente={{ ...expediente('Abierto'), flujo: flujo() }} />);

    expect(screen.getByText('Proveedor crítico · v3')).toBeInTheDocument();
    expect(screen.getByText('(por regla)')).toBeInTheDocument();
    expect(screen.getByText(/Se escogió por regla: nivelRiesgo es uno de Alto \(prioridad 2\)/)).toBeInTheDocument();
  });

  it('el detalle del expediente por defecto lo explica y uno sin flujo no muestra explicación', () => {
    const { unmount } = montarPantalla(<DatosExpediente expediente={{ ...expediente('Abierto'), flujo: flujo({ origen: 'PorDefecto', regla: null }) }} />);
    expect(screen.getByText('(por defecto)')).toBeInTheDocument();
    expect(screen.getByText(/Ninguna regla de selección se cumplió/)).toBeInTheDocument();
    unmount();

    montarPantalla(<DatosExpediente expediente={expediente('Abierto')} />);
    expect(screen.queryByText(/regla/i)).not.toBeInTheDocument();
  });

  it('el detalle del vínculo muestra el flujo del expediente en curso y por qué se escogió', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    simularApi({
      'GET /api/v1/terceros/t': { id: 't', nombre: 'Aceros del Norte S.A.S.' },
      'GET /api/v1/expedientes/e1': { ...expediente('EnRevision'), flujo: flujo() },
    });
    const vinculo = { id: 'vin', empresaId: 'emp', terceroId: 't', expedienteAbiertoId: 'e1', estado: 'EnValidacion' } as VinculoDto;

    montarPantalla(<TarjetaExpedienteVinculo vinculo={vinculo} />);

    expect(await screen.findByText('Flujo: Proveedor crítico · v3')).toBeInTheDocument();
    expect(screen.getByText(/Se escogió por regla: nivelRiesgo es uno de Alto \(prioridad 2\)/)).toBeInTheDocument();
  });
});
