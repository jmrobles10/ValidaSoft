import { Typography } from '@mui/material';
import type { ApiKeyDto, EmpresaDto } from '@validasoft/api-client';
import { Dato, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { DialogoValorUnaVez } from '../../../shared/DialogoValorUnaVez';
import { textoAlcance } from './modelo';
import { ResumenPermisos } from './ResumenApiKey';

/**
 * La clave completa solo existe en la respuesta de `POST /api-keys` o de `POST /api-keys/{id}/rotar` (`valor`): se muestra
 * aquí **una sola vez**, se copia y se guarda en el sistema que la usa. Al cerrar, el padre la descarta. Tras una rotación,
 * `anteriorHasta` dice hasta cuándo vale la clave anterior.
 */
export function DialogoClaveCreada({
  llave,
  valor,
  empresas,
  anteriorHasta,
  onCerrar,
}: {
  llave: ApiKeyDto;
  valor: string;
  empresas: readonly EmpresaDto[];
  anteriorHasta?: string;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  const rotada = anteriorHasta !== undefined;
  return (
    <DialogoValorUnaVez
      titulo={t(rotada ? 'apiKeys.claveCreada.tituloRotada' : 'apiKeys.claveCreada.titulo', { nombre: llave.nombre })}
      aviso={t('apiKeys.claveCreada.unaVez')}
      valor={valor}
      textoCopiar={t('apiKeys.claveCreada.copiar')}
      textoCopiado={t('apiKeys.claveCreada.copiada')}
      textoListo={t('apiKeys.claveCreada.listo')}
      onCerrar={onCerrar}
    >
      <RejillaDatos columnas={2}>
        <Dato etiqueta={t('apiKeys.columnas.nombre')}>{llave.nombre}</Dato>
        <Dato etiqueta={t('apiKeys.columnas.alcance')}>{textoAlcance(llave, empresas, t('comun.todoElTenant'))}</Dato>
        <Dato etiqueta={t('apiKeys.columnas.permisos')} ancho>
          <ResumenPermisos scopes={llave.scopes} />
        </Dato>
      </RejillaDatos>
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('apiKeys.claveCreada.uso')}
      </Typography>
      {rotada && (
        <Typography variant="body2">
          {new Date(anteriorHasta) > new Date()
            ? t('apiKeys.claveCreada.anteriorHasta', { fecha: formatearFechaHora(anteriorHasta) })
            : t('apiKeys.claveCreada.anteriorYaNo')}
        </Typography>
      )}
    </DialogoValorUnaVez>
  );
}
