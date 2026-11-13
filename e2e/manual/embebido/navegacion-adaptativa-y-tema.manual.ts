import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'embebido',
    actor: 'Revisor/área',
    titulo: 'Usa la barra de secciones en cualquier ancho y en modo oscuro',
    resumen:
      'La barra superior de la app se adapta al ancho del sistema que la aloja: cuando no caben todas las secciones, las que sobran van al botón «Más». ' +
      'El sistema también puede pedir el tema oscuro, y la app lo aplica sin que Rubén cambie nada.',
    precondiciones: [
      'Un grupo con Constructora Norte y Rubén como miembro del área Compras, abierto dentro de la página del sistema integrado.',
      'El sistema de prueba puede cambiar el ancho de su página y enviar el tema a la app.',
    ],
    relacionados: ['embebido/app-de-la-empresa-en-un-iframe', 'configuracion/permisos-por-rol'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await page.setViewportSize({ width: 1536, height: 800 });
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();

    const barra = app.getByRole('navigation', { name: 'Secciones' });
    const mas = barra.locator('button[aria-haspopup="menu"]');
    const secciones = barra.getByRole('link');
    await expect(barra.getByRole('link', { name: 'Bandeja', exact: true })).toBeVisible();

    // Con 1280 px caben todas; al achicar el ancho, las últimas secciones pasan a «Más» (se mide para afirmarlo en la explicación).
    const enlaces: Record<number, number> = {};
    for (const ancho of [1280, 1100, 900, 700]) {
      await page.setViewportSize({ width: ancho, height: 800 });
      await expect(barra).toBeVisible();
      await expect(mas).toHaveCount(ancho === 1280 ? 0 : 1);
      enlaces[ancho] = await secciones.count();
    }
    expect(enlaces[1100]).toBeLessThan(enlaces[1280] ?? 0);
    expect(enlaces[900]).toBeLessThan(enlaces[1100] ?? 0);
    expect(enlaces[700]).toBeLessThan(enlaces[900] ?? 0);
    await page.setViewportSize({ width: 1280, height: 800 });
    await expect(mas).toHaveCount(0);
    await paso(page, {
      titulo: 'Mira cómo se adapta la barra al ancho',
      explicacion:
        'En el ancho de escritorio (la captura principal, 1280 px) las secciones de la empresa caben todas en la barra superior y cada una se abre con un clic. ' +
        'Al achicar el ancho del sistema que aloja la app (capturas de 1100, 900 y 700 px), las últimas secciones pasan al botón «Más» y la barra se queda con las primeras. ' +
        'Se recalcula sola, sin recargar la página.',
      resaltar: barra,
      anchos: [1100, 900, 700],
      variantes: [
        { cuando: 'Tu rol no tiene acceso a una sección', entonces: 'Esa sección no aparece en la barra: la barra solo muestra lo que puedes usar.' },
        { cuando: 'La sección en la que estás quedó dentro de «Más»', entonces: 'El botón lleva el nombre de esa sección (por ejemplo «Bandeja») en lugar de «Más» y se ve marcado.' },
      ],
    });

    // En un celular (390 px) la barra se reduce a un selector con la sección en la que estás; el menú lista las demás.
    await page.setViewportSize({ width: 390, height: 844 });
    const selector = barra.locator('button[aria-haspopup="menu"]');
    await expect(selector).toBeVisible();
    await selector.click();
    await expect(app.getByRole('menuitem', { name: 'Excepciones', exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Cambia de sección desde el selector del celular',
      explicacion:
        'En un celular la barra no cabe: aparece un solo botón con el nombre de la sección en la que estás, en este caso Bandeja. Al abrirlo, el menú lista todas las secciones a las que tienes acceso para que elijas la que necesitas. ' +
        'La barra se recalcula sola al cambiar el ancho, sin recargar la página.',
      resaltar: barra,
      celular: true,
    });
    await page.keyboard.press('Escape');

    await page.evaluate(() => {
      const marco = document.getElementById('validasoft') as HTMLIFrameElement | null;
      marco?.contentWindow?.postMessage({ tipo: 'validasoft:tema', mode: 'dark' }, '*');
    });
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await paso(page, {
      titulo: 'Usa el tema oscuro del sistema',
      explicacion:
        'Si tu sistema tiene el tema oscuro activo, envía su preferencia a la app y Validasoft cambia a colores oscuros al momento. ' +
        'La app no guarda esa preferencia por su cuenta: sigue el tema que le indique el sistema que la aloja.',
      resaltar: app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' }),
      variantes: [{ cuando: 'El sistema envía tema claro', entonces: 'La app vuelve al tema claro.' }],
    });
    await page.setViewportSize({ width: 1536, height: 800 });
  },
);
