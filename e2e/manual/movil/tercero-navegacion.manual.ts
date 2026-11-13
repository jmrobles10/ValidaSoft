import { expect, paso, recorrido } from '../nucleo';
import { terceroConDosEmpresas } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Navega por la app del tercero desde el celular',
    resumen:
      'En el celular, las secciones del portal están en una barra inferior fija. El tercero, con dos empresas, pasa de Mis empresas a Mi información con un toque y sale de su sesión desde la parte de arriba.',
    precondiciones: [
      'Un tercero invitado por Constructora Norte y por Constructora Sur, con sesión abierta en el portal.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica', 'movil/tercero-campana'],
  },
  async ({ page }) => {
    await terceroConDosEmpresas(page, 'Aceros del Pacífico S.A.S.');

    // 1. La barra inferior.
    await page.goto('/empresas');
    const barra = page.getByRole('navigation', { name: 'Secciones' }).last();
    await expect(barra.getByRole('link', { name: 'Mis empresas' })).toBeVisible();
    await paso(page, {
      titulo: 'Usa la barra inferior para moverte',
      explicacion:
        'En el celular las secciones están abajo, siempre a la vista: Mi bóveda, Mis empresas, Mi información, Mis cuentas y Privacidad. La sección en la que estás queda marcada. ' +
        'Arriba quedan el logotipo, la campana de avisos y «Salir»; el menú de secciones de la parte de arriba solo aparece en la pantalla grande.',
      resaltar: page.getByRole('heading', { level: 2, name: 'Constructora Norte S.A.S.' }),
    });

    // 2. Mis empresas: dos tarjetas.
    await expect(page.getByRole('heading', { level: 2, name: 'Constructora Norte S.A.S.' })).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: 'Constructora Sur S.A.S.' })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa cada empresa en Mis empresas',
      explicacion:
        'Mis empresas muestra una tarjeta por cada empresa con la que tienes un vínculo, y en cada una el estado de tu validación. ' +
        'Con dos empresas, las tarjetas se apilan una debajo de la otra; toca la que quieras para ver su revisión.',
      resaltar: page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: 'Constructora Sur S.A.S.' }) }),
      variantes: [{ cuando: 'Tienes una sola empresa', entonces: 'La lista trae una sola tarjeta y la barra inferior se ve igual.' }],
    });

    // 3. Mi información desde la barra.
    await barra.getByRole('link', { name: 'Mi información' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
    await paso(page, {
      titulo: 'Pasa a Mi información con un toque',
      explicacion:
        'Cada toque en la barra inferior cambia de sección sin volver a entrar. Mi información es tu ficha: los datos generales, tributarios, financieros y de seguridad y salud en el trabajo que pide cada empresa.',
      resaltar: barra.getByRole('link', { name: 'Mi información' }),
    });

    // 4. Salir.
    await paso(page, {
      titulo: 'Sal del portal desde la parte de arriba',
      explicacion:
        '«Salir» cierra tu sesión en este dispositivo. Para volver a entrar, pide el enlace con tu correo: Validasoft no te pide contraseña.',
      resaltar: page.getByRole('button', { name: 'Salir' }),
    });
    await page.getByRole('button', { name: 'Salir' }).click();
    await expect(page.getByText('Cerramos tu sesión en este dispositivo.', { exact: false })).toBeVisible();
  },
);
