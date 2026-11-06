import { Box, MenuItem, Stack, TextField } from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { useBoveda } from '../shared/api';
import { CabeceraPagina, EsqueletoTarjetas } from '../shared/componentes';
import { tipoSubible, TIPOS_SUBIBLES } from './catalogo';
import { Dropzone } from './Dropzone';
import { EstadoDeSubida } from './EstadoDeSubida';
import type { DestinoCarga } from './subida';
import { useSubida } from './useSubida';

/**
 * Subir un documento o una versión nueva (`?documento=`). Sin documento, el tercero elige el tipo entre los canónicos
 * globales; con documento, la versión va al mismo (canónico global o tipo de la empresa en su vínculo).
 */
export function PaginaSubir() {
  const { t } = useTranslation();
  const [parametros] = useSearchParams();
  const documentoId = parametros.get('documento');
  const boveda = useBoveda();
  const [tipoElegido, setTipoElegido] = useState(parametros.get('tipo') ?? '');
  const { estado, subir, cancelar, reiniciar } = useSubida();

  const existente = documentoId ? boveda.data?.documentos.find((d) => d.id === documentoId) : undefined;
  if (documentoId && boveda.isPending) return <EsqueletoTarjetas cantidad={1} />;

  const destino: DestinoCarga | null = existente
    ? existente.ambito === 'Global' || !existente.tipo.tipoDocumentoTenantId || !existente.vinculoId
      ? { tipoCanonico: existente.tipo.codigo }
      : { tipoDocumentoTenantId: existente.tipo.tipoDocumentoTenantId, vinculoId: existente.vinculoId }
    : tipoElegido
      ? { tipoCanonico: tipoElegido }
      : null;
  const nombreTipo = existente?.tipo.nombre ?? tipoSubible(tipoElegido)?.nombre ?? null;
  const ocupado = estado.fase === 'en-curso';
  const mostrarZona = estado.fase === 'inicio' || estado.fase === 'problema';

  return (
    <>
      <CabeceraPagina titulo={existente ? t('subir.tituloNueva', { tipo: existente.tipo.nombre }) : t('subir.titulo')} />
      <Stack spacing={2}>
        {!existente && (
          <TextField
            select
            label={t('subir.tipo')}
            value={tipoElegido}
            onChange={(e) => setTipoElegido(e.target.value)}
            helperText={tipoSubible(tipoElegido)?.ayuda ?? t('subir.tipoAyuda')}
            disabled={ocupado}
            fullWidth
          >
            {TIPOS_SUBIBLES.map((tipo) => (
              <MenuItem key={tipo.codigo} value={tipo.codigo}>
                {tipo.nombre}
              </MenuItem>
            ))}
          </TextField>
        )}

        {estado.fase !== 'inicio' && (
          <EstadoDeSubida
            estado={estado}
            tipo={nombreTipo ?? ''}
            onCancelar={cancelar}
            onReintentar={estado.fase === 'fallo' && destino ? () => subir(estado.archivo, destino) : undefined}
            onOtroArchivo={reiniciar}
          />
        )}

        {mostrarZona && (
          <Box>
            <Dropzone
              titulo={nombreTipo ? t('subir.zona', { tipo: nombreTipo }) : t('subir.zonaSinTipo')}
              deshabilitado={!destino}
              onArchivo={(archivo) => destino && subir(archivo, destino)}
            />
          </Box>
        )}
      </Stack>
    </>
  );
}
