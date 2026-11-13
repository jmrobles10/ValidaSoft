// Pruebas del generador del manual: texto, Markdown de un recorrido, páginas HTML y la referencia de la API (esquemas, ejemplos y lectura del código C#).
import { describe, expect, it } from 'vitest';
import { aplanar, ejemplo, normalizar, tipoLegible } from './api-esquema.mjs';
import { accesoDeAtributos, analizarCSharp, describirAcceso, erroresDelCuerpo, leerPermisos, xmlATexto } from './api-fuente.mjs';
import { construirModeloApi, htmlDeApi, markdownDeApi, nombreLegible } from './api.mjs';
import { aSlug, escapar, medidasPng, parrafos, textoEnHtml, validarManifiesto } from './comun.mjs';
import { leerEncabezado, markdownDeRecorrido } from './markdown.mjs';
import { construirSitioDeRecorridos } from './sitio.mjs';

const CATALOGO = {
  actores: [{ nombre: 'Tercero', slug: 'tercero', descripcion: 'Quien valida su información.' }],
  dominios: [{ slug: 'tercero', nombre: 'Portal del tercero', descripcion: 'Lo que hace el tercero.' }],
};

const RECORRIDO = {
  version: 1,
  id: 'tercero/invitacion',
  dominio: 'tercero',
  archivo: 'invitacion',
  titulo: 'Acepta la invitación',
  actor: 'Tercero',
  resumen: 'El tercero acepta la invitación de una empresa y queda listo para subir documentos.',
  precondiciones: ['Una empresa con un flujo publicado.'],
  relacionados: ['tercero/otro'],
  generado: '2026-11-12T00:00:00.000Z',
  carpeta: '/no/existe',
  pasos: [
    {
      orden: 1,
      titulo: 'Abre el enlace',
      explicacion: 'Abre el enlace del correo.\n\nVerás **la invitación** y el código `123456`. Sigue con [[tercero/otro]].',
      variantes: [{ cuando: 'Eres persona natural', entonces: 'Ves la cédula.' }],
      configuracion: [{ ajuste: 'Textos legales', ruta: '/empresas', efecto: 'Cambian lo que aceptas.' }],
      ruta: '/e/{enlace}',
      imagenes: { claro: '01-abre-el-enlace-claro.png', celular: '01-abre-el-enlace-celular.png' },
      resaltados: 1,
      completa: false,
    },
  ],
};

const OTRO = { ...RECORRIDO, id: 'tercero/otro', archivo: 'otro', titulo: 'Otro recorrido', relacionados: [] };

describe('texto', () => {
  it('escapa HTML y aplica el formato de las explicaciones', () => {
    expect(escapar('<a href="x">&</a>')).toBe('&lt;a href=&quot;x&quot;&gt;&amp;&lt;/a&gt;');
    const html = textoEnHtml('Ve a **Inicio** y `pnpm manual` <script>', () => null);
    expect(html).toBe('Ve a <strong>Inicio</strong> y <code>pnpm manual</code> &lt;script&gt;');
  });

  it('un enlace [[dominio/archivo]] apunta al recorrido o, si no existe, queda como texto', () => {
    const enlace = (id) => (id === 'a/b' ? { href: '../a/b.html', titulo: 'B' } : null);
    expect(textoEnHtml('Mira [[a/b]] y [[c/d]]', enlace)).toBe('Mira <a href="../a/b.html">B</a> y <em>c/d</em>');
  });

  it('aSlug quita tildes y signos; parrafos separa por línea en blanco', () => {
    expect(aSlug('Más información: «Mi cuenta»')).toBe('mas-informacion-mi-cuenta');
    expect(parrafos('uno\ndos\n\ntres')).toEqual(['uno dos', 'tres']);
  });

  it('lee las medidas de un PNG', () => {
    const png = Buffer.alloc(24);
    png.writeUInt32BE(0x89504e47, 0);
    png.writeUInt32BE(1280, 16);
    png.writeUInt32BE(800, 20);
    expect(medidasPng(png)).toEqual({ ancho: 1280, alto: 800 });
    expect(medidasPng(Buffer.from('no es png'))).toBeNull();
  });

  it('valida el manifiesto de un recorrido', () => {
    expect(validarManifiesto(RECORRIDO, CATALOGO)).toBeNull();
    expect(validarManifiesto({ ...RECORRIDO, dominio: 'inventado' }, CATALOGO)).toMatch(/dominio desconocido/);
    expect(validarManifiesto({ ...RECORRIDO, actor: 'Nadie' }, CATALOGO)).toMatch(/actor desconocido/);
    expect(validarManifiesto({ ...RECORRIDO, pasos: [] }, CATALOGO)).toBe('sin pasos');
    expect(validarManifiesto({ ...RECORRIDO, version: 2 }, CATALOGO)).toMatch(/versión/);
  });
});

