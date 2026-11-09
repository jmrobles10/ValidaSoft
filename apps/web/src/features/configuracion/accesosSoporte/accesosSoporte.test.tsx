import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import type { EmpresaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import '../../../i18n';
import { DialogoConcederAcceso } from './DialogoConcederAcceso';
import { estadoDe, restante, validarAcceso } from './modelo';

describe('Accesos de soporte (CF-2)', () => {
  it('pide empresa, 1 a 72 horas y una justificación de al menos 10 caracteres', () => {
    expect(validarAcceso({ empresaId: '', horas: 0, justificacion: 'corto' })).toEqual(['empresa-requerida', 'duracion-invalida', 'justificacion-requerida']);
    expect(validarAcceso({ empresaId: 'e1', horas: 72, justificacion: 'Revisar el error al importar' })).toEqual([]);
    expect(validarAcceso({ empresaId: 'e1', horas: 4, justificacion: 'x'.repeat(501) })).toEqual(['texto-demasiado-largo']);
  });

  it('distingue vigente, revocado y expirado y cuánto le queda', () => {
    expect(estadoDe({ vigente: true, revocadoEn: null })).toBe('vigente');
    expect(estadoDe({ vigente: false, revocadoEn: '2026-11-09T10:00:00Z' })).toBe('revocado');
    expect(estadoDe({ vigente: false, revocadoEn: null })).toBe('expirado');
    expect(restante('2026-11-09T12:30:00Z', new Date('2026-11-09T10:00:00Z'))).toEqual({ horas: 2, minutos: 30 });
    expect(restante('2026-11-09T09:00:00Z', new Date('2026-11-09T10:00:00Z'))).toBeNull();
  });

  it('el diálogo explica qué verá el soporte y no concede sin justificación', () => {
    const empresa = { id: 'e1', nombre: 'Constructora Norte S.A.S.', activa: true } as EmpresaDto;
    render(
      <QueryClientProvider client={new QueryClient()}>
        <ThemeProvider theme={createValidasoftTheme('light')}>
          <DialogoConcederAcceso empresas={[empresa]} empresaInicial="e1" onCerrar={vi.fn()} />
        </ThemeProvider>
      </QueryClientProvider>,
    );
    expect(screen.getByText(/Nunca verá ni descargará documentos/)).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole('button', { name: 'Conceder acceso' }).at(-1)!);
    expect(screen.getByRole('textbox', { name: 'Para qué lo necesitan' })).toHaveAttribute('aria-invalid', 'true');
  });
});
