import { Alert, Box, Button, FormControlLabel, Stack, Switch, TextField, Typography } from '@mui/material';
import { CampoNumero } from '@validasoft/ui';
import { SCOPES, type ArchivoDeLoteDto, type VistaPreviaLoteDto } from '@validasoft/api-client';
import { Download, FileUp, Send } from 'lucide-react';
import { useState, type ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, IconoCarga, SinPermiso, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { guardarArchivo } from '../vencimientos/api';
import { useCrearLote, useDescargarLote, usePrevisualizarLote } from './api';
import { aBase64 } from './modelo';
import { TablaVistaPrevia } from './TablaVistaPrevia';

const VIGENCIA_DIAS = 7;

/**
 * Nuevo lote (H8): plantilla, carga del archivo y vista previa con la validación por fila; solo se crea al confirmar. Las
 * filas con errores o repetidas quedan en el informe del lote sin procesarse.
 */
export function PaginaNuevoLote() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const mostrar = useAvisos((s) => s.mostrar);
  const descargar = useDescargarLote(empresa.id);
  const previsualizar = usePrevisualizarLote(empresa.id);
  const crear = useCrearLote(empresa.id);
  const [archivo, setArchivo] = useState<ArchivoDeLoteDto | null>(null);
  const [vista, setVista] = useState<VistaPreviaLoteDto | null>(null);
  const [nombre, setNombre] = useState('');
  const [dias, setDias] = useState(String(VIGENCIA_DIAS));
  const [soloErrores, setSoloErrores] = useState(false);

  if (!permisos.puede(SCOPES.vinculosEscribir)) return <SinPermiso seccion={t('lotes.titulo')} />;

  const plantilla = (formato: 'xlsx' | 'csv') =>
    descargar.mutate(
      { tipo: 'plantilla', formato },
      {
        onSuccess: (a) => guardarArchivo(a, `plantilla-invitacion-masiva.${formato}`),
        onError: (error) => mostrar(`${t('lotes.nuevoLote.errorPlantilla')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );

  const elegir = async (evento: ChangeEvent<HTMLInputElement>) => {
    const elegido = evento.target.files?.[0];
    evento.target.value = '';
    if (!elegido) return;
    const dto = { nombre: elegido.name, contenidoBase64: await aBase64(elegido) };
    setArchivo(dto);
    setVista(null);
    previsualizar.mutate(
      { archivo: dto, vigenciaHoras: VIGENCIA_DIAS * 24 },
      {
        onSuccess: (v) => {
          setVista(v);
          setSoloErrores(v.invalidas + v.duplicadas > 0);
        },
        onError: (error) => mostrar(`${t('lotes.nuevoLote.errorArchivo')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );
  };

  const vigencia = Math.round(Number(dias));
  const vigenciaValida = Number.isFinite(vigencia) && vigencia >= 1 && vigencia <= 30;
  const confirmar = () => {
    if (!archivo) return;
    crear.mutate(
      { nombre: nombre.trim() || null, archivo, vigenciaHoras: vigencia * 24 },
      {
        onSuccess: (lote) => {
          mostrar(t('lotes.nuevoLote.creado'), 'success');
          void navegar(`/e/${empresa.id}/invitaciones/lotes/${lote.id}`);
        },
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      },
    );
  };

  return (
    <>
      <EncabezadoPagina
        antes={<EnlaceVolver a={`/e/${empresa.id}/invitaciones/lotes`}>{t('lotes.titulo')}</EnlaceVolver>}
        titulo={t('lotes.nuevoLote.titulo')}
        subtitulo={t('lotes.nuevoLote.subtitulo')}
      />
      <Stack spacing={2}>
        <Tarjeta titulo={t('lotes.nuevoLote.pasoPlantilla')}>
          <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
            {t('lotes.nuevoLote.pasoPlantillaAyuda')}
          </Typography>
          <Stack direction="row" spacing={1}>
            <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={descargar.isPending} onClick={() => plantilla('xlsx')}>
              {t('lotes.nuevoLote.plantillaExcel')}
            </Button>
            <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={descargar.isPending} onClick={() => plantilla('csv')}>
              {t('lotes.nuevoLote.plantillaCsv')}
            </Button>
          </Stack>
        </Tarjeta>

        <Tarjeta titulo={t('lotes.nuevoLote.pasoArchivo')}>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Button
              variant="contained"
              component="label"
              startIcon={previsualizar.isPending ? <IconoCarga /> : <FileUp size={16} aria-hidden />}
              disabled={previsualizar.isPending}
            >
              {previsualizar.isPending ? t('lotes.nuevoLote.validando') : t('lotes.nuevoLote.elegirArchivo')}
              <input hidden type="file" accept=".xlsx,.csv,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={(e) => void elegir(e)} />
            </Button>
            {archivo && <Typography variant="body2">{t('lotes.nuevoLote.archivoElegido', { nombre: archivo.nombre })}</Typography>}
          </Stack>
        </Tarjeta>

        {vista && (
          <Tarjeta titulo={t('lotes.nuevoLote.pasoRevision')}>
            <Alert severity={vista.validas === 0 ? 'error' : vista.invalidas + vista.duplicadas > 0 ? 'warning' : 'success'} sx={{ mb: 2 }}>
              {vista.validas === 0
                ? t('lotes.nuevoLote.sinValidas')
                : t('lotes.nuevoLote.totales', {
                    total: String(vista.total),
                    validas: String(vista.validas),
                    invalidas: String(vista.invalidas),
                    duplicadas: String(vista.duplicadas),
                  })}
            </Alert>
            <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', mb: 2 }}>
              <TextField
                size="small"
                label={t('lotes.nuevoLote.nombre')}
                helperText={t('lotes.nuevoLote.nombreAyuda')}
                value={nombre}
                onChange={(e) => setNombre(e.target.value)}
                slotProps={{ htmlInput: { maxLength: 150 } }}
                sx={{ width: 360, maxWidth: '100%' }}
              />
              <CampoNumero etiqueta={t('lotes.nuevoLote.vigencia')} valor={dias} error={!vigenciaValida} onCambiar={setDias} sx={{ width: 240 }} />
            </Box>
            <FormControlLabel
              control={<Switch checked={soloErrores} onChange={(e) => setSoloErrores(e.target.checked)} />}
              label={t('lotes.nuevoLote.soloErrores')}
              sx={{ mb: 1 }}
            />
            <TablaVistaPrevia filas={vista.filas} soloErrores={soloErrores} />
            <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 2 }}>
              <Button
                variant="contained"
                startIcon={crear.isPending ? <IconoCarga /> : <Send size={16} aria-hidden />}
                disabled={vista.validas === 0 || !vigenciaValida || crear.isPending}
                onClick={confirmar}
              >
                {t('lotes.nuevoLote.crear', { count: vista.validas })}
              </Button>
            </Box>
          </Tarjeta>
        )}
      </Stack>
    </>
  );
}
