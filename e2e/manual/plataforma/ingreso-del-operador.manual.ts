import { randomUUID } from 'node:crypto';
import { CLAVE_PLATAFORMA, llamar } from '../../soporte/api';
import { autenticadorVirtual } from '../../soporte/autenticador';
import { codigo, esperarCorreo } from '../../soporte/correo';
import { expect, paso, recorrido } from '../nucleo';

const ASUNTO_CODIGO = /Tu código para ingresar a Validasoft/;

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Entra a la consola de Plataforma con correo, código y passkey',
    resumen:
      'Un operador de Plataforma entra a la consola sin contraseña: pide un código a su correo, lo escribe, registra una passkey en su primer ingreso y, en los siguientes, entra con esa passkey.',
    precondiciones: ['Un operador de Plataforma dado de alta con su correo (ingreso directo).', 'Acceso al correo del operador para leer el código.'],
    relacionados: ['plataforma/operadores', 'plataforma/ingresos-de-los-operadores', 'acceso/ingreso-con-passkey'],
  },
  async ({ page }) => {
    const correo = `paula.${randomUUID().replace(/-/g, '').slice(0, 8)}@e2e.validasoft.co`;
    await llamar('POST', '/api/v1/plataforma/operadores', { llave: CLAVE_PLATAFORMA, cuerpo: { nombre: 'Paula Plataforma', correo } });
    await autenticadorVirtual(page);

    await page.goto('/app/');
    await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
    await paso(page, {
      titulo: 'Escribe tu correo',
      explicacion:
        'Validasoft no usa contraseña para los operadores. Escribe el correo con el que te dieron de alta y pulsa «Continuar». ' +
        'Si el correo no corresponde a un operador de ingreso directo, la pantalla responde lo mismo que para cualquier otro correo: así no se revela quién tiene acceso.',
      resaltar: page.getByLabel('Correo'),
      configuracion: {
        ajuste: 'Ingreso directo de operadores',
        ruta: 'Plataforma › Operadores (/plataforma/operadores)',
        efecto: 'Solo quien está dado de alta como operador con ingreso directo recibe el código.',
      },
    });

    await page.getByLabel('Correo').fill(correo);
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa tu correo',
      explicacion:
        'Validasoft te envía un correo con un código de seis casillas. Ábrelo y copia el código: vence pronto y, si te equivocas varias veces, hay que pedir uno nuevo. ' +
        'El código no es tu contraseña: sirve solo para este ingreso.',
      resaltar: page.getByRole('heading', { name: 'Revisa tu correo' }),
      variantes: [
        { cuando: 'El código venció', entonces: 'La pantalla dice que el código venció y ofrece pedir uno nuevo.' },
        { cuando: 'Se agotaron los intentos', entonces: 'Se bloquea ese código y hay que pedir uno nuevo.' },
      ],
    });

    const correoDelCodigo = await esperarCorreo(correo, ASUNTO_CODIGO);
    await page.keyboard.type(codigo(correoDelCodigo));
    await expect(page.getByRole('heading', { name: 'Registra tu passkey' })).toBeVisible();
    await paso(page, {
      titulo: 'Registra tu passkey',
      explicacion:
        'En tu primer ingreso registras una passkey: una llave del dispositivo (huella, rostro o PIN) que reemplaza a la contraseña. Pulsa «Registrar passkey» y confirma con tu dispositivo. ' +
        'Para los operadores la passkey es obligatoria: sin ella no se puede entrar a la consola.',
      resaltar: page.getByRole('button', { name: 'Registrar passkey' }),
      configuracion: {
        ajuste: 'Passkey obligatoria para operadores',
        efecto: 'Sin una passkey registrada, la cuenta no puede entrar a la consola.',
      },
    });

    await page.getByRole('button', { name: 'Registrar passkey' }).click();
    await expect(page.getByRole('heading', { name: 'Grupos y empresas' })).toBeVisible();
    await paso(page, {
      titulo: 'Llega a la consola',
      explicacion:
        'Una cuenta de operador no pertenece a ninguna empresa: va directo a la consola de Plataforma,. ' +
        'Desde aquí administras grupos, operadores, uso de IA, ingresos y secretos.',
      resaltar: page.getByRole('navigation').first(),
    });

    await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
    await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
    await expect(page.getByRole('button', { name: 'Entrar con una passkey' })).toBeVisible();
    await paso(page, {
      titulo: 'Entra en otro ingreso con tu passkey',
      explicacion:
        'Al cerrar sesión vuelves a la pantalla de ingreso. Para entrar otra vez no necesitas código: pulsa «Entrar con una passkey» y confirma con tu dispositivo. ' +
        'Si perdiste la passkey, pide a otro operador que la revoque y registra una nueva.',
      resaltar: page.getByRole('button', { name: 'Entrar con una passkey' }),
    });

    await page.getByRole('button', { name: 'Entrar con una passkey' }).click();
    await expect(page.getByRole('heading', { name: 'Grupos y empresas' })).toBeVisible();
  },
);
