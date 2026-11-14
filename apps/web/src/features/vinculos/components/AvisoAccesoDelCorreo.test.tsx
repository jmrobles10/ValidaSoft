import { ThemeProvider } from '@mui/material';
import { render, screen } from '@testing-library/react';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it } from 'vitest';
import { AvisoAccesoDelCorreo } from './AvisoAccesoDelCorreo';

function montar(vinculo: Parameters<typeof AvisoAccesoDelCorreo>[0]['vinculo']) {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <AvisoAccesoDelCorreo vinculo={vinculo} />
    </ThemeProvider>,
  );
}

describe('aviso del acceso del correo invitado (acceso aislado)', () => {
  it('pendiente: dice que nada de la bóveda del tercero se comparte y que los contactos ya fueron avisados', () => {
    montar({ accesoDelCorreo: { estado: 'Pendiente', correo: 'compras@empresa-a.co', desde: '2026-11-12T15:00:00Z', resueltoEn: null } });

    expect(screen.getByText('El acceso de compras@empresa-a.co espera la aprobación del tercero')).toBeInTheDocument();
    expect(screen.getByText(/no es un contacto conocido de este tercero/)).toBeInTheDocument();
    expect(screen.getByText(/no se comparte con tu empresa nada de la bóveda ni de la información del tercero/)).toBeInTheDocument();
    expect(screen.getByText(/desde el 12\/11\/2026/)).toBeInTheDocument();
  });

  it('rechazado: dice que se cerró su sesión y que hay que invitar a otro correo', () => {
    montar({ accesoDelCorreo: { estado: 'Rechazado', correo: 'compras@empresa-a.co', desde: '2026-11-12T15:00:00Z', resueltoEn: '2026-11-13T10:00:00Z' } });

    expect(screen.getByText('El tercero rechazó el acceso de compras@empresa-a.co')).toBeInTheDocument();
    expect(screen.getByText(/El 13\/11\/2026 los contactos del tercero rechazaron ese correo/)).toBeInTheDocument();
    expect(screen.getByText(/Invita a otro correo con «Reenviar invitación»/)).toBeInTheDocument();
  });

  it('aprobado: lo dice con la fecha de la aprobación', () => {
    montar({ accesoDelCorreo: { estado: 'Aprobado', correo: 'compras@empresa-a.co', desde: '2026-11-12T15:00:00Z', resueltoEn: '2026-11-13T10:00:00Z' } });

    expect(screen.getByText('El tercero aprobó el acceso de compras@empresa-a.co')).toBeInTheDocument();
    expect(screen.getByText(/El 13\/11\/2026 un contacto del tercero aprobó a compras@empresa-a\.co/)).toBeInTheDocument();
  });

  it('pendiente por desplazamiento: dice que otro contacto del tercero se verificó y que el verificado prevalece', () => {
    montar({
      accesoDelCorreo: { estado: 'Pendiente', correo: 'creador@empresa-a.co', desde: '2026-11-14T15:00:00Z', resueltoEn: null, motivo: 'ContactoDesplazado' },
    });

    expect(screen.getByText('El acceso de creador@empresa-a.co pasó a acceso aislado')).toBeInTheDocument();
    expect(screen.getByText(/estaba como contacto del tercero sin verificar y, desde el 14\/11\/2026, otro contacto del tercero quedó verificado: el verificado prevalece/)).toBeInTheDocument();
    expect(screen.getByText(/lo que ya recibió se conserva/)).toBeInTheDocument();
  });

  it('un vínculo con correo de confianza no lleva aviso', () => {
    const { container } = montar({ accesoDelCorreo: null });
    expect(container).toBeEmptyDOMElement();
    expect(montar({}).container).toBeEmptyDOMElement();
  });
});
