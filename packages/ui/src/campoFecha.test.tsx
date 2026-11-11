import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { CampoFecha, fechaDeIso, isoDeFecha, type CampoFechaProps } from './campoFecha';
import { createValidasoftTheme } from './tema';

/** Un campo con estado (como lo usa un formulario) que anota cada valor que entrega. */
function Campo({ inicial = '', alCambiar, ...resto }: { inicial?: string; alCambiar?: (iso: string) => void } & Partial<CampoFechaProps>) {
  const [valor, setValor] = useState(inicial);
  return (
    <>
      <CampoFecha
        etiqueta="Fecha de corte"
        valor={valor}
        onCambiar={(iso) => {
          setValor(iso);
          alCambiar?.(iso);
        }}
        {...resto}
      />
      <output data-testid="valor">{valor}</output>
    </>
  );
}

const montar = (nodo: React.ReactNode, app: 'web' | 'public' = 'web') => render(<ThemeProvider theme={createValidasoftTheme('light', { app })}>{nodo}</ThemeProvider>);
const seccion = (nombre: string) => screen.getByRole('spinbutton', { name: nombre });
const escribir = async (usuario: ReturnType<typeof userEvent.setup>, digitos: string) => {
  await usuario.click(seccion('Día'));
  await usuario.keyboard(digitos);
};

describe('conversión ISO ⇄ fecha', () => {
  it('lee aaaa-mm-dd como fecha local y rechaza lo que no es una fecha real', () => {
    expect(fechaDeIso('2026-03-15')?.format('DD/MM/YYYY')).toBe('15/03/2026');
    expect(fechaDeIso('2026-03-15T10:00:00Z')?.format('DD/MM/YYYY')).toBe('15/03/2026');
    expect(fechaDeIso('2026-02-31')).toBeNull();
    expect(fechaDeIso('15/03/2026')).toBeNull();
    expect(fechaDeIso('')).toBeNull();
    expect(fechaDeIso(null)).toBeNull();
    expect(fechaDeIso(undefined)).toBeNull();
  });

  it('entrega aaaa-mm-dd, y vacío cuando no hay fecha', () => {
    expect(isoDeFecha(fechaDeIso('2026-03-05'))).toBe('2026-03-05');
    expect(isoDeFecha(null)).toBe('');
  });
});

