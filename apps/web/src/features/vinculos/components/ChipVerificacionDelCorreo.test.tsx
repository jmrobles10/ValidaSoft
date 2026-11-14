import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it } from 'vitest';
import { ChipVerificacionDelCorreo } from './ChipVerificacionDelCorreo';

function montar(verificacion: Parameters<typeof ChipVerificacionDelCorreo>[0]['verificacion']) {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <ChipVerificacionDelCorreo verificacion={verificacion} />
    </ThemeProvider>,
  );
}

describe('verificación del correo del vínculo (decisión AV)', () => {
  it('verificado: dice con qué se probó que el correo es del tercero', () => {
    montar({ estado: 'Verificado', medio: 'CorreoDelRut' });

    expect(screen.getByText('Verificado · correo del RUT')).toBeInTheDocument();
  });

  it.each([
    ['CorreoDeLaCamara', 'Verificado · correo de la Cámara de comercio'],
    ['Corroborado', 'Verificado · invitado por dos empresas distintas'],
    ['Plataforma', 'Verificado · verificado por la plataforma'],
    ['Importacion', 'Verificado · traído del sistema de origen'],
    ['AprobadoPorContacto', 'Verificado · aprobado por un contacto verificado'],
  ])('verificado por %s', (medio, texto) => {
    montar({ estado: 'Verificado', medio });
    expect(screen.getByText(texto)).toBeInTheDocument();
  });

  it('sin verificar: es contacto del tercero, pero nada prueba aún que sea suyo', () => {
    montar({ estado: 'SinVerificar', medio: null });

    expect(screen.getByText('Sin verificar')).toBeInTheDocument();
    expect(screen.queryByText(/·/)).not.toBeInTheDocument();
  });

  it('aún no es contacto: entra con acceso aislado', () => {
    montar({ estado: 'NoEsContacto', medio: null });

    expect(screen.getByText('Aún no es contacto')).toBeInTheDocument();
  });

  it('sin el dato (el listado de vínculos no lo trae) no muestra nada', () => {
    expect(montar(null).container).toBeEmptyDOMElement();
    expect(montar(undefined).container).toBeEmptyDOMElement();
  });
});
