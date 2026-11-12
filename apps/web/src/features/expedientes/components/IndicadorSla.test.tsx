import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { EstadoDeSla } from '@validasoft/api-client';
import { APARIENCIA_ESTADO, createValidasoftTheme, formatearFechaHora } from '@validasoft/ui';
import { describe, expect, it } from 'vitest';
import { ESTADO_CANONICO_SLA } from '../sla';
import { IndicadorSla } from './IndicadorSla';

const montar = (nodo: React.ReactNode) => render(<ThemeProvider theme={createValidasoftTheme('light')}>{nodo}</ThemeProvider>);

describe('Indicador del plazo de revisión en la bandeja', () => {
  it('a tiempo: chip verde «A tiempo» y cuándo vence', () => {
    montar(<IndicadorSla sla="EnTiempo" vence="2026-11-14T15:00:00Z" />);
    const chip = screen.getByText('A tiempo').closest('[data-estado]');
    expect(chip).toHaveAttribute('data-estado', 'AlDia');
    expect(screen.getByText('vence el 14/11/2026')).toBeInTheDocument();
  });

  it('por vencer: chip de advertencia «Por vencer» y cuándo vence', () => {
    montar(<IndicadorSla sla="PorVencer" vence="2026-11-12T20:00:00Z" />);
    expect(screen.getByText('Por vencer').closest('[data-estado]')).toHaveAttribute('data-estado', 'PorVencer');
    expect(screen.getByText('vence el 12/11/2026')).toBeInTheDocument();
  });

  it('vencido: chip de error «Vencido» y cuándo venció, con la hora completa al pasar el cursor', () => {
    montar(<IndicadorSla sla="Vencido" vence="2026-11-10T20:30:00Z" />);
    expect(screen.getByText('Vencido').closest('[data-estado]')).toHaveAttribute('data-estado', 'Vencido');
    const texto = screen.getByText('venció el 10/11/2026');
    expect(texto).toHaveAttribute('title', formatearFechaHora('2026-11-10T20:30:00Z'));
    expect(texto.getAttribute('title')).toContain('10/11/2026');
  });

  it('sin plazo (o área ya decidida) solo muestra un guion accesible, sin chip', () => {
    const { container } = montar(<IndicadorSla sla={null} vence={null} />);
    expect(screen.getByLabelText('Sin plazo')).toHaveTextContent('—');
    expect(container.querySelector('[data-estado]')).toBeNull();
    // El contrato omite `sla` cuando el área no tiene plazo.
    montar(<IndicadorSla />);
    expect(screen.getAllByLabelText('Sin plazo')).toHaveLength(2);
  });

  it('con el estado y sin fecha muestra solo el chip', () => {
    montar(<IndicadorSla sla="PorVencer" vence={null} />);
    expect(screen.getByText('Por vencer')).toBeInTheDocument();
    expect(screen.queryByText(/vence el/)).not.toBeInTheDocument();
  });

  it('cada estado del plazo usa el estado canónico de su tono: verde, advertencia y error', () => {
    expect(Object.keys(ESTADO_CANONICO_SLA).sort()).toEqual(Object.values(EstadoDeSla).sort());
    expect(APARIENCIA_ESTADO[ESTADO_CANONICO_SLA.EnTiempo].tono).toBe('exito');
    expect(APARIENCIA_ESTADO[ESTADO_CANONICO_SLA.PorVencer].tono).toBe('advertencia');
    expect(APARIENCIA_ESTADO[ESTADO_CANONICO_SLA.Vencido].tono).toBe('error');
  });
});
