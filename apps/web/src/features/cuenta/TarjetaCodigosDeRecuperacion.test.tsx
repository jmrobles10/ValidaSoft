import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { montarPantalla } from '../../pruebas/montar';
import { ProblemaSimulado, simularApi } from '../../pruebas/simularApi';
import { documentoParaImprimir, textoDeCodigos } from './codigosDeRecuperacion';
import { TarjetaCodigosDeRecuperacion } from './TarjetaCodigosDeRecuperacion';

afterEach(() => reiniciarCliente());

const CODIGOS = ['AB3CD-EFGH2', 'JK4MN-PQRS5', 'TU6VW-XYZ72', 'AB3CE-EFGH3', 'JK4MP-PQRS6', 'TU6VX-XYZ73', 'AB3CF-EFGH4', 'JK4MQ-PQRS7', 'TU6VY-XYZ74', 'AB3CG-EFGH5'];

function montar(estado: { disponibles: number; total: number; generadosEn: string | null }, rutas: Parameters<typeof simularApi>[0] = {}) {
  const peticiones = simularApi({ 'GET /api/v1/mi-cuenta/codigos-de-recuperacion': () => estado, ...rutas });
  montarPantalla(<TarjetaCodigosDeRecuperacion />);
  return { peticiones, usuario: userEvent.setup() };
}

describe('códigos de recuperación en Mi cuenta (PI-18)', () => {
  it('sin códigos invita a generarlos', async () => {
    montar({ disponibles: 0, total: 0, generadosEn: null });
    expect(await screen.findByText('Aún no tienes códigos de recuperación.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Generar códigos' })).toBeInTheDocument();
  });

  it('dice cuántos quedan sin usar y cuándo se generaron', async () => {
    montar({ disponibles: 7, total: 10, generadosEn: '2026-11-05T15:00:00Z' });
    expect(await screen.findByText(/Te quedan 7 de 10 códigos sin usar\./)).toBeInTheDocument();
    expect(screen.getByText(/Los generaste el/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Generar códigos nuevos' })).toBeInTheDocument();
    expect(screen.queryByText(/Genera otros antes de quedarte sin ninguno/)).not.toBeInTheDocument();
  });

  it('con dos o menos avisa que genere otros antes de quedarse sin ninguno', async () => {
    montar({ disponibles: 1, total: 10, generadosEn: '2026-11-05T15:00:00Z' });
    expect(await screen.findByText(/Te queda 1 de 10 códigos sin usar\./)).toBeInTheDocument();
    expect(screen.getByText(/Genera otros antes de quedarte sin ninguno/)).toBeInTheDocument();
  });

  it('generar pide confirmar, muestra los diez códigos una sola vez y no deja cerrar hasta que se guarden', async () => {
    const { peticiones, usuario } = montar(
      { disponibles: 0, total: 0, generadosEn: null },
      { 'POST /api/v1/mi-cuenta/codigos-de-recuperacion': { codigos: CODIGOS, generadosEn: '2026-11-11T15:00:00Z' } },
    );
    await usuario.click(await screen.findByRole('button', { name: 'Generar códigos' }));
    const confirmar = await screen.findByRole('dialog', { name: 'Generar códigos de recuperación' });
    await usuario.click(within(confirmar).getByRole('button', { name: 'Generar códigos' }));

    const codigos = await screen.findByRole('dialog', { name: 'Tus códigos de recuperación' });
    expect(within(codigos).getByText(/no volverán a mostrarse/)).toBeInTheDocument();
    expect(within(codigos).getAllByRole('listitem')).toHaveLength(10);
    expect(within(codigos).getByText('AB3CD-EFGH2')).toBeInTheDocument();
    const listo = within(codigos).getByRole('button', { name: 'Listo' });
    expect(listo).toBeDisabled();

    await usuario.click(within(codigos).getByRole('checkbox', { name: 'Ya los guardé en un lugar seguro' }));
    expect(listo).toBeEnabled();
    await usuario.click(listo);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByText('AB3CD-EFGH2')).not.toBeInTheDocument();
    expect(peticiones.filter((p) => p.metodo === 'POST')).toHaveLength(1);
  });

  it('al regenerar avisa que los anteriores dejan de servir', async () => {
    const { usuario } = montar({ disponibles: 4, total: 10, generadosEn: '2026-11-05T15:00:00Z' });
    await usuario.click(await screen.findByRole('button', { name: 'Generar códigos nuevos' }));
    expect(await screen.findByText(/Los códigos que tienes dejarán de servir/)).toBeInTheDocument();
  });

  it('un error de la API al generar se avisa y el diálogo de confirmación sigue abierto para reintentar', async () => {
    const { usuario } = montar(
      { disponibles: 0, total: 0, generadosEn: null },
      { 'POST /api/v1/mi-cuenta/codigos-de-recuperacion': new ProblemaSimulado(409, 'sin-passkey', 'Los códigos de recuperación respaldan tu passkey: registra una primero.') },
    );
    await usuario.click(await screen.findByRole('button', { name: 'Generar códigos' }));
    const confirmar = await screen.findByRole('dialog', { name: 'Generar códigos de recuperación' });

    await usuario.click(within(confirmar).getByRole('button', { name: 'Generar códigos' }));

    expect(await screen.findByText('Registra una passkey en Mi cuenta para hacer esto.')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Generar códigos de recuperación' })).toBeInTheDocument();
  });

  it('si no se pueden cargar ofrece reintentar', async () => {
    simularApi({ 'GET /api/v1/mi-cuenta/codigos-de-recuperacion': new ProblemaSimulado(500, 'error-interno', 'falló') });
    montarPantalla(<TarjetaCodigosDeRecuperacion />);
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});

describe('copiar e imprimir los códigos', () => {
  it('copia uno por línea', () => {
    expect(textoDeCodigos(['AB3CD-EFGH2', 'JK4MN-PQRS5'])).toBe('AB3CD-EFGH2\nJK4MN-PQRS5');
  });

  it('la hoja impresa lleva los códigos y escapa lo que no debe interpretarse como HTML', () => {
    const html = documentoParaImprimir({ codigos: ['AB3CD-EFGH2'], generadosEn: '2026-11-11T15:00:00Z' }, 'Códigos <b>de</b> Validasoft', 'Guarda "esta" hoja & listo');
    expect(html).toContain('<li>AB3CD-EFGH2</li>');
    expect(html).toContain('Códigos &lt;b&gt;de&lt;/b&gt; Validasoft');
    expect(html).toContain('Guarda &quot;esta&quot; hoja &amp; listo');
    expect(html).not.toContain('<b>de</b>');
    expect(html).not.toContain('<script');
  });
});
