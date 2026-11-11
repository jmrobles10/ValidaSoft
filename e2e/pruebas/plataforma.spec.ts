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

  // La lista es paginada (con cientos de grupos la nuestra no está en la primera página): se busca por la razón social.
  await consola.getByRole('link', { name: 'Grupos y empresas' }).first().click();
  await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(empresa);
  const fila = consola.getByRole('row', { name: new RegExp(literal(empresa)) });
  await expect(fila).toContainText(`Activo · llave ••••${ultimos}`);
  // La llave nunca se ve en claro en la consola.
  await expect(consola.locator('body')).not.toContainText(llaveOpenAi);

  // 4. La empresa nueva opera: su sistema invita a un proveedor en nombre de la administradora.
  const grupos = await llamar<{ datos: { empresas: { id: string; nombre: string }[] }[] }>('GET', `/api/v1/plataforma/grupos?q=${encodeURIComponent(empresa)}`, {
    llave: CLAVE_PLATAFORMA,
  });
  const empresaId = grupos.datos.flatMap((g) => g.empresas).find((e) => e.nombre === empresa)?.id ?? '';
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

/**
 * La lista de grupos de la consola es paginada (PT-9) y de scroll infinito virtualizado (PT-11): pide la primera página
 * de 20, trae la siguiente con el cursor al acercarse al final, solo mantiene en el DOM las tarjetas que se ven, y cambiar
 * a «Secretos» y volver es inmediato aunque la base tenga cientos de grupos.
 */
test('La consola pagina los grupos con scroll infinito y cambia de pestaña al instante', async ({ page }) => {
  const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);
  const total = 30;
  const nombre = (i: number) => `Paginado ${sufijo} ${String(i).padStart(2, '0')}`;
  await Promise.all(
    Array.from({ length: total }, (_, i) =>
      llamar('POST', '/api/v1/plataforma/tenants', {
        llave: CLAVE_PLATAFORMA,
        cuerpo: { slug: `pg${sufijo}${String(i).padStart(2, '0')}`, nombre: nombre(i), region: 'co', zonaHoraria: 'America/Bogota' },
      }),
    ),
  );

  const peticiones: URL[] = [];
  page.on('request', (r) => {
    const url = new URL(r.url());
    if (url.pathname === '/api/v1/plataforma/grupos') peticiones.push(url);
  });
  const consola = await abrirConsolaDePlataforma(page);
  const texto = `Paginado ${sufijo}`;
  const delTexto = () => peticiones.filter((p) => p.searchParams.get('q') === texto);
  await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(texto);
  await expect.poll(() => delTexto().length).toBe(1);
  await expect(consola.getByText(nombre(0), { exact: true })).toBeVisible();

  // Primera página: 20 grupos pedidos y solo unas pocas tarjetas dibujadas.
  const consulta = delTexto();
  expect(consulta[0]?.searchParams.get('limite')).toBe('20');
  expect(consulta[0]?.searchParams.has('cursor')).toBe(false);
  const tarjetas = consola.getByRole('list', { name: 'Grupos' }).getByRole('listitem');
  expect(await tarjetas.count()).toBeLessThan(total);
  await expect(consola.getByRole('button', { name: /cargar más/i })).toHaveCount(0);

  // Al desplazar hasta el final trae el resto con el cursor; el último grupo aparece y no queda nada más por pedir.
  await expect(async () => {
    await consola.locator('main').evaluate((m) => m.scrollTo({ top: m.scrollHeight }));
    await expect(consola.getByText(nombre(total - 1), { exact: true })).toBeVisible({ timeout: 1_000 });
  }).toPass({ timeout: 15_000 });
  await expect(consola.getByText(`Eso es todo: ${String(total)} grupos.`)).toBeVisible();
  const paginas = delTexto();
  expect(paginas).toHaveLength(2);
  expect(paginas[1]?.searchParams.get('cursor')).toBeTruthy();
  expect(await tarjetas.count()).toBeLessThan(total);

  // Pasar a «Secretos» es inmediato (la lista anterior se va con la ruta) y volver muestra lo ya cargado.
  const inicio = Date.now();
  await consola.getByRole('link', { name: 'Secretos' }).click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Secretos en reposo' })).toBeVisible();
  await expect(consola.getByRole('list', { name: 'Grupos' })).toHaveCount(0);
  expect(Date.now() - inicio).toBeLessThan(3_000);
  await consola.getByRole('link', { name: 'Grupos y empresas' }).first().click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Grupos y empresas' })).toBeVisible();
});
