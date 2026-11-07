import { Box, MenuItem, Stack, TextField } from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { useBoveda } from '../shared/api';
import { CabeceraPagina, EsqueletoTarjetas } from '../shared/componentes';
import { tipoSubible, TIPOS_SUBIBLES } from './catalogo';
import { Dropzone } from './Dropzone';
import { EstadoDeSubida } from './EstadoDeSubida';
import { destinoFijo, rutaSegura } from './parametrosSubida';
import type { DestinoCarga } from './subida';
import { useSubida } from './useSubida';

/**
 * Subir un documento o una versión nueva. `?documento=`: otra versión del mismo documento de la bóveda. Desde un
 * requisito de la revisión: `?tipo=&nombre=` (canónico global) o `?tipoDocumentoTenantId=&vinculoId=&nombre=` (tipo
 * propio de la empresa), con `volver=` para regresar. Sin nada, el tercero elige el tipo entre los canónicos globales.
 */
export function PaginaSubir() {
  const { t } = useTranslation();
  const [parametros] = useSearchParams();
  const documentoId = parametros.get('documento');
  const fijo = destinoFijo(parametros);
  const volver = rutaSegura(parametros.get('volver'));
  const boveda = useBoveda();
  const [tipoElegido, setTipoElegido] = useState(parametros.get('tipo') ?? '');
  const { estado, subir, cancelar, reiniciar } = useSubida();

  const existente = documentoId ? boveda.data?.documentos.find((d) => d.id === documentoId) : undefined;
  if (documentoId && boveda.isPending) return <EsqueletoTarjetas cantidad={1} />;

  const destino: DestinoCarga | null = fijo
    ? fijo.destino
    : existente
      ? existente.ambito === 'Global' || !existente.tipo.tipoDocumentoTenantId || !existente.vinculoId
        ? { tipoCanonico: existente.tipo.codigo }
        : { tipoDocumentoTenantId: existente.tipo.tipoDocumentoTenantId, vinculoId: existente.vinculoId }
      : tipoElegido
        ? { tipoCanonico: tipoElegido }
        : null;
  const nombreTipo = fijo?.nombre ?? existente?.tipo.nombre ?? tipoSubible(tipoElegido)?.nombre ?? null;
  const tituloFijo = fijo ? fijo.nombre : existente?.tipo.nombre;
  const ocupado = estado.fase === 'en-curso';
  const mostrarZona = estado.fase === 'inicio' || estado.fase === 'problema';

  return (
    <>
      <CabeceraPagina
        titulo={fijo ? t('subir.tituloDocumento', { tipo: fijo.nombre }) : tituloFijo ? t('subir.tituloNueva', { tipo: tituloFijo }) : t('subir.titulo')}
      />
      <Stack spacing={2}>
        {!existente && !fijo && (
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
            volver={volver}
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
