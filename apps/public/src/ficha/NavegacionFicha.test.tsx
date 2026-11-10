import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { estadoFicha, filaEstado, guardada, json, montarFicha } from './pruebas/montar';
import { filaActual } from './secciones';

afterEach(() => reiniciarCliente());

const estado = () =>
  estadoFicha({
    siguienteSeccion: 'FICHA_TRIBUTARIA',
    secciones: [
      filaEstado('FICHA_GENERAL', { version: 2, completa: true }),
      filaEstado('FICHA_TRIBUTARIA', { version: 1, faltantes: [{ campo: 'responsable_ica', regla: null, mensaje: 'Falta «ICA».' }] }),
      filaEstado('FICHA_LEGAL'),
      filaEstado('FICHA_FINANCIERA', { periodo: 2025, version: 1, completa: true }),
      filaEstado('FICHA_SST'),
      filaEstado('FICHA_ESPECIALIDADES'),
    ],
  });

describe('Mi información: panel de secciones', () => {
  it('acompaña la sección con el avance y todas las secciones con su estado, marcando la abierta', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/tributaria', { estado: estado() });

    const panel = await screen.findByRole('navigation', { name: 'Secciones de mi información' });
    expect(within(panel).getByText('2 de 6 secciones completas')).toBeInTheDocument();
    expect(within(panel).getByRole('link', { name: 'Mi información' })).toHaveAttribute('href', '/ficha');
    // En el celular el panel es una barra con el paso actual que se despliega.
    const barra = within(panel).getByRole('button', { name: /Paso 2 de 6/ });
    expect(barra).toHaveAttribute('aria-expanded', 'false');
    await usuario.click(barra);

    const secciones = within(panel).getAllByRole('link').filter((a) => a.getAttribute('href') !== '/ficha');
    expect(secciones.map((a) => a.getAttribute('aria-label'))).toEqual([
      'Información general: Completa',
      'Información tributaria: Falta 1 dato',
      'Información legal: Sin diligenciar',
      'Información financiera · 2025: Completa',
      'Seguridad y salud en el trabajo: Sin diligenciar',
      'Especialidades: Sin diligenciar',
    ]);
    expect(within(panel).getByRole('link', { name: 'Información tributaria: Falta 1 dato' })).toHaveAttribute('aria-current', 'step');
    expect(within(panel).getByRole('link', { name: 'Información financiera · 2025: Completa' })).toHaveAttribute('href', '/ficha/financiera?periodo=2025');

    await usuario.click(within(panel).getByRole('link', { name: 'Información legal: Sin diligenciar' }));
    expect(await screen.findByTestId('ubicacion')).toHaveTextContent('/ficha/legal');
    // Al elegir, la barra del celular se pliega y dice la sección nueva.
    expect(await screen.findByRole('button', { name: /Paso 3 de 6\s*Información legal/ })).toHaveAttribute('aria-expanded', 'false');
  });

  it('con cambios sin guardar pregunta antes de salir; «Guardar y continuar» avanza sin preguntar', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/tributaria', {
      estado: estado(),
      alGuardar: () => json(200, guardada('FICHA_TRIBUTARIA', [], 'FICHA_LEGAL', { responsable_ica: false })),
    });

    const ica = await screen.findByRole('radiogroup', { name: '¿Eres contribuyente del impuesto de industria y comercio (ICA)?' });
    await usuario.click(within(ica).getByRole('radio', { name: 'No' }));
    const panel = screen.getByRole('navigation', { name: 'Secciones de mi información' });
    await usuario.click(within(panel).getByRole('link', { name: 'Mi información' }));

    const dialogo = await screen.findByRole('dialog', { name: '¿Salir sin guardar?' });
    expect(dialogo).toHaveTextContent('Tienes cambios en Información tributaria que no has guardado.');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Seguir editando' }));
    expect(screen.getByTestId('ubicacion')).toHaveTextContent('/ficha/tributaria');

    // Al cerrar, el diálogo devuelve la página (mientras sale, el resto queda oculto a la accesibilidad).
    await usuario.click(await screen.findByRole('button', { name: 'Guardar y continuar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(1));
    await vi.waitFor(() => expect(screen.getByTestId('ubicacion')).toHaveTextContent('/ficha/legal'));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('la financiera abierta en un periodo nuevo marca la fila de la financiera que hay', () => {
    const filas = [filaEstado('FICHA_GENERAL'), filaEstado('FICHA_FINANCIERA', { periodo: 2025 }), filaEstado('FICHA_FINANCIERA', { periodo: 2024 })];
    expect(filaActual(filas, 'FICHA_FINANCIERA', 2024)).toBe(filas[2]);
    expect(filaActual(filas, 'FICHA_FINANCIERA', 2023)).toBe(filas[1]);
    expect(filaActual(filas, 'FICHA_GENERAL', null)).toBe(filas[0]);
  });
});