describe('recorridos', () => {
  it('el Markdown lleva el encabezado, los pasos, las variantes, la configuración y los enlaces relativos', () => {
    const porId = new Map([[RECORRIDO.id, RECORRIDO], [OTRO.id, OTRO]]);
    const md = markdownDeRecorrido(RECORRIDO, { dominio: CATALOGO.dominios[0], carpetaDocs: '/repo/docs/manual', carpetaImagenes: '/repo/salida/manual/img', porId });
    expect(leerEncabezado(md)).toMatchObject({ id: 'tercero/invitacion', actor: 'Tercero', pasos: 1 });
    expect(md).toContain('## Paso 1. Abre el enlace');
    expect(md).toContain('![Paso 1: Abre el enlace (escritorio)](../../../salida/manual/img/tercero/invitacion/01-abre-el-enlace-claro.png)');
    expect(md).toContain('(celular)');
    expect(md).toContain('**Eres persona natural:** Ves la cédula.');
    expect(md).toContain('**Textos legales** (`/empresas`): Cambian lo que aceptas.');
    expect(md).toContain('[Otro recorrido](otro.md)');
    expect(md).not.toContain('2026-11-12');
  });

  it('las páginas HTML incluyen portada, actor, dominio y el recorrido con sus pasos, y el buscador lo encuentra', () => {
    const sitio = construirSitioDeRecorridos([RECORRIDO, OTRO], CATALOGO, { generado: 'hoy', totalEndpoints: 3 });
    const rutas = sitio.paginas.map((p) => p.ruta);
    expect(rutas).toEqual(expect.arrayContaining(['index.html', 'actor/tercero.html', 'dominio/tercero.html', 'r/tercero/invitacion.html']));
    const pagina = sitio.paginas.find((p) => p.ruta === 'r/tercero/invitacion.html').html;
    expect(pagina).toContain('<h1>Acepta la invitación</h1>');
    expect(pagina).toContain('id="paso-1"');
    expect(pagina).toContain('Variantes');
    expect(pagina).toContain('Configuración que lo afecta');
    expect(pagina).toContain('<strong>la invitación</strong>');
    expect(pagina).toContain('<a href="../../r/tercero/otro.html">Otro recorrido</a>');
    expect(sitio.indice.find((i) => i.u === 'r/tercero/invitacion.html').x).toContain('Ves la cédula');
    expect(sitio.imagenes).toHaveLength(4);
  });
});

describe('esquemas OpenAPI', () => {
  const componentes = {
    Arbol: { type: 'object', required: ['nombre'], properties: { nombre: { type: 'string' }, hijos: { type: ['null', 'array'], items: { $ref: '#/components/schemas/Arbol' } } } },
    Estado: { type: 'string', enum: ['Abierto', 'Cerrado'] },
    Pieza: { type: 'object', required: ['id'], properties: { id: { type: 'string', format: 'uuid' }, estado: { $ref: '#/components/schemas/Estado' }, correo: { type: 'string' } } },
    Ficha: {
      type: 'object',
      properties: {
        publicada: { oneOf: [{ type: 'null' }, { $ref: '#/components/schemas/Pieza' }] },
        borrador: { oneOf: [{ type: 'null' }, { $ref: '#/components/schemas/Pieza' }] },
      },
    },
  };

  it('normaliza referencias, nulos y enums', () => {
    const n = normalizar({ oneOf: [{ type: 'null' }, { $ref: '#/components/schemas/Pieza' }] }, componentes);
    expect(n.nulo).toBe(true);
    expect(n.nombre).toBe('Pieza');
    expect(tipoLegible(normalizar({ $ref: '#/components/schemas/Estado' }, componentes), componentes)).toBe('Estado (texto)');
    expect(tipoLegible(normalizar({ type: 'string', format: 'date-time' }, componentes), componentes)).toBe('fecha y hora');
  });

  it('aplana con rutas, marca lo requerido y los valores de un enum', () => {
    const filas = aplanar({ $ref: '#/components/schemas/Pieza' }, componentes);
    expect(filas.map((f) => f.ruta)).toEqual(['id', 'estado', 'correo']);
    expect(filas[0]).toMatchObject({ requerido: true, tipo: 'uuid' });
    expect(filas[1].detalle).toContain('valores: Abierto · Cerrado');
  });

  it('un tipo recursivo no se expande sin fin', () => {
    const filas = aplanar({ $ref: '#/components/schemas/Arbol' }, componentes);
    expect(filas.map((f) => f.ruta)).toEqual(['nombre', 'hijos']);
    expect(filas[1].detalle).toContain('recursivo: ver Arbol');
  });

  it('un tipo repetido se detalla una sola vez', () => {
    const filas = aplanar({ $ref: '#/components/schemas/Ficha' }, componentes);
    expect(filas.map((f) => f.ruta)).toEqual(['publicada', 'publicada.id', 'publicada.estado', 'publicada.correo', 'borrador']);
    expect(filas.at(-1).detalle).toContain('mismos campos que `publicada`');
  });

  it('construye ejemplos plausibles, con el primer valor de cada enum y un solo nivel de recursión', () => {
    const e = ejemplo({ $ref: '#/components/schemas/Pieza' }, componentes);
    expect(e).toEqual({ id: '3fa85f64-5717-4562-b3fc-2c963f66afa6', estado: 'Abierto', correo: 'contacto@empresa.co' });
    const arbol = ejemplo({ $ref: '#/components/schemas/Arbol' }, componentes);
    expect(arbol).toEqual({ nombre: 'Constructora Norte S.A.S.', hijos: [{}] });
  });
});