describe('CampoFecha', () => {
  it('es un grupo con nombre, con las secciones dd/mm/aaaa en español, y no es un input de fecha del navegador', () => {
    const { container } = montar(<Campo />);
    expect(screen.getByRole('group', { name: 'Fecha de corte' })).toBeInTheDocument();
    expect(seccion('Día')).toHaveTextContent('dd');
    expect(seccion('Mes')).toHaveTextContent('mm');
    expect(seccion('Año')).toHaveTextContent('aaaa');
    expect(container.querySelector('input[type="date"]')).toBeNull();
    expect(screen.getByRole('button', { name: 'Elegir fecha' })).toBeInTheDocument();
  });

  it('muestra el valor ISO como dd/mm/aaaa', () => {
    montar(<Campo inicial="2025-12-31" />);
    expect(seccion('Día')).toHaveTextContent('31');
    expect(seccion('Mes')).toHaveTextContent('12');
    expect(seccion('Año')).toHaveTextContent('2025');
    expect(screen.getByRole('button', { name: /^Elegir fecha, la fecha elegida es 31 de dic/ })).toBeInTheDocument();
  });

  it('al escribir con el teclado entrega la fecha en ISO (aaaa-mm-dd)', async () => {
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    montar(<Campo alCambiar={alCambiar} />);
    await escribir(usuario, '15032026');
    expect(alCambiar).toHaveBeenLastCalledWith('2026-03-15');
    expect(screen.getByTestId('valor')).toHaveTextContent('2026-03-15');
    expect(seccion('Día')).toHaveTextContent('15');
    expect(seccion('Mes')).toHaveTextContent('03');
    expect(seccion('Año')).toHaveTextContent('2026');
  });

  it('mientras la fecha está incompleta entrega vacío y conserva lo escrito', async () => {
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    montar(<Campo alCambiar={alCambiar} />);
    await escribir(usuario, '1503');
    expect(alCambiar).not.toHaveBeenCalledWith(expect.stringMatching(/^\d{4}-/));
    expect(screen.getByTestId('valor')).toBeEmptyDOMElement();
    expect(seccion('Día')).toHaveTextContent('15');
    expect(seccion('Mes')).toHaveTextContent('03');
    expect(seccion('Año')).toHaveTextContent('aaaa');
  });

  it('una fecha que no existe (31/02) entrega vacío y, al salir del campo, avisa que no es válida', async () => {
    const usuario = userEvent.setup();
    montar(<Campo />);
    await escribir(usuario, '31022026');
    expect(screen.getByTestId('valor')).toBeEmptyDOMElement();
    await usuario.tab();
    await usuario.tab();
    await usuario.click(document.body);
    expect(await screen.findByText('Escribe una fecha válida (dd/mm/aaaa).')).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Fecha de corte' })).toHaveAccessibleDescription('Escribe una fecha válida (dd/mm/aaaa).');
  });

  it('el botón del calendario y su ícono no se vuelven a montar al escribir ni al salir del campo (así el primer clic abre el calendario)', async () => {
    const usuario = userEvent.setup();
    montar(<Campo />);
    const boton = screen.getByRole('button', { name: /^Elegir fecha/ });
    const icono = boton.querySelector('svg');
    await escribir(usuario, '15032026');
    await usuario.tab();
    await usuario.tab();
    const despues = screen.getByRole('button', { name: /^Elegir fecha/ });
    expect(despues).toBe(boton);
    expect(despues.querySelector('svg')).toBe(icono);
    await usuario.click(despues);
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
  });

  it('al elegir un día en el calendario entrega esa fecha en ISO', async () => {
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    montar(<Campo inicial="2026-03-05" alCambiar={alCambiar} />);
    await usuario.click(screen.getByRole('button', { name: /^Elegir fecha/ }));
    const calendario = await screen.findByRole('dialog');
    await usuario.click(within(calendario).getByRole('gridcell', { name: '18' }));
    expect(alCambiar).toHaveBeenLastCalledWith('2026-03-18');
    expect(screen.getByTestId('valor')).toHaveTextContent('2026-03-18');
    expect(seccion('Día')).toHaveTextContent('18');
  });

  it('el calendario está en español: mes, días de la semana (lunes primero) y botones', async () => {
    const usuario = userEvent.setup();
    montar(<Campo inicial="2025-12-31" />);
    await usuario.click(screen.getByRole('button', { name: /^Elegir fecha/ }));
    const calendario = await screen.findByRole('dialog');
    expect(within(calendario).getByText(/diciembre 2025/i)).toBeInTheDocument();
    expect(within(calendario).getByRole('button', { name: 'Mes anterior' })).toBeInTheDocument();
    expect(within(calendario).getByRole('button', { name: 'Mes siguiente' })).toBeInTheDocument();
    expect(within(calendario).getAllByRole('columnheader').map((c) => c.textContent)).toEqual(['L', 'M', 'M', 'J', 'V', 'S', 'D']);
    expect(within(calendario).getByRole('button', { name: 'Hoy' })).toBeInTheDocument();
    expect(within(calendario).getByRole('button', { name: 'Limpiar' })).toBeInTheDocument();
  });

  it('«Limpiar» del calendario deja el campo vacío', async () => {
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    montar(<Campo inicial="2026-03-05" alCambiar={alCambiar} />);
    await usuario.click(screen.getByRole('button', { name: /^Elegir fecha/ }));
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Limpiar' }));
    expect(alCambiar).toHaveBeenLastCalledWith('');
    expect(screen.getByTestId('valor')).toBeEmptyDOMElement();
    expect(seccion('Día')).toHaveTextContent('dd');
  });

  it('siendo requerido no ofrece «Limpiar» y marca el asterisco', async () => {
    const usuario = userEvent.setup();
    montar(<Campo requerido />);
    expect(screen.getByRole('group', { name: /Fecha de corte/ })).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: /^Elegir fecha/ }));
    const calendario = await screen.findByRole('dialog');
    expect(within(calendario).queryByRole('button', { name: 'Limpiar' })).not.toBeInTheDocument();
    expect(within(calendario).getByRole('button', { name: 'Hoy' })).toBeInTheDocument();
  });

  it('cuando el formulario cambia el valor por su cuenta (limpiar filtros), el campo lo refleja', () => {
    const { rerender } = montar(<CampoFecha etiqueta="Desde" valor="2026-03-05" onCambiar={() => undefined} />);
    expect(seccion('Día')).toHaveTextContent('05');
    const tema = createValidasoftTheme('light', { app: 'web' });
    rerender(
      <ThemeProvider theme={tema}>
        <CampoFecha etiqueta="Desde" valor="2026-07-20" onCambiar={() => undefined} />
      </ThemeProvider>,
    );
    expect(seccion('Día')).toHaveTextContent('20');
    expect(seccion('Mes')).toHaveTextContent('07');
    rerender(
      <ThemeProvider theme={tema}>
        <CampoFecha etiqueta="Desde" valor="" onCambiar={() => undefined} />
      </ThemeProvider>,
    );
    expect(seccion('Día')).toHaveTextContent('dd');
    expect(seccion('Año')).toHaveTextContent('aaaa');
  });

  it('con error del formulario muestra la ayuda y marca el campo; deshabilitado no se edita', () => {
    montar(<CampoFecha etiqueta="Vence" valor="" onCambiar={() => undefined} error ayuda="Es obligatoria" />);
    expect(screen.getByText('Es obligatoria')).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Vence' })).toHaveAccessibleDescription('Es obligatoria');
  });

  it('un campo deshabilitado no abre el calendario', async () => {
    const usuario = userEvent.setup({ pointerEventsCheck: 0 });
    montar(<CampoFecha etiqueta="Vence" valor="2026-03-05" onCambiar={() => undefined} deshabilitado />);
    expect(screen.getByRole('button', { name: /^Elegir fecha/ })).toBeDisabled();
    await usuario.click(screen.getByRole('button', { name: /^Elegir fecha/ }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('respeta el mínimo y el máximo: los días fuera del rango no se pueden elegir', async () => {
    const usuario = userEvent.setup();
    montar(<Campo inicial="2026-03-15" minimo="2026-03-10" maximo="2026-03-20" />);
    await usuario.click(screen.getByRole('button', { name: /^Elegir fecha/ }));
    const calendario = await screen.findByRole('dialog');
    expect(within(calendario).getByRole('gridcell', { name: '9' })).toBeDisabled();
    expect(within(calendario).getByRole('gridcell', { name: '10' })).toBeEnabled();
    expect(within(calendario).getByRole('gridcell', { name: '21' })).toBeDisabled();
  });

  it('en la app del tercero también se pinta (campo medium) y tiene el mismo nombre', () => {
    montar(<CampoFecha etiqueta="Fecha de nacimiento" valor="" onCambiar={() => undefined} fullWidth />, 'public');
    expect(screen.getByRole('group', { name: 'Fecha de nacimiento' })).toBeInTheDocument();
  });
});
