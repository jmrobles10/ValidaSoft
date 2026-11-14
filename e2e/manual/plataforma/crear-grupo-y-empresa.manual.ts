import { randomUUID } from 'node:crypto';
import { clavesDeSistema, nitAleatorio } from '../../soporte/api';
import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { ORIGEN_SISTEMA } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Crea un grupo con su primera empresa',
    resumen:
      'El operador de Plataforma da de alta un cliente completo desde la consola: el grupo, su primera empresa, la persona que lo administra y el sistema desde el que su gente entra a Validasoft. Al terminar, la pantalla muestra las API keys una sola vez.',
    precondiciones: ['Un operador de Plataforma con la consola abierta.', 'La clave pública PEM del sistema integrado (nunca la privada).'],
    relacionados: ['plataforma/lista-de-grupos', 'plataforma/agregar-empresa-a-un-grupo', 'plataforma/sistemas-de-un-grupo'],
  },
  async ({ page }) => {
    const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);
    const nit = nitAleatorio();
    const grupo = `Grupo Cumbre ${sufijo}`;
    const empresa = `Edificaciones Cumbre ${sufijo} S.A.S.`;
    const prefijo = `CU${sufijo.slice(0, 4).toUpperCase()}`;
    const admin = { nombre: 'Valentina Administradora', correo: `valentina.${sufijo}@cumbre.e2e`, sub: `valentina${sufijo}` };
    const claves = clavesDeSistema();

    const consola = await abrirConsolaDePlataforma(page);
    await expect(consola.getByRole('heading', { level: 1, name: 'Grupos y empresas' })).toBeVisible();

    await paso(page, {
      titulo: 'Abre el formulario de un grupo nuevo',
      explicacion:
        'Desde «Grupos y empresas», el botón «Nuevo grupo» abre el formulario de alta. Un grupo es el cliente de Validasoft: nace con una primera empresa, con la persona que lo administra y, si lo pides, con el sistema desde el que esa gente entra. ' +
        'El alta se envía completa en una sola acción.',
      resaltar: consola.getByRole('link', { name: 'Nuevo grupo' }),
      configuracion: {
        ajuste: 'Alta de grupos',
        ruta: 'Plataforma › Grupos y empresas › Nuevo grupo (/plataforma/grupos/nuevo)',
        efecto: 'Crea un cliente nuevo con su primera empresa, su administrador y, si aplica, su sistema integrado.',
      },
    });
    await consola.getByRole('link', { name: 'Nuevo grupo' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Nuevo grupo' })).toBeVisible();

    await consola.getByRole('textbox', { name: 'Nombre del grupo' }).fill(grupo);
    await paso(page, {
      titulo: 'Nombra el grupo',
      explicacion:
        'Escribe el nombre del grupo. El identificador se propone solo a partir del nombre y admite minúsculas, números y guiones, de 3 a 30 caracteres. ' +
        'Ese identificador forma parte de las API keys del grupo y no cambia después, así que revísalo antes de crear. La zona horaria viene por omisión (America/Bogota).',
      resaltar: [consola.getByRole('textbox', { name: 'Nombre del grupo' }), consola.getByRole('textbox', { name: 'Identificador' })],
      variantes: [
        {
          cuando: 'El identificador no cumple (muy corto, con mayúsculas o con guion al inicio o al final)',
          entonces: 'El campo se marca en rojo con la razón y el botón «Crear grupo» queda deshabilitado hasta corregirlo.',
        },
      ],
    });

    await consola.getByRole('textbox', { name: 'Número de identificación' }).fill(nit.numero);
    await consola.getByRole('textbox', { name: 'Razón social' }).fill(empresa);
    await consola.getByRole('textbox', { name: 'Prefijo de folio' }).fill(prefijo);
    await paso(page, {
      titulo: 'Da de alta la primera empresa del grupo',
      explicacion:
        'La primera empresa es la razón social que valida a sus terceros. Eliges el país y el tipo de identificación, escribes el número y la razón social, y defines el prefijo de folio: las letras con que empiezan los números de sus expedientes. ' +
        'El prefijo no cambia después, así que elige uno corto y fácil de reconocer. Más empresas se agregan después desde el mismo grupo.',
      resaltar: [
        consola.getByRole('textbox', { name: 'Número de identificación' }),
        consola.getByRole('textbox', { name: 'Razón social' }),
        consola.getByRole('textbox', { name: 'Prefijo de folio' }),
      ],
      variantes: [
        { cuando: 'El número no tiene la forma del tipo elegido', entonces: 'El campo lo indica debajo y no deja crear el grupo.' },
      ],
      configuracion: {
        ajuste: 'Prefijo de folio',
        efecto: 'Aparece al inicio de los números de expediente de la empresa y no cambia después.',
      },
    });

    await consola.getByRole('textbox', { name: 'Nombre', exact: true }).fill(admin.nombre);
    await consola.getByRole('textbox', { name: 'Correo' }).fill(admin.correo);
    await consola.getByRole('textbox', { name: 'Su usuario en el sistema (opcional)' }).fill(admin.sub);
    await paso(page, {
      titulo: 'Nombra al administrador del grupo',
      explicacion:
        'El administrador tiene el rol Administrador en todo el grupo: crea usuarios, áreas y flujos. Validasoft no guarda su contraseña; esta persona entra desde el sistema integrado o, si eliges el ingreso directo, con un correo. ' +
        'Si el sistema identifica a sus personas con un usuario propio, escríbelo; si lo dejas vacío, la persona se enlaza por su correo la primera vez que entre.',
      resaltar: [
        consola.getByRole('textbox', { name: 'Nombre', exact: true }),
        consola.getByRole('textbox', { name: 'Correo' }),
        consola.getByRole('textbox', { name: 'Su usuario en el sistema (opcional)' }),
      ],
      variantes: [{ cuando: 'Dejas vacío el usuario del sistema', entonces: 'El enlace se hace por correo en el primer ingreso.' }],
    });

    await paso(page, {
      titulo: 'Elige cómo entra la gente del grupo',
      explicacion:
        'Marca al menos una forma de ingreso. «Desde el sistema integrado» hace que las personas entren desde el ERP o portal del cliente, que firma sus aserciones; «Directo a Validasoft» permite entrar con un correo. ' +
        'Si no marcas ninguna, el formulario no se puede enviar. Con la primera opción aparece la sección del sistema integrado.',
      resaltar: consola.getByRole('checkbox', { name: 'Desde el sistema integrado' }),
      variantes: [
        { cuando: 'Solo «Directo a Validasoft»', entonces: 'La sección del sistema integrado desaparece y el grupo no recibe API key de sistema.' },
        { cuando: 'Las dos', entonces: 'Aparece la sección del sistema y el grupo recibe las dos llaves.' },
      ],
    });

    await consola.getByRole('textbox', { name: 'Código' }).fill('ERP');
    await consola.getByRole('textbox', { name: 'Nombre del sistema' }).fill('ERP de Cumbre');
    await consola.getByRole('textbox', { name: 'Clave pública (PEM)' }).fill(claves.publica);
    await consola.getByRole('textbox', { name: 'Sitios que pueden incrustar Validasoft (opcional)' }).fill(ORIGEN_SISTEMA);
    await paso(page, {
      titulo: 'Registra el sistema integrado',
      explicacion:
        'El sistema del cliente se registra con un código (es el «iss» de sus aserciones), un nombre y su clave pública en formato PEM: RSA de 2048 bits o más, o EC P-256. Solo se pega la clave pública; la privada se queda en el sistema del cliente. ' +
        'Los sitios que pueden incrustar Validasoft se escriben uno por línea, como https://erp.cliente.co. Sin ellos, el sistema solo usa la API.',
      resaltar: [
        consola.getByRole('textbox', { name: 'Código' }),
        consola.getByRole('textbox', { name: 'Clave pública (PEM)' }),
        consola.getByRole('textbox', { name: 'Sitios que pueden incrustar Validasoft (opcional)' }),
      ],
      variantes: [
        { cuando: 'Falta la clave pública', entonces: 'El botón «Crear grupo» queda deshabilitado y el campo pide pegar la clave en PEM.' },
      ],
    });

    await consola.getByRole('button', { name: 'Crear grupo' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: `${grupo} creado` })).toBeVisible();
    await expect(consola.getByLabel('API key del sistema ERP', { exact: true })).toBeVisible();

    await paso(page, {
      titulo: 'Copia las llaves: solo se muestran una vez',
      explicacion:
        'Al crear el grupo aparecen dos API keys: la del sistema integrado, que usa su backend con la aserción de cada persona, y la de administración, para tareas de servidor a servidor. ' +
        'Cópialas y entrégalas por un canal seguro: no se vuelven a mostrar. Si se pierden, el administrador del grupo puede emitir otras desde Configuración › API keys.',
      resaltar: [
        consola.getByLabel('API key del sistema ERP', { exact: true }),
        consola.getByLabel('API key de administración', { exact: true }),
      ],
      configuracion: {
        ajuste: 'API keys del grupo',
        ruta: 'Configuración › API keys',
        efecto: 'Permite emitir o revocar llaves después del alta.',
      },
    });

    await expect(consola.getByRole('link', { name: `Configurar el plan de IA de ${empresa}` })).toBeVisible();
    await paso(page, {
      titulo: 'Pasa al plan de IA de la empresa nueva',
      explicacion:
        'Desde la misma pantalla puedes pasar al plan de IA de la primera empresa. Sin plan, sus documentos se revisan manualmente; con plan, se validan con IA dentro de las validaciones incluidas cada mes. ' +
        'El botón «Volver a los grupos» regresa a la lista, donde la empresa ya aparece dentro del grupo.',
      resaltar: consola.getByRole('link', { name: `Configurar el plan de IA de ${empresa}` }),
    });
  },
);