describe('código de los endpoints', () => {
  const FUENTE = `
namespace Validasoft.Aplicacion.Flujos;

/// <summary>Flujos aprobatorios de la empresa activa (<c>X-Empresa</c>).</summary>
public static class FlujosEndpoints
{
    /// <summary>Crea el flujo como borrador v1.</summary>
    [WolverinePost("/api/v1/flujos/{id:guid}")]
    [Authorize(Policy = Permisos.FlujosEscribir)]
    public static async Task<IResult> Crear(CrearFlujoDto dto, SesionDeEmpresa empresa, CancellationToken ct)
    {
        if (x) throw ReglaDeNegocioException.Conflicto("flujo-duplicado", $"La empresa ya tiene {codigo}.");
        throw ReglaDeNegocioException.Validacion("tipo-vinculo-invalido", "x");
    }

    [WolverineGet("/public/v1/algo")]
    [AllowAnonymous]
    public static string Leer() => "ok";
}`;

  it('lee ruta, descripción, permiso, empresa activa y errores', () => {
    const analizado = analizarCSharp(FUENTE);
    expect(analizado.clase).toBe('Validasoft.Aplicacion.Flujos.FlujosEndpoints');
    expect(analizado.resumenDeClase).toBe('Flujos aprobatorios de la empresa activa (`X-Empresa`).');
    const [crear, leer] = analizado.endpoints;
    expect(crear).toMatchObject({
      metodo: 'POST',
      ruta: '/api/v1/flujos/{id}',
      resumen: 'Crea el flujo como borrador v1.',
      alcanceEmpresa: true,
      acceso: { tipo: 'politica', politica: 'Permisos.FlujosEscribir' },
    });
    expect(crear.errores).toEqual([
      { estado: 409, codigo: 'flujo-duplicado' },
      { estado: 422, codigo: 'tipo-vinculo-invalido' },
    ]);
    expect(leer).toMatchObject({ metodo: 'GET', alcanceEmpresa: false, acceso: { tipo: 'publico' } });
    expect(leer.errores).toEqual([]);
  });

  it('resuelve los permisos y describe el acceso', () => {
    const permisos = leerPermisos('public const string FlujosEscribir = "flujos:escribir";');
    expect(describirAcceso(accesoDeAtributos('[Authorize(Policy = Permisos.FlujosEscribir)]'), permisos)).toEqual({ etiqueta: 'Permiso flujos:escribir', scope: 'flujos:escribir' });
    expect(describirAcceso(accesoDeAtributos('[Authorize]'), permisos).etiqueta).toBe('Cualquier sesión autenticada');
    expect(describirAcceso(accesoDeAtributos('[Authorize(Policy = ReclamosTercero.Politica)]'), permisos).etiqueta).toBe('Sesión del tercero');
    expect(describirAcceso(null, permisos).etiqueta).toBeNull();
  });

  it('el comentario XML pasa a texto y los errores fijos tienen su código', () => {
    expect(xmlATexto('Usa <c>X</c> y <see cref="Foo.Bar"/> &lt;ya&gt;')).toBe('Usa `X` y `Bar` <ya>');
    expect(erroresDelCuerpo('throw ReglaDeNegocioException.DemasiadoGrande("x");')).toEqual([{ estado: 413, codigo: 'archivo-demasiado-grande' }]);
  });
});

