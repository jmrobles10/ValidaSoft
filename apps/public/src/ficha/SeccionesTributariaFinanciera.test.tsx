import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cuadre, leerCifra } from './cifras';
import { escribir, estadoFicha, guardada, json, montarFicha } from './pruebas/montar';

afterEach(() => reiniciarCliente());

describe('cifras como se escriben en Colombia', () => {
  it('lee puntos de miles, coma decimal y el signo de pesos; vacío es null e ilegible undefined', () => {
    expect(leerCifra('1.500.000')).toBe(1_500_000);
    expect(leerCifra('$ 1.500.000,50')).toBe(1_500_000.5);
    expect(leerCifra('1500000.5')).toBe(1_500_000.5);
    expect(leerCifra('-250.000')).toBe(-250_000);
    expect(leerCifra('')).toBeNull();
    expect(leerCifra('mil')).toBeUndefined();
  });

  it('el cuadre informa la diferencia con la tolerancia del 0,5 % de los activos', () => {
    expect(cuadre(1000, 400, 600)).toEqual({ suma: 1000, diferencia: 0, cuadra: true });
    expect(cuadre(1000, 400, 597)?.cuadra).toBe(true);
    expect(cuadre(1000, 400, 500)?.cuadra).toBe(false);
    expect(cuadre(1000, null, 600)).toBeNull();
  });
});

describe('Mi información: Información tributaria', () => {
  it('pide la resolución de gran contribuyente solo si elige la responsabilidad 13', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/tributaria', {
      estado: estadoFicha(),
      catalogos: {
        'responsabilidades-dian': [
          { codigo: '13', nombre: 'Gran contribuyente' },
          { codigo: '48', nombre: 'Impuesto sobre las ventas - IVA' },
        ],
      },
      alGuardar: () => json(200, guardada('FICHA_TRIBUTARIA', [], 'FICHA_LEGAL')),
    });

    const responsabilidades = await screen.findByRole('combobox', { name: 'Responsabilidades (casilla 53 del RUT)' });
    expect(screen.queryByText('Resolución de gran contribuyente')).not.toBeInTheDocument();
    await usuario.click(responsabilidades);
    await usuario.click(await screen.findByRole('option', { name: '13 · Gran contribuyente' }));
    await usuario.keyboard('{Escape}');
    expect(screen.getByText('Resolución de gran contribuyente')).toBeInTheDocument();
    await escribir(usuario, screen.getByRole('textbox', { name: 'Número de la resolución' }), '012635');

    const ica = screen.getByRole('radiogroup', { name: '¿Eres contribuyente del impuesto de industria y comercio (ICA)?' });
    await usuario.click(within(ica).getByRole('radio', { name: 'No' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]?.cuerpo).toEqual({
      datos: { responsabilidades: ['13'], resolucion_gran_contribuyente: { numero: '012635' }, responsable_ica: false },
      soportes: [],
    });
  });
});

describe('Mi información: Información financiera', () => {
  it('guarda el periodo elegido y muestra el cuadre en vivo', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/financiera?periodo=2024', { estado: estadoFicha(), alGuardar: () => json(200, guardada('FICHA_FINANCIERA', [], 'FICHA_SST')) });

    expect(await screen.findByRole('combobox', { name: 'Año (periodo)' })).toHaveTextContent('2024');
    const cuadreVivo = screen.getByRole('status', { name: 'Cuadre: activos = pasivos + patrimonio' });
    expect(cuadreVivo).toHaveTextContent('Escribe activos, pasivos y patrimonio para ver el cuadre.');
    await escribir(usuario, screen.getByRole('textbox', { name: 'Activos totales' }), '1.000.000');
    await escribir(usuario, screen.getByRole('textbox', { name: 'Pasivos totales' }), '400.000');
    await escribir(usuario, screen.getByRole('textbox', { name: 'Patrimonio' }), '500.000');
    expect(cuadreVivo).toHaveTextContent('Diferencia de');
    await usuario.clear(screen.getByRole('textbox', { name: 'Patrimonio' }));
    await escribir(usuario, screen.getByRole('textbox', { name: 'Patrimonio' }), '600.000,00');
    expect(cuadreVivo).toHaveTextContent('Cuadra');

    await usuario.click(screen.getByRole('button', { name: 'Guardar y continuar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]?.ruta).toBe('/public/v1/ficha/FICHA_FINANCIERA');
    expect(guardados[0]?.parametros.get('periodo')).toBe('2024');
    expect(guardados[0]?.cuerpo).toEqual({ datos: { activos: 1_000_000, pasivos: 400_000, patrimonio: 600_000 }, soportes: [] });
    await vi.waitFor(() => expect(screen.getByTestId('ubicacion')).toHaveTextContent('/ficha/sst'));
  });
});
