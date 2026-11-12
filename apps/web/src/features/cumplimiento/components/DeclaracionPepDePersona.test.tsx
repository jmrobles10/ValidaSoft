import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PersonaInformeDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderizar } from '../pruebasApi';
import { DeclaracionPepDePersona } from './DeclaracionPepDePersona';

const descargarArchivo = vi.hoisted(() => vi.fn());
const guardarArchivo = vi.hoisted(() => vi.fn());
vi.mock('@validasoft/api-client', async (importar) => ({ ...(await importar<typeof import('@validasoft/api-client')>()), descargarArchivo }));
vi.mock('../../vencimientos/api', async (importar) => ({ ...(await importar<typeof import('../../vencimientos/api')>()), guardarArchivo }));

const persona = (cambio: Partial<PersonaInformeDto>): PersonaInformeDto => ({
  rol: 'Socio',
  tipoIdentificacion: 'CC',
  identificacion: '79555111',
  nombre: 'Jorge Iván Castro',
  participacion: 100,
  esPep: true,
  vinculoPep: false,
  beneficiarioFinal: true,
  declaracionPep: true,
  indice: 1,
  ...cambio,
});

function montar(p: PersonaInformeDto) {
  renderizar(<DeclaracionPepDePersona empresaId="emp" terceroId="t1" persona={p} />);
}

afterEach(() => vi.clearAllMocks());

describe('Declaración PEP en el detalle de personas del informe SAGRILAFT', () => {
  it('una persona PEP con declaración ofrece el PDF de su posición en la legal', async () => {
    descargarArchivo.mockResolvedValue({ blob: new Blob(['%PDF']), nombre: 'declaracion-pep.pdf' });
    montar(persona({}));

    await userEvent.click(screen.getByRole('button', { name: 'Descargar el PDF de la declaración PEP de Jorge Iván Castro' }));

    await waitFor(() => expect(guardarArchivo).toHaveBeenCalled());
    expect(descargarArchivo).toHaveBeenCalledWith(
      { url: '/api/v1/terceros/t1/declaraciones-pep/pdf', params: { seccion: 'FICHA_LEGAL', persona: 1 } },
      { empresa: 'emp' },
    );
  });

  it('la persona natural (el propio tercero, índice −1) pide la General sin índice de persona', async () => {
    descargarArchivo.mockResolvedValue({ blob: new Blob(['%PDF']), nombre: null });
    montar(persona({ rol: 'Tercero', indice: -1 }));

    await userEvent.click(screen.getByRole('button', { name: /Descargar el PDF de la declaración PEP/ }));

    await waitFor(() => expect(descargarArchivo).toHaveBeenCalled());
    expect(descargarArchivo.mock.calls[0]?.[0]).toEqual({ url: '/api/v1/terceros/t1/declaraciones-pep/pdf', params: { seccion: 'FICHA_GENERAL' } });
  });

  it('un PEP sin declaración lo dice y no ofrece descarga; quien no es PEP no lleva nada', () => {
    montar(persona({ declaracionPep: false }));
    expect(screen.getByText('Sin declaración')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('quien no es PEP no tiene declaración que mostrar', () => {
    montar(persona({ esPep: false, declaracionPep: false }));
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('Sin declaración')).not.toBeInTheDocument();
  });
});
