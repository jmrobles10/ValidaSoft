import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState, type ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { aEditables, contactoVacio, desdeEditables, EditorDeContactos, MAXIMO_DE_CONTACTOS, ROLES_DE_CONTACTO, validarContactos, type ContactoEditable } from './EditorDeContactos';
import { createValidasoftTheme } from './tema';

const conTema = (nodo: ReactNode) => render(<ThemeProvider theme={createValidasoftTheme('light')}>{nodo}</ThemeProvider>);

const MENSAJES = { nombre: 'Falta el nombre', correo: 'Correo inválido', repetido: 'Repetido' };
const ETIQUETAS = {
  nombre: 'Nombre',
  cargo: 'Cargo',
  correo: 'Correo',
  telefono: 'Teléfono',
  rol: 'Rol',
  agregar: 'Agregar contacto',
  quitar: (n: number) => `Quitar el contacto ${String(n)}`,
  vacio: 'Sin contactos',
  contacto: (n: number) => `Contacto ${String(n)}`,
};
const ROLES = ROLES_DE_CONTACTO.map((r) => ({ valor: r, etiqueta: r }));

describe('helpers de contactos', () => {
  it('aEditables pasa lo nulo a texto vacío y desdeEditables lo devuelve a nulo, sin espacios y sin filas vacías', () => {
    expect(aEditables([{ nombre: 'Ana', cargo: null, correo: 'a@b.co', telefono: undefined, rol: 'Pagos' }])).toEqual([
      { nombre: 'Ana', cargo: '', correo: 'a@b.co', telefono: '', rol: 'Pagos' },
    ]);
    expect(aEditables(null)).toEqual([]);
    expect(desdeEditables([{ nombre: ' Ana ', cargo: ' ', correo: ' a@b.co ', telefono: '', rol: 'Pagos' }, contactoVacio()])).toEqual([
      { nombre: 'Ana', cargo: null, correo: 'a@b.co', telefono: null, rol: 'Pagos' },
    ]);
  });

  it('valida nombre y correo, y que el mismo correo no se repita con el mismo rol; una fila vacía no cuenta', () => {
    const contactos: ContactoEditable[] = [
      { ...contactoVacio('Pagos'), nombre: 'Ana', correo: 'ana@b.co' },
      { ...contactoVacio('Pagos'), nombre: 'Ana G.', correo: 'ANA@b.co' },
      { ...contactoVacio('Comercial'), nombre: 'Ana', correo: 'ana@b.co' },
      { ...contactoVacio('Notificaciones'), correo: 'sin-arroba' },
      contactoVacio(),
    ];
    expect(validarContactos(contactos, MENSAJES)).toEqual({ 1: { correo: 'Repetido' }, 3: { nombre: 'Falta el nombre', correo: 'Correo inválido' } });
  });
});

function Controlado({ inicial = [], alCambiar }: { inicial?: ContactoEditable[]; alCambiar?: (c: ContactoEditable[]) => void }) {
  const [contactos, setContactos] = useState(inicial);
  return (
    <EditorDeContactos
      contactos={contactos}
      onCambiar={(siguientes) => {
        setContactos(siguientes);
        alCambiar?.(siguientes);
      }}
      roles={ROLES}
      etiquetas={ETIQUETAS}
    />
  );
}

describe('EditorDeContactos', () => {
  it('sin contactos dice que no hay y permite agregar; cada uno lleva nombre, cargo, correo, teléfono y rol', async () => {
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    conTema(<Controlado alCambiar={alCambiar} />);
    expect(screen.getByText('Sin contactos')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Agregar contacto' }));
    const contacto = screen.getByRole('group', { name: 'Contacto 1' });
    expect(screen.queryByText('Sin contactos')).not.toBeInTheDocument();
    for (const campo of ['Nombre', 'Cargo', 'Correo', 'Teléfono']) expect(within(contacto).getByRole('textbox', { name: new RegExp(campo) })).toBeInTheDocument();
    expect(within(contacto).getByRole('combobox', { name: /Rol/ })).toHaveTextContent('Comercial');

    await usuario.click(within(contacto).getByRole('textbox', { name: /Nombre/ }));
    await usuario.paste('Ana Gómez');
    expect(alCambiar).toHaveBeenLastCalledWith([{ nombre: 'Ana Gómez', cargo: '', correo: '', telefono: '', rol: 'Comercial' }]);
  });

  it('se elige el rol de la lista, se quita un contacto y no pasa del máximo', async () => {
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    conTema(<Controlado inicial={[{ ...contactoVacio(), nombre: 'Ana' }, { ...contactoVacio(), nombre: 'Luis' }]} alCambiar={alCambiar} />);

    await usuario.click(within(screen.getByRole('group', { name: 'Contacto 2' })).getByRole('combobox', { name: /Rol/ }));
    await usuario.click(await screen.findByRole('option', { name: 'Pagos' }));
    expect(alCambiar).toHaveBeenLastCalledWith([expect.objectContaining({ nombre: 'Ana', rol: 'Comercial' }), expect.objectContaining({ nombre: 'Luis', rol: 'Pagos' })]);

    await usuario.click(screen.getByRole('button', { name: 'Quitar el contacto 1' }));
    expect(alCambiar).toHaveBeenLastCalledWith([expect.objectContaining({ nombre: 'Luis' })]);
    expect(screen.getAllByRole('group')).toHaveLength(1);
  });

  it('el botón de agregar se apaga al llegar al máximo y los errores salen en su campo', () => {
    const lleno = Array.from({ length: MAXIMO_DE_CONTACTOS }, (_, i) => ({ ...contactoVacio(), nombre: `C${String(i)}`, correo: `c${String(i)}@b.co` }));
    conTema(<EditorDeContactos contactos={lleno} onCambiar={() => undefined} roles={ROLES} etiquetas={ETIQUETAS} errores={{ 0: { correo: 'Correo inválido' } }} />);
    expect(screen.getByRole('button', { name: 'Agregar contacto' })).toBeDisabled();
    expect(within(screen.getByRole('group', { name: 'Contacto 1' })).getByRole('textbox', { name: /Correo/ })).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Correo inválido')).toBeInTheDocument();
  });
});
