import { Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { camposDe, valoresIniciales } from '../formulario/formulario';
import { FormularioRequisito } from '../formulario/FormularioRequisito';
import { useRequisitoPublico } from '../shared/api';
import { CabeceraPagina, EsqueletoTarjetas } from '../shared/componentes';
import { modoDe } from '../tipoDocumento/reglas';
import { destinoFijo, rutaSegura } from './parametrosSubida';
import { SubirArchivo } from './SubirArchivo';

/**
 * Subir un documento o una versión nueva. `?documento=`: otra versión del mismo documento de la bóveda. Desde un
 * requisito de la revisión: `?tipo=&nombre=` (canónico global) o `?tipoDocumentoTenantId=&vinculoId=&nombre=` (tipo
 * propio de la empresa), con `expediente=&requisito=` para leer las reglas, los campos y la plantilla del tipo y
 * `volver=` para regresar. Un tipo `Formulario` se llena aquí mismo, sin archivo. Sin nada, el tercero elige el tipo
 * entre los canónicos globales.
 */
export function PaginaSubir() {
  const { t } = useTranslation();
  const [parametros] = useSearchParams();
  const contexto = useRequisitoPublico(parametros.get('expediente'), parametros.get('requisito'));
  const fijo = destinoFijo(parametros);

  if (contexto.isLoading) return <EsqueletoTarjetas cantidad={1} />;
  // Si la revisión no carga o el requisito ya no está, se sube como antes (con las reglas globales).
  const delRequisito = fijo ? (contexto.data ?? null) : null;
  const tipo = delRequisito?.requisito.tipoDocumento;

  if (fijo && delRequisito && tipo && modoDe(tipo) === 'Formulario') {
    return (
      <>
        <CabeceraPagina titulo={t('formulario.titulo', { tipo: fijo.nombre })} sub={t('formulario.sub', { empresa: delRequisito.expediente.empresa.nombre })} />
        {tipo.textoAyuda && <Typography sx={{ fontSize: 15, lineHeight: '22px', mb: 2 }}>{tipo.textoAyuda}</Typography>}
        <FormularioRequisito
          tipo={fijo.nombre}
          campos={camposDe(tipo)}
          destino={fijo.destino}
          volver={rutaSegura(parametros.get('volver'))}
          iniciales={valoresIniciales(camposDe(tipo), delRequisito.requisito.versionActual?.datosDeclarados)}
        />
      </>
    );
  }

  return <SubirArchivo parametros={parametros} delRequisito={delRequisito} />;
}
