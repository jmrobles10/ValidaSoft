import { screen, within } from '@testing-library/react';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { estadoFicha, filaEstado, montarFicha, ORDEN_NATURAL } from './pruebas/montar';

afterEach(() => reiniciarCliente());

describe('Mi información: resumen', () => {
  it('muestra cada sección con el estado del servidor y «Continuar» lleva a la siguiente incompleta', async () => {
    montarFicha('/ficha', {
      estado: estadoFicha({
        siguienteSeccion: 'FICHA_TRIBUTARIA',
        secciones: [
          filaEstado('FICHA_GENERAL', { version: 2, completa: true, actualizada: '2026-11-01T15:00:00Z' }),
          filaEstado('FICHA_TRIBUTARIA', {
            version: 1,
            faltantes: [
              { campo: 'responsable_ica', regla: null, mensaje: 'Falta «ICA».' },
              { campo: null, regla: 'soporte-del-cambio', mensaje: 'Falta el soporte del cambio' },
            ],
          }),
          filaEstado('FICHA_LEGAL'),
          filaEstado('FICHA_FINANCIERA', { periodo: 2025, version: 1, completa: true }),
          filaEstado('FICHA_SST'),
          filaEstado('FICHA_ESPECIALIDADES'),
        ],
      }),
    });

    const lista = await screen.findByRole('list', { name: 'Mi información' });
    expect(within(lista).getAllByRole('listitem')).toHaveLength(6);
    expect(within(lista).getByRole('listitem', { name: 'Información general' })).toHaveTextContent('Completa');
    expect(within(lista).getByRole('listitem', { name: 'Información general' })).toHaveTextContent('Actualizada el 01/11/2026');
    expect(within(lista).getByRole('listitem', { name: 'Información tributaria' })).toHaveTextContent('Faltan 2 datos');
    expect(within(lista).getByRole('listitem', { name: 'Información legal' })).toHaveTextContent('Sin diligenciar');
    expect(within(lista).getByRole('listitem', { name: 'Información financiera · 2025' })).toHaveTextContent('Completa');
    expect(screen.getByText('2 de 6 secciones completas')).toBeInTheDocument();

    expect(screen.getByRole('link', { name: 'Continuar' })).toHaveAttribute('href', '/ficha/tributaria');
    expect(screen.getByRole('link', { name: 'Abrir Información financiera · 2025' })).toHaveAttribute('href', '/ficha/financiera?periodo=2025');
    expect(screen.getByRole('link', { name: 'Abrir Información legal' })).toHaveTextContent('Diligenciar');
  });

  it('con la ficha completa no ofrece «Continuar» y lo dice', async () => {
    montarFicha('/ficha', {
      estado: estadoFicha({ orden: ORDEN_NATURAL, siguienteSeccion: null, completa: true, secciones: ORDEN_NATURAL.map((s) => filaEstado(s, { version: 1, completa: true })) }),
    });
    expect(await screen.findByText('Tu información está completa')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Continuar' })).not.toBeInTheDocument();
  });

  it('a la persona natural no le muestra Legal ni Financiera, ni la deja abrirlas', async () => {
    montarFicha('/ficha', {
      // La API trae todas las filas, pero el orden de la persona natural no incluye Legal ni Financiera.
      estado: estadoFicha({ tipoPersona: 'Natural', orden: ORDEN_NATURAL, secciones: ['FICHA_GENERAL', 'FICHA_TRIBUTARIA', 'FICHA_LEGAL', 'FICHA_FINANCIERA', 'FICHA_SST', 'FICHA_ESPECIALIDADES'].map((s) => filaEstado(s)) }),
    });
    const lista = await screen.findByRole('list', { name: 'Mi información' });
    expect(within(lista).getAllByRole('listitem')).toHaveLength(4);
    expect(lista).not.toHaveTextContent('Información legal');
    expect(lista).not.toHaveTextContent('Información financiera');
  });

  it('una sección que no aplica al tipo de persona no se abre', async () => {
    montarFicha('/ficha/legal', { estado: estadoFicha({ tipoPersona: 'Natural', orden: ORDEN_NATURAL }) });
    expect(await screen.findByText('Esta sección no aplica para ti')).toBeInTheDocument();
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
  });
});
