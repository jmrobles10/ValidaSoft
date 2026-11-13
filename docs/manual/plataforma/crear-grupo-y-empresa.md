---
id: "plataforma/crear-grupo-y-empresa"
titulo: "Crea un grupo con su primera empresa"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma da de alta un cliente completo desde la consola: el grupo, su primera empresa, la persona que lo administra y el sistema desde el que su gente entra a Validasoft. Al terminar, la pantalla muestra las API keys una sola vez."
pasos: 8
---

# Crea un grupo con su primera empresa

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma da de alta un cliente completo desde la consola: el grupo, su primera empresa, la persona que lo administra y el sistema desde el que su gente entra a Validasoft. Al terminar, la pantalla muestra las API keys una sola vez.

**Antes de empezar**

- Un operador de Plataforma con la consola abierta.
- La clave pública PEM del sistema integrado (nunca la privada).

**Pasos**

1. Abre el formulario de un grupo nuevo
2. Nombra el grupo
3. Da de alta la primera empresa del grupo
4. Nombra al administrador del grupo
5. Elige cómo entra la gente del grupo
6. Registra el sistema integrado
7. Copia las llaves: solo se muestran una vez
8. Pasa al plan de IA de la empresa nueva

## Paso 1. Abre el formulario de un grupo nuevo

Desde «Grupos y empresas», el botón «Nuevo grupo» abre el formulario de alta. Un grupo es el cliente de Validasoft: nace con una primera empresa, con la persona que lo administra y, si lo pides, con el sistema desde el que esa gente entra. El alta se envía completa en una sola acción.

> **Pantalla:** `/plataforma/grupos`

![Paso 1: Abre el formulario de un grupo nuevo (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/01-abre-el-formulario-de-un-grupo-nuevo-claro.webp)

**Configuración que lo afecta**

- **Alta de grupos** (`Plataforma › Grupos y empresas › Nuevo grupo (/plataforma/grupos/nuevo)`): Crea un cliente nuevo con su primera empresa, su administrador y, si aplica, su sistema integrado.

## Paso 2. Nombra el grupo

Escribe el nombre del grupo. El identificador se propone solo a partir del nombre y admite minúsculas, números y guiones, de 3 a 30 caracteres. Ese identificador forma parte de las API keys del grupo y no cambia después, así que revísalo antes de crear. La zona horaria viene por omisión (America/Bogota).

> **Pantalla:** `/plataforma/grupos/nuevo`

![Paso 2: Nombra el grupo (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/02-nombra-el-grupo-claro.webp)

**Variantes**

- **El identificador no cumple (muy corto, con mayúsculas o con guion al inicio o al final):** El campo se marca en rojo con la razón y el botón «Crear grupo» queda deshabilitado hasta corregirlo.

## Paso 3. Da de alta la primera empresa del grupo

La primera empresa es la razón social que valida a sus terceros. Eliges el país y el tipo de identificación, escribes el número y la razón social, y defines el prefijo de folio: las letras con que empiezan los números de sus expedientes. El prefijo no cambia después, así que elige uno corto y fácil de reconocer. Más empresas se agregan después desde el mismo grupo.

> **Pantalla:** `/plataforma/grupos/nuevo`

![Paso 3: Da de alta la primera empresa del grupo (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/03-da-de-alta-la-primera-empresa-del-grupo-claro.webp)

**Variantes**

- **El número no tiene la forma del tipo elegido:** El campo lo indica debajo y no deja crear el grupo.

**Configuración que lo afecta**

- **Prefijo de folio**: Aparece al inicio de los números de expediente de la empresa y no cambia después.

## Paso 4. Nombra al administrador del grupo

El administrador tiene el rol Administrador en todo el grupo: crea usuarios, áreas y flujos. Validasoft no guarda su contraseña; esta persona entra desde el sistema integrado o, si eliges el ingreso directo, con un correo. Si el sistema identifica a sus personas con un usuario propio, escríbelo; si lo dejas vacío, la persona se enlaza por su correo la primera vez que entre.

> **Pantalla:** `/plataforma/grupos/nuevo`

![Paso 4: Nombra al administrador del grupo (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/04-nombra-al-administrador-del-grupo-claro.webp)

**Variantes**

- **Dejas vacío el usuario del sistema:** El enlace se hace por correo en el primer ingreso.

## Paso 5. Elige cómo entra la gente del grupo

Marca al menos una forma de ingreso. «Desde el sistema integrado» hace que las personas entren desde el ERP o portal del cliente, que firma sus aserciones; «Directo a Validasoft» permite entrar con un correo. Si no marcas ninguna, el formulario no se puede enviar. Con la primera opción aparece la sección del sistema integrado.

> **Pantalla:** `/plataforma/grupos/nuevo`

![Paso 5: Elige cómo entra la gente del grupo (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/05-elige-como-entra-la-gente-del-grupo-claro.webp)

**Variantes**

- **Solo «Directo a Validasoft»:** La sección del sistema integrado desaparece y el grupo no recibe API key de sistema.
- **Las dos:** Aparece la sección del sistema y el grupo recibe las dos llaves.

## Paso 6. Registra el sistema integrado

El sistema del cliente se registra con un código (es el «iss» de sus aserciones), un nombre y su clave pública en formato PEM: RSA de 2048 bits o más, o EC P-256. Solo se pega la clave pública; la privada se queda en el sistema del cliente. Los sitios que pueden incrustar Validasoft se escriben uno por línea, como https://erp.cliente.co. Sin ellos, el sistema solo usa la API.

> **Pantalla:** `/plataforma/grupos/nuevo`

![Paso 6: Registra el sistema integrado (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/06-registra-el-sistema-integrado-claro.webp)

**Variantes**

- **Falta la clave pública:** El botón «Crear grupo» queda deshabilitado y el campo pide pegar la clave en PEM.

## Paso 7. Copia las llaves: solo se muestran una vez

Al crear el grupo aparecen dos API keys: la del sistema integrado, que usa su backend con la aserción de cada persona, y la de administración, para tareas de servidor a servidor. Cópialas y entrégalas por un canal seguro: no se vuelven a mostrar. Si se pierden, el administrador del grupo puede emitir otras desde Configuración › API keys.

> **Pantalla:** `/plataforma/grupos/nuevo`

![Paso 7: Copia las llaves: solo se muestran una vez (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/07-copia-las-llaves-solo-se-muestran-una-vez-claro.webp)

**Configuración que lo afecta**

- **API keys del grupo** (`Configuración › API keys`): Permite emitir o revocar llaves después del alta.

## Paso 8. Pasa al plan de IA de la empresa nueva

Desde la misma pantalla puedes pasar al plan de IA de la primera empresa. Sin plan, sus documentos se revisan manualmente; con plan, se validan con IA dentro de las validaciones incluidas cada mes. El botón «Volver a los grupos» regresa a la lista, donde la empresa ya aparece dentro del grupo.

> **Pantalla:** `/plataforma/grupos/nuevo`

![Paso 8: Pasa al plan de IA de la empresa nueva (escritorio)](../../../salida/manual/img/plataforma/crear-grupo-y-empresa/08-pasa-al-plan-de-ia-de-la-empresa-nueva-claro.webp)

## Recorridos relacionados

- [Busca un grupo y revisa sus empresas](lista-de-grupos.md)
- [Agrega una empresa a un grupo existente](agregar-empresa-a-un-grupo.md)
- [Desactiva y reactiva un sistema integrado de un grupo](sistemas-de-un-grupo.md)
