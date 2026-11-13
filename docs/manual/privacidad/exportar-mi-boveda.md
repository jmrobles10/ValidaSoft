---
id: "privacidad/exportar-mi-boveda"
titulo: "Descarga una copia firmada de tu bóveda"
actor: "Tercero"
dominio: "privacidad"
resumen: "El tercero pide una exportación de su bóveda: Validasoft la prepara en segundo plano, avisa por correo cuando está lista y el tercero la descarga como un ZIP con sus archivos, sus datos y un manifiesto firmado para comprobar que nada cambió."
pasos: 4
---

# Descarga una copia firmada de tu bóveda

**Actor:** Tercero · **Dominio:** Datos personales

El tercero pide una exportación de su bóveda: Validasoft la prepara en segundo plano, avisa por correo cuando está lista y el tercero la descarga como un ZIP con sus archivos, sus datos y un manifiesto firmado para comprobar que nada cambió.

**Antes de empezar**

- Un grupo con Constructora Norte, que invitó a un tercero que aceptó la invitación y subió el RUT y el certificado de existencia y representación legal.
- El tercero entra al portal con su sesión abierta.

**Pasos**

1. Abre la exportación de tu bóveda
2. Pide la exportación y espera su preparación
3. Descarga el ZIP
4. Entiende cómo verificar la copia

## Paso 1. Abre la exportación de tu bóveda

En la parte de arriba de «Privacidad y mis datos» está «Exportar mi bóveda». La copia incluye todos tus archivos, tus datos y un manifiesto firmado por Validasoft, con el que se puede comprobar que el contenido no cambió después de la exportación. No tienes que esperar en esta pantalla: se prepara sola y te avisamos por correo cuando esté lista.

> **Pantalla:** `/privacidad`

![Paso 1: Abre la exportación de tu bóveda (escritorio)](../../../salida/manual/img/privacidad/exportar-mi-boveda/01-abre-la-exportacion-de-tu-boveda-claro.webp)

![Paso 1: Abre la exportación de tu bóveda (celular)](../../../salida/manual/img/privacidad/exportar-mi-boveda/01-abre-la-exportacion-de-tu-boveda-celular.webp)

## Paso 2. Pide la exportación y espera su preparación

Al pedirla aparece el aviso de que la estamos preparando y queda en «Mis exportaciones» con su fecha. Cuando termina, aparecen el número de archivos, el tamaño y la fecha hasta la que puedes descargarla; después de esa fecha la eliminamos y tendrás que pedir otra.

> **Pantalla:** `/privacidad`

![Paso 2: Pide la exportación y espera su preparación (escritorio)](../../../salida/manual/img/privacidad/exportar-mi-boveda/02-pide-la-exportacion-y-espera-su-preparacion-claro.webp)

![Paso 2: Pide la exportación y espera su preparación (celular)](../../../salida/manual/img/privacidad/exportar-mi-boveda/02-pide-la-exportacion-y-espera-su-preparacion-celular.webp)

**Variantes**

- **La exportación falla:** El detalle dice que no pudimos prepararla; pide otra, y si se repite, escríbenos.
- **Pasó el plazo de descarga:** El detalle dice que venció y se eliminó: pide una nueva si la necesitas.
- **Ya pediste varias exportaciones hoy:** Validasoft no prepara otra ese día y te pide intentarlo al día siguiente.

## Paso 3. Descarga el ZIP

Con «Descargar» baja un archivo ZIP. Dentro encuentras tus archivos, manifiesto.json con la huella SHA-256 de cada uno, manifiesto.firma.json con la firma de Validasoft y LEEME.txt con los pasos para verificarla. Descárgalo antes de la fecha «disponible hasta», porque después Validasoft lo elimina.

> **Pantalla:** `/privacidad`

![Paso 3: Descarga el ZIP (escritorio)](../../../salida/manual/img/privacidad/exportar-mi-boveda/03-descarga-el-zip-claro.webp)

![Paso 3: Descarga el ZIP (celular)](../../../salida/manual/img/privacidad/exportar-mi-boveda/03-descarga-el-zip-celular.webp)

## Paso 4. Entiende cómo verificar la copia

Debajo de la exportación, «Cómo verificarla» explica el proceso: manifiesto.json lista la huella SHA-256 de cada archivo y manifiesto.firma.json trae la firma de Validasoft. La firma se comprueba con la clave pública que Validasoft publica en /.well-known/validasoft-certificados.json; si todo coincide, el contenido no cambió desde la exportación.

> **Pantalla:** `/privacidad`

![Paso 4: Entiende cómo verificar la copia (escritorio)](../../../salida/manual/img/privacidad/exportar-mi-boveda/04-entiende-como-verificar-la-copia-claro.webp)

![Paso 4: Entiende cómo verificar la copia (celular)](../../../salida/manual/img/privacidad/exportar-mi-boveda/04-entiende-como-verificar-la-copia-celular.webp)

## Recorridos relacionados

- [Pide conocer, corregir o suprimir tus datos personales](solicitud-sobre-mis-datos.md)
