import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createValidasoftTheme } from '@validasoft/ui';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import type { DeclaracionPep } from '../tipos';
import type { OrigenDeclaracion } from './declaracion';
import { PreguntaPep } from './PreguntaPep';

vi.mock('./FirmaPad', () => ({
  FirmaPad: ({ onCambiar }: { onCambiar: (png: string | null) => void }) => (
    <button type="button" onClick={() => onCambiar('iVBORw0KGgo=')}>
      Simular firma
    </button>
  ),
}));

const PREGUNTA = '¿Eres una persona expuesta políticamente (PEP)?';
const NATURAL: OrigenDeclaracion = { tipo: 'natural', nombres: 'Ana María', apellidos: 'Gómez', tipoIdentificacion: 'CC', numero: '1020304050', direccion: 'Carrera 7 # 32-16' };

const GUARDADA: DeclaracionPep = {
  nacionalidad: 'CO',
  cargo: 'Alcalde',
  institucion: 'Alcaldía de Soacha',
  pais: 'CO',
  fecha_vinculacion: '2020-01-01',
  vigente: true,
  cuentas_exterior: false,
  vinculo_con_pep: { tiene: false },
  acepta_juramento: true,
  firma_en_calidad: 'NombrePropio',
  firma_png: 'iVBORw0KGgo=',
  fecha_firma: '2026-11-01',
};

interface Estado {
  pep?: boolean | null;
  declaracion_pep?: DeclaracionPep | null;
}

/** La pregunta con su estado, como la usa un formulario; deja a la vista lo que cambió. */
function Contenedor({ inicial, origen = NATURAL }: { inicial: Estado; origen?: OrigenDeclaracion }) {
  const [estado, setEstado] = useState<Estado>(inicial);
  return (
    <>
      <PreguntaPep etiqueta={PREGUNTA} pep={estado.pep} declaracion={estado.declaracion_pep} origen={origen} onCambiar={(c) => setEstado((a) => ({ ...a, ...c }))} mensaje={() => null} />
      <output data-testid="estado">{JSON.stringify({ pep: estado.pep ?? null, cargo: estado.declaracion_pep?.cargo ?? null })}</output>
    </>
  );
}

function montar(inicial: Estado, origen?: OrigenDeclaracion) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <Contenedor inicial={inicial} origen={origen} />
    </ThemeProvider>,
  );
  return userEvent.setup();
}

const respuesta = (valor: 'Sí' | 'No') => within(screen.getByRole('radiogroup', { name: PREGUNTA })).getByRole('radio', { name: valor });
const estado = () => JSON.parse(screen.getByTestId('estado').textContent) as { pep: boolean | null; cargo: string | null };

describe('La pregunta «¿Es PEP?» con su declaración', () => {
  it('responder «Sí» abre la declaración; salir sin declarar devuelve la respuesta a «No»', async () => {
    const usuario = montar({});
    await usuario.click(respuesta('Sí'));
    expect(screen.getByRole('dialog', { name: 'Declaración jurada PEP' })).toBeInTheDocument();
    expect(estado().pep).toBe(true);

    await usuario.click(within(screen.getByRole('dialog', { name: 'Declaración jurada PEP' })).getByRole('button', { name: 'Cerrar' }));
    await usuario.click(within(screen.getByRole('dialog', { name: 'Declaración incompleta' })).getByRole('button', { name: 'Salir sin declarar' }));

    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(estado().pep).toBe(false);
    expect(respuesta('No')).toBeChecked();
    expect(screen.queryByText('Falta diligenciar la declaración PEP.')).not.toBeInTheDocument();
  });

  it('confirmar la declaración deja el «Sí», la guarda en los datos y muestra su estado', async () => {
    const usuario = montar({ pep: false, declaracion_pep: GUARDADA });
    await usuario.click(respuesta('Sí'));
    // «No» la había conservado inactiva: abre precargada.
    expect(screen.getByRole('textbox', { name: 'Cargo desempeñado' })).toHaveValue('Alcalde');
    await usuario.click(screen.getByRole('checkbox', { name: /^Declaro bajo juramento/ }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar declaración' }));

    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(estado()).toEqual({ pep: true, cargo: 'Alcalde' });
    expect(screen.getByText('Declaración lista: se guarda con la sección')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ver o editar declaración' })).toBeInTheDocument();
  });

  it('con una declaración ya guardada, salir sin cambios mantiene el «Sí» y muestra la fecha de la firma', async () => {
    const usuario = montar({ pep: true, declaracion_pep: GUARDADA });
    expect(screen.getByText('Declaración firmada el 01/11/2026')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Ver o editar declaración' }));
    await usuario.click(within(screen.getByRole('dialog', { name: 'Declaración jurada PEP' })).getByRole('button', { name: 'Cerrar' }));
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(estado().pep).toBe(true);
  });

  it('«No» conserva la declaración (inactiva) y esconde su estado', async () => {
    const usuario = montar({ pep: true, declaracion_pep: GUARDADA });
    await usuario.click(respuesta('No'));
    expect(estado()).toEqual({ pep: false, cargo: 'Alcalde' });
    expect(screen.queryByText(/^Declaración firmada/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ver o editar declaración' })).not.toBeInTheDocument();
  });

  it('un «Sí» que ya existía sin declaración avisa que falta; abrirla con el botón y salir mantiene el «Sí»', async () => {
    const usuario = montar({ pep: true });
    expect(screen.getByText('Falta diligenciar la declaración PEP.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Diligenciar declaración' }));
    await usuario.click(within(screen.getByRole('dialog', { name: 'Declaración jurada PEP' })).getByRole('button', { name: 'Cerrar' }));
    await usuario.click(within(screen.getByRole('dialog', { name: 'Declaración incompleta' })).getByRole('button', { name: 'Salir sin declarar' }));
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(estado().pep).toBe(true);
    expect(screen.getByText('Falta diligenciar la declaración PEP.')).toBeInTheDocument();
  });

  it('si faltan los datos básicos avisa qué falta, no abre la declaración y no cambia la respuesta', async () => {
    const usuario = montar({ pep: false }, { ...NATURAL, direccion: '' });
    await usuario.click(respuesta('Sí'));
    expect(screen.getByText('Completa antes la dirección para poder declarar PEP.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(estado().pep).toBe(false);
    expect(respuesta('No')).toBeChecked();
  });

  it('en una persona de la información legal pide su rol, su identificación y su nombre', async () => {
    const usuario = montar({ pep: null }, { tipo: 'persona', persona: { nombres: 'Jorge' } });
    await usuario.click(respuesta('Sí'));
    expect(screen.getByText('Completa antes el rol, el tipo de identificación, el número de identificación para poder declarar PEP.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
