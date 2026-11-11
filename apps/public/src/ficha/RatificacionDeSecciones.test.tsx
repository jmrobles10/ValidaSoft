import { screen, within } from '@testing-library/react';
import { reiniciarCliente, type VinculoPublicoDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { GENERAL_POR_RATIFICAR, renovacionConSecciones } from '../expediente/pruebasSeccion';
import { estadoFicha, filaEstado, montarFicha, seccion } from './pruebas/montar';
import { ratificacionesPendientes, deLaSeccion } from './useRatificacionesPendientes';

afterEach(() => reiniciarCliente());

const VINCULO: VinculoPublicoDto = {
  id: 'v1',
  empresa: { id: 'emp', nombre: 'Constructora Norte', logoUrl: null },
  tipoVinculo: { id: 'tv', codigo: 'CLIENTE', nombre: 'Cliente' },
  estado: 'Vencido',
  invitacionAceptada: true,
  invitacionAceptadaEn: '2026-11-01T15:00:00Z',
  expedienteId: 'e1',
  vigenteHasta: '2026-11-08',
};

const GENERAL = seccion('FICHA_GENERAL', {
  version: 1,
  versiones: 1,
  datos: { razon_social: 'Aceros S.A.S.', tipo_persona: 'Juridica', direccion: 'Calle 45 # 10-20', pais: 'CO' },
  completitud: { completa: true, faltantes: [], reglaVersion: 1 },
});

describe('lo que las empresas piden ratificar (renovación periódica del vínculo)', () => {
  it('lee del expediente de renovación solo las secciones que se pueden ratificar', () => {
    const renovacion = renovacionConSecciones([GENERAL_POR_RATIFICAR]);
    expect(ratificacionesPendientes(renovacion)).toEqual([
      {
        empresa: 'Constructora Norte',
        expedienteId: 'e1',
        requisitoId: 'r-general',
        seccion: 'FICHA_GENERAL',
        periodo: null,
        nombre: 'Información general',
      },
    ]);
    // Un expediente que no es de renovación, o una sección ya ratificada, no pide nada.
    expect(ratificacionesPendientes({ ...renovacion, tipo: 'Actualizacion' })).toEqual([]);
    expect(ratificacionesPendientes(renovacionConSecciones([{ ...GENERAL_POR_RATIFICAR, puedeRatificar: false }]))).toEqual([]);
  });

  it('filtra por sección y por periodo en la financiera', () => {
    const pendientes = [
      { empresa: 'A', expedienteId: 'e1', requisitoId: 'r1', seccion: 'FICHA_GENERAL', periodo: null, nombre: 'General' },
      { empresa: 'A', expedienteId: 'e1', requisitoId: 'r2', seccion: 'FICHA_FINANCIERA', periodo: 2025, nombre: 'Financiera 2025' },
      { empresa: 'A', expedienteId: 'e1', requisitoId: 'r3', seccion: 'FICHA_FINANCIERA', periodo: 2024, nombre: 'Financiera 2024' },
    ];
    expect(deLaSeccion(pendientes, 'FICHA_GENERAL', null).map((p) => p.requisitoId)).toEqual(['r1']);
    expect(deLaSeccion(pendientes, 'FICHA_FINANCIERA', 2025).map((p) => p.requisitoId)).toEqual(['r2']);
    expect(deLaSeccion(pendientes, 'FICHA_FINANCIERA', null).map((p) => p.requisitoId)).toEqual(['r2', 'r3']);
  });
});

describe('Mi información con una renovación pendiente', () => {
  it('avisa qué empresa lo pide y deja ratificar cada sección desde su fila', async () => {
    montarFicha('/ficha', {
      estado: estadoFicha({
        siguienteSeccion: null,
        secciones: [
          filaEstado('FICHA_GENERAL', { version: 1, completa: true }),
          filaEstado('FICHA_TRIBUTARIA', { version: 1, completa: true }),
          filaEstado('FICHA_LEGAL'),
          filaEstado('FICHA_FINANCIERA'),
          filaEstado('FICHA_SST'),
          filaEstado('FICHA_ESPECIALIDADES'),
        ],
      }),
      vinculos: [VINCULO],
      expedientes: { e1: renovacionConSecciones([GENERAL_POR_RATIFICAR]) },
    });

    expect(await screen.findByText('1 empresa te pide ratificar tu información', undefined, { timeout: 15_000 })).toBeInTheDocument();
    expect(screen.getByText(/Confirma que sigue igual \(sin cambiar nada\) o actualízala: Constructora Norte\./)).toBeInTheDocument();
    const lista = screen.getByRole('list', { name: 'Mi información' });
    const general = within(within(lista).getByRole('listitem', { name: 'Información general' }));
    expect(general.getByRole('button', { name: 'Ratificar para Constructora Norte' })).toBeInTheDocument();
    // Solo la sección que la empresa pidió.
    expect(within(lista).getByRole('listitem', { name: 'Información tributaria' })).not.toHaveTextContent('Ratificar');
  });

  it('el wizard de la sección muestra el pedido de la empresa y deja ratificar sin editar', async () => {
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      vinculos: [VINCULO],
      expedientes: { e1: renovacionConSecciones([GENERAL_POR_RATIFICAR]) },
    });

    const aviso = await screen.findByRole('alert', { name: 'Constructora Norte te pide ratificar esta información' }, { timeout: 15_000 });
    expect(aviso).toHaveTextContent('Si no cambió, confírmalo. Si cambió, actualiza el formulario y guarda');
    expect(within(aviso).getByRole('button', { name: 'Ratificar' })).toBeInTheDocument();
  });
});
