/** Textos es-CO de la ficha del tercero (Tercero 360, docs/06 §`/terceros/:id`); se integran en `es-CO.ts` como `terceros`. */

export const terceros = {
  ficha: {
    titulo: 'Ficha del tercero',
    verFicha: 'Ver ficha del tercero',
    abrirFicha: 'Abrir la ficha de {{tercero}}',
    tiposPersona: { Natural: 'Persona natural', Juridica: 'Persona jurídica', Consorcio: 'Consorcio o unión temporal' },
    extranjero: 'Extranjero',
    identificacion: {
      titulo: 'Identificación y contactos',
      nombre: 'Nombre o razón social',
      nombreComercial: 'Nombre comercial',
      identificacion: 'Identificación',
      pais: 'País',
      tipoPersona: 'Tipo de persona',
      registrado: 'Registrado en Validasoft',
      contactos: 'Contactos',
      sinContactos: 'El tercero no ha registrado contactos.',
    },
    vinculos: {
      titulo: 'Vínculos con {{empresa}}',
      sinPermiso: 'No tienes permiso para ver los vínculos de esta empresa. Pide acceso a un administrador.',
      verVinculo: 'Ver vínculo',
      verVinculoAccesible: 'Ver el vínculo {{tipo}}',
    },
    certificado: {
      titulo: 'Certificado',
      sinPermiso: 'No tienes permiso para ver los certificados.',
      sinEmitir: 'Aún no se ha emitido.',
      vigenteHasta: 'Vigente hasta el {{fecha}}',
      sinVencimiento: 'Sin vencimiento',
    },
    cuenta: {
      titulo: 'Cuenta designada',
      sinCuenta: 'El tercero no tiene una cuenta designada.',
      propuestaPendiente: 'Hay un cambio de cuenta por aprobar: revísalo en el vínculo.',
    },
    documentos: {
      titulo: 'Documentos compartidos',
      sinPermiso: 'No tienes permiso para ver los documentos.',
      vacio: 'El tercero no ha compartido documentos con este vínculo.',
      version: 'Versión {{n}}',
      sinVersion: 'Sin versión compartida',
    },
    otrasEmpresas: {
      titulo: 'Otras empresas del grupo',
      ayuda: 'El tercero también tiene vínculo con estas empresas. Sus decisiones no se muestran aquí.',
      sinAcceso: 'Otra empresa del grupo',
    },
    noEncontrado: 'No existe ese tercero en esta empresa',
    noEncontradoAyuda: 'Puede que el enlace esté incompleto o que el tercero solo tenga vínculo con otra empresa del grupo. Revisa la empresa activa.',
    irAVinculos: 'Ir a Vínculos',
    errorCarga: 'No pudimos cargar la ficha del tercero.',
  },
} as const;
