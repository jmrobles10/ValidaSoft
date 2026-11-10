import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type MotivoTipificadoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { area, areaDeRequisito, expediente, requisito, requisitoSeccion } from '../datosPrueba';
import { DialogoDecision } from './DialogoDecision';
import { DialogoDecisionArea } from './DialogoDecisionArea';

const MOTIVO: MotivoTipificadoDto = {
  id: 'm1',
  codigo: 'dato-incorrecto',
  nombre: 'Dato incorrecto',
  categoria: 'Documento',
  aplicaA: 'Observacion',
  textoAlTercero: 'Un dato no coincide con el soporte.',
  base: true,
  activo: true,
};

const SECCION = requisitoSeccion();
const DATOS = { ...expediente('EnRevision'), requisitos: [requisito(), SECCION] };

/** Monta el diálogo y devuelve las peticiones POST con su cuerpo. */
function montar(contenido: ReactNode) {
  const enviados: { url: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      if ((init?.method ?? 'GET') === 'POST') enviados.push({ url: entrada instanceof Request ? entrada.url : entrada.toString(), cuerpo: JSON.parse(typeof init?.body === 'string' ? init.body : 'null') });
      return Promise.resolve(new Response(JSON.stringify(DATOS), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>{contenido}</QueryClientProvider>
    </ThemeProvider>,
  );
  return enviados;
}

/** Abre «Campos por corregir» y elige las opciones por su etiqueta legible. */
async function elegirCampos(usuario: ReturnType<typeof userEvent.setup>, contenedor: HTMLElement, etiquetas: string[]) {
  await usuario.click(within(contenedor).getByRole('combobox', { name: 'Campos por corregir (opcional)' }));
  for (const etiqueta of etiquetas) await usuario.click(await screen.findByRole('option', { name: etiqueta }));
  await usuario.keyboard('{Escape}');
}

afterEach(() => {
  reiniciarCliente();
  useAvisos.getState().cerrar();
});

describe('Observar una sección de la información (F2)', () => {
  it('por requisito: además de motivos y observación señala campos, que viajan en `campos`', async () => {
    const usuario = userEvent.setup();
    const enviados = montar(
      <DialogoDecision
        dialogo={{ tipo: 'veredictoRequisito', requisito: SECCION, areaRequisito: areaDeRequisito(), nombreArea: 'Jurídica' }}
        expediente={DATOS}
        empresaId="emp"
        motivos={[MOTIVO]}
        aplica
        onCerrar={() => undefined}
      />,
    );
    const dialogo = screen.getByRole('dialog');
    expect(within(dialogo).queryByRole('combobox', { name: 'Campos por corregir (opcional)' })).not.toBeInTheDocument();

    await usuario.click(within(dialogo).getByRole('radio', { name: /Observar/ }));
    await usuario.click(within(dialogo).getByRole('checkbox', { name: /Dato incorrecto/ }));
    await elegirCampos(usuario, dialogo, ['Razón social', 'Soporte']);
    await usuario.click(within(dialogo).getByRole('textbox', { name: 'Observación para el tercero (opcional)' }));
    await usuario.paste('La razón social no coincide con el RUT.');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Observar y devolver' }));

    await waitFor(() => expect(enviados).toHaveLength(1));
    expect(enviados[0]?.url).toContain('/api/v1/expedientes/e1/requisitos/r-general/areas/a-juridica/veredicto');
    expect(enviados[0]?.cuerpo).toEqual({
      veredicto: 'Observado',
      motivos: ['dato-incorrecto'],
      observacion: 'La razón social no coincide con el RUT.',
      sobreVersion: 3,
      campos: ['razon_social', 'soportes'],
    });
  });

  it('un documento no ofrece campos y no los envía', async () => {
    const usuario = userEvent.setup();
    const enviados = montar(
      <DialogoDecision
        dialogo={{ tipo: 'veredictoRequisito', requisito: requisito(), areaRequisito: areaDeRequisito(), nombreArea: 'Jurídica' }}
        expediente={DATOS}
        empresaId="emp"
        motivos={[MOTIVO]}
        aplica
        onCerrar={() => undefined}
      />,
    );
    await usuario.click(screen.getByRole('radio', { name: /Observar/ }));
    expect(screen.queryByRole('combobox', { name: 'Campos por corregir (opcional)' })).not.toBeInTheDocument();
    await usuario.click(screen.getByRole('checkbox', { name: /Dato incorrecto/ }));
    await usuario.click(screen.getByRole('button', { name: 'Observar y devolver' }));
    await waitFor(() => expect(enviados).toHaveLength(1));
    expect(enviados[0]?.cuerpo).not.toHaveProperty('campos');
  });

  it('«Observar» del área: la sección marcada lleva sus campos en `requisitos[]`', async () => {
    const usuario = userEvent.setup();
    const enviados = montar(<DialogoDecisionArea dialogo={{ tipo: 'observarArea', area: area() }} expediente={DATOS} empresaId="emp" motivos={[MOTIVO]} aplica onCerrar={() => undefined} />);
    const dialogo = screen.getByRole('dialog');

    await usuario.click(within(dialogo).getByRole('checkbox', { name: /Información general/ }));
    await usuario.click(within(dialogo).getByRole('checkbox', { name: /Dato incorrecto/ }));
    await elegirCampos(usuario, dialogo, ['Dirección']);
    await usuario.click(within(dialogo).getByRole('button', { name: 'Observar 1 documento' }));

    await waitFor(() => expect(enviados).toHaveLength(1));
    expect(enviados[0]?.cuerpo).toEqual({
      veredicto: 'Observado',
      requisitos: [{ requisitoId: 'r-general', sobreVersion: 3, motivos: ['dato-incorrecto'], observacion: null, campos: ['direccion'] }],
    });
  });
});
