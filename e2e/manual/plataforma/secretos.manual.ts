import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Revisa los secretos en reposo y la clave de arranque',
    resumen:
      'El operador de Plataforma revisa cómo están guardados los secretos de la plataforma: las llaves de OpenAI, los secretos de webhooks, los números de cuenta y las credenciales de listas. Verifica que estén con la clave maestra vigente y revisa el estado de la clave de arranque.',
    precondiciones: ['Un operador de Plataforma con la consola abierta.', 'Secretos guardados con la clave maestra vigente.'],
    relacionados: ['plataforma/operadores', 'plataforma/plan-de-ia-y-llave-openai', 'plataforma/listas-restrictivas-de-la-plataforma'],
  },
  async ({ page }) => {
    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('link', { name: 'Secretos' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Secretos en reposo' })).toBeVisible();
    await expect(consola.getByRole('table').first()).toBeVisible();

    await paso(page, {
      titulo: 'Abre Secretos en reposo',
      explicacion:
        'Algunos datos sensibles se guardan cifrados con la clave maestra de la plataforma: las llaves de OpenAI, los secretos de los webhooks, los números de cuenta de los terceros y las credenciales de las listas restrictivas. ' +
        'La tabla cuenta por tipo cuántos secretos hay, cuántos están con la clave vigente, cuántos esperan re-cifrado y cuántos son ilegibles. Ningún valor se muestra: solo se ven conteos.',
      resaltar: consola.getByRole('table').first(),
      configuracion: {
        ajuste: 'Clave maestra de secretos',
        ruta: 'Plataforma › Secretos (/plataforma/secretos)',
        efecto: 'Decide con qué clave se cifran los secretos guardados. Al rotarla, hay que re-cifrar lo que quedó con la anterior.',
      },
    });

    await paso(page, {
      titulo: 'Lee la clave vigente y las anteriores',
      explicacion:
        'Arriba se indica cuál es la clave maestra vigente y cuántas claves anteriores están configuradas. Si todo está cifrado con la vigente, la pantalla dice que todo está con la clave vigente. ' +
        'Con «Revisar de nuevo» vuelves a contar los secretos sin salir de la pantalla.',
      resaltar: consola.getByText('Clave maestra vigente').first(),
      variantes: [
        { cuando: 'Hay secretos con una clave anterior', entonces: 'Aparecen como «Por re-cifrar» y se pueden re-cifrar con la clave vigente.' },
        {
          cuando: 'Hay secretos ilegibles',
          entonces: 'La pantalla pide agregar su clave a la configuración de claves anteriores; no se pueden re-cifrar hasta entonces.',
        },
      ],
    });

    const claveArranque = consola.getByRole('heading', { name: 'Clave de arranque' });
    await expect(claveArranque).toBeVisible();
    await paso(page, {
      titulo: 'Revisa la clave de arranque',
      explicacion:
        'La clave de arranque es la llave de configuración del servidor que da de alta al primer operador de Plataforma. Aquí ves si está activa y cuántos operadores ya entran con su passkey. ' +
        'Mientras no haya al menos un operador con passkey, no se puede desactivar: nadie podría volver a entrar a la consola.',
      resaltar: claveArranque,
      variantes: [
        {
          cuando: 'Hay operadores con passkey',
          entonces: 'Aparece el aviso «Ya hay operadores con passkey: puedes desactivar la clave de arranque» y el botón «Desactivar» se habilita.',
        },
        { cuando: 'No hay operadores con passkey', entonces: 'El botón «Desactivar» queda deshabilitado y el aviso pide registrar una passkey antes.' },
      ],
      configuracion: {
        ajuste: 'Clave de arranque',
        ruta: 'Plataforma › Secretos › Clave de arranque',
        efecto: 'Sirve solo para dar de alta al primer operador; desactivarla cierra ese acceso de servidor.',
      },
    });
  },
);
