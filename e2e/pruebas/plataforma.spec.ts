import { expect, test } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { asercion, CLAVE_PLATAFORMA, clavesDeSistema, llamar, nitAleatorio } from '../soporte/api';
import { esperarCorreo, literal } from '../soporte/correo';
import { abrirEmbebidoCon, sesionDeUsuario } from '../soporte/embebido';
import { abrirConsolaDePlataforma } from '../soporte/plataforma';
import { ORIGEN_SISTEMA } from '../soporte/semilla';

/**
 * Consola de Plataforma (PT-4…PT-6): un operador da de alta un grupo nuevo con su primera empresa, su administradora y su
 * sistema integrado; configura el plan de IA de la empresa con su llave de OpenAI (que nunca vuelve en claro) y la prueba;
 * y la empresa nueva queda operable: su sistema invita a un proveedor y la administradora lo ve en Validasoft.
 */
test('Alta de una empresa nueva desde la consola, su plan de IA con llave y su primera invitación', async ({ page, browser }) => {
  const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);
  const nit = nitAleatorio();
  const grupo = `Grupo Andino ${sufijo}`;
  const empresa = `Obras Andinas ${sufijo} S.A.S.`;
  const prefijo = `AN${sufijo.slice(0, 4).toUpperCase()}`;
  const admin = { sub: 'camila', nombre: 'Camila Administradora', correo: `camila.${sufijo}@andina.e2e` };
  const claves = clavesDeSistema();
  const llaveOpenAi = `sk-proj-e2e-${randomUUID().replace(/-/g, '')}`;
  const ultimos = llaveOpenAi.slice(-4);

  // 1. El operador da de alta el grupo desde la consola.
  const consola = await abrirConsolaDePlataforma(page);
  await expect(consola.getByRole('heading', { level: 1, name: 'Grupos y empresas' })).toBeVisible();
  await consola.getByRole('link', { name: 'Nuevo grupo' }).click();
  await consola.getByRole('textbox', { name: 'Nombre del grupo' }).fill(grupo);
  await expect(consola.getByRole('textbox', { name: 'Identificador' })).toHaveValue(`grupo-andino-${sufijo}`);
  await consola.getByRole('textbox', { name: 'Número de identificación' }).fill(nit.numero);
  await consola.getByRole('textbox', { name: 'Razón social' }).fill(empresa);
  await consola.getByRole('textbox', { name: 'Prefijo de folio' }).fill(prefijo);
  await consola.getByRole('textbox', { name: 'Nombre', exact: true }).fill(admin.nombre);
  await consola.getByRole('textbox', { name: 'Correo' }).fill(admin.correo);
  await consola.getByRole('textbox', { name: 'Su usuario en el sistema (opcional)' }).fill(admin.sub);
  await consola.getByRole('textbox', { name: 'Código' }).fill('ERP');
  await consola.getByRole('textbox', { name: 'Nombre del sistema' }).fill('ERP de Andina');
  await consola.getByRole('textbox', { name: 'Clave pública (PEM)' }).fill(claves.publica);
  await consola.getByRole('textbox', { name: 'Sitios que pueden incrustar Validasoft (opcional)' }).fill(ORIGEN_SISTEMA);
  await consola.getByRole('button', { name: 'Crear grupo' }).click();

  // 2. Las llaves se muestran una sola vez: la del sistema la usa su backend.
  await expect(consola.getByRole('heading', { level: 1, name: `Grupo ${grupo} creado` })).toBeVisible();
  const llaveSistema = (await consola.getByLabel('API key del sistema ERP', { exact: true }).textContent())?.trim() ?? '';
  expect(llaveSistema).toMatch(/^vsk_dev_grupo-andino-/);

  // 3. Plan de IA de la empresa nueva con su llave de OpenAI; «Probar» solo cuando se pide (analizador simulado).
  await consola.getByRole('link', { name: `Configurar el plan de IA de ${empresa}` }).click();
  await expect(consola.getByText('Esta empresa aún no tiene plan', { exact: false })).toBeVisible();
  await consola.getByRole('textbox', { name: 'Validaciones incluidas por mes' }).fill('300');
  await consola.getByLabel('Permitir validaciones adicionales').check();
  await consola.getByRole('textbox', { name: 'Tope de adicionales por mes' }).fill('100');
  const campoLlave = consola.getByLabel('Nueva API key de OpenAI');
  await expect(campoLlave).toHaveAttribute('type', 'password');
  await campoLlave.fill(llaveOpenAi);
  await consola.getByRole('button', { name: 'Probar la nueva llave' }).click();
  await expect(consola.getByText(`OpenAI aceptó la llave ••••${ultimos}.`)).toBeVisible();
  await consola.getByRole('button', { name: 'Guardar plan' }).click();
  await expect(consola.getByText(`Configurada · ••••${ultimos}`)).toBeVisible();
  await expect(consola.getByLabel('Nueva API key de OpenAI')).toHaveCount(0);
  // La guardada también se prueba, sin volver a escribirla.
  await consola.getByRole('button', { name: 'Probar llave' }).click();
  await expect(consola.getByText(`OpenAI aceptó la llave ••••${ultimos}.`)).toBeVisible();

  await consola.getByRole('link', { name: 'Grupos y empresas' }).first().click();
  const fila = consola.getByRole('row', { name: new RegExp(literal(empresa)) });
  await expect(fila).toContainText(`Activo · llave ••••${ultimos}`);
  // La llave nunca se ve en claro en la consola.
  await expect(consola.locator('body')).not.toContainText(llaveOpenAi);

  // 4. La empresa nueva opera: su sistema invita a un proveedor en nombre de la administradora.
  const grupos = await llamar<{ empresas: { id: string; nombre: string }[] }[]>('GET', '/api/v1/plataforma/grupos', { llave: CLAVE_PLATAFORMA });
  const empresaId = grupos.flatMap((g) => g.empresas).find((e) => e.nombre === empresa)?.id ?? '';
  expect(empresaId).not.toBe('');
  const delegado = () => asercion('ERP', claves.privada, admin);
  const correoProveedor = `compras.${sufijo}@proveedor.e2e`;
  const proveedor = `Aceros ${sufijo} S.A.S.`;
  await llamar('POST', '/api/v1/vinculos', {
    llave: llaveSistema,
    empresa: empresaId,
    delegado: delegado(),
    cuerpo: {
      tercero: {
        identificacion: nitAleatorio(),
        tipoPersona: 'Juridica',
        nombre: proveedor,
        nombreComercial: null,
        contactos: [{ nombre: 'Compras', correo: correoProveedor, telefono: null, rol: 'Comercial' }],
      },
      tipoVinculo: 'PROVEEDOR',
      atributos: {},
      correoInvitacion: correoProveedor,
      referenciaExterna: { sistema: 'ERP', id: `PRV-${sufijo}` },
      enviarInvitacion: true,
    },
  });
  const invitacion = await esperarCorreo(correoProveedor, new RegExp(literal(empresa)));
  expect(invitacion.texto).toContain('/e/');

  // 5. La administradora entra desde su sistema a la empresa nueva y ve al proveedor invitado.
  const pagina = await (await browser.newContext()).newPage();
  const app = await abrirEmbebidoCon(pagina, { empresaId, rutaInicial: 'vinculos', sesion: () => sesionDeUsuario(llaveSistema, delegado()) });
  await expect(app.getByRole('row').filter({ hasText: proveedor })).toContainText('Invitado');
});