describe('referencia de la API', () => {
  const contrato = {
    info: { title: 'Prueba', version: '1' },
    paths: {
      '/api/v1/flujos/{id}': {
        post: {
          tags: ['Validasoft.Aplicacion.Flujos.FlujosEndpoints'],
          operationId: 'POST_api_v1_flujos_id',
          parameters: [{ name: 'id', in: 'path', required: true, schema: { type: 'string', format: 'uuid' } }],
          requestBody: { required: true, content: { 'application/json': { schema: { $ref: '#/components/schemas/Cuerpo' } } } },
          responses: { 200: { description: 'OK', content: { 'application/json': { schema: { $ref: '#/components/schemas/IResult' } } } }, 404: { description: 'Not Found' } },
        },
      },
      '/health/ready': { get: { tags: ['Validasoft.Api'], responses: { 200: { description: 'OK' } } } },
    },
    components: { schemas: { Cuerpo: { type: 'object', required: ['nombre'], properties: { nombre: { type: 'string' } } }, IResult: { type: 'object' } } },
  };
  const fuentes = {
    endpoints: new Map([['POST /api/v1/flujos/{id}', { resumen: 'Crea algo.', acceso: { tipo: 'politica', politica: 'Permisos.FlujosEscribir' }, alcanceEmpresa: true, errores: [{ estado: 409, codigo: 'duplicado' }], parametros: { id: 'Id del flujo.' }, clase: 'Validasoft.Aplicacion.Flujos.FlujosEndpoints' }]]),
    clases: new Map([['Validasoft.Aplicacion.Flujos.FlujosEndpoints', 'Flujos de la empresa.']]),
    permisos: new Map([['Permisos.FlujosEscribir', 'flujos:escribir']]),
  };

  it('agrupa por módulo y recurso, solo con las rutas de la API, y arma ejemplos', () => {
    const modelo = construirModeloApi({ bruto: contrato, transformado: contrato, fuentes });
    expect(modelo.total).toBe(1);
    expect(modelo.modulos.map((m) => [m.nombre, m.recursos.map((r) => r.nombre)])).toEqual([['Flujos', ['Flujos']]]);
    const e = modelo.modulos[0].recursos[0].endpoints[0];
    expect(e).toMatchObject({ metodo: 'POST', resumen: 'Crea algo.', alcanceEmpresa: true });
    expect(e.acceso.scope).toBe('flujos:escribir');
    expect(e.parametros[0]).toMatchObject({ nombre: 'id', requerido: true, detalle: 'Id del flujo.' });
    expect(e.respuestas.find((r) => r.codigo === '200').sinEsquema).toBe(true);
    expect(e.ejemplos.curl).toContain('-H "X-Api-Key: $API_KEY"');
    expect(e.ejemplos.curl).toContain('-H "X-Empresa: $EMPRESA_ID"');
    expect(e.ejemplos.curl).toContain('-H "Idempotency-Key: $(uuidgen)"');
    expect(e.ejemplos.curl).toContain('"nombre":"Constructora Norte S.A.S."');
  });

  it('genera HTML y Markdown con el permiso, el cuerpo, las respuestas y los errores', () => {
    const modelo = construirModeloApi({ bruto: contrato, transformado: contrato, fuentes });
    const html = htmlDeApi(modelo, 'hoy');
    expect(html.paginas.map((p) => p.ruta)).toEqual(['api/index.html', 'api/convenciones.html', 'api/flujos.html']);
    const pagina = html.paginas[2].html;
    expect(pagina).toContain('Permiso flujos:escribir');
    expect(pagina).toContain('class="metodo post"');
    expect(pagina).toContain('<span class="param">{id}</span>');
    expect(html.indice[0].u).toBe('api/flujos.html#post-api-v1-flujos-id');
    const md = markdownDeApi(modelo).find((a) => a.ruta === 'flujos.md').texto;
    expect(md).toContain('### `POST /api/v1/flujos/{id}`');
    expect(md).toContain('| `nombre` | texto | sí |');
    expect(md).toContain('- `409` `duplicado`');
  });

  it('nombreLegible pone tildes y siglas', () => {
    expect(nombreLegible('ContactosDeVinculo')).toBe('Contactos de vínculo');
    expect(nombreLegible('PlanIa')).toBe('Plan IA');
  });
});
