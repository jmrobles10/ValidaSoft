import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material';
import { SCOPES, type VistaPreviaCampaniaDto } from '@validasoft/api-client';
import { EsqueletoTarjeta } from '@validasoft/ui';
import { Megaphone, Search } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, IconoCarga, SinPermiso, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useTiposVinculo } from '../vinculos/api';
import { useCrearCampania, usePrevisualizarCampania } from './api';
import { DialogoAvisoCampania } from './DialogoAvisoCampania';
import { FiltroCampania } from './FiltroCampania';
import { BORRADOR_INICIAL, filtroDeBorrador, filtroValido, hayQueHacer, type BorradorDeFiltro } from './modelo';
import { ResumenVistaPrevia } from './ResumenVistaPrevia';
import { TablaVistaPreviaCampania } from './TablaVistaPreviaCampania';

/**
 * Nueva campaña de renovación (OP-1…OP-3): un solo flujo. Se elige el filtro, la vista previa dice a quiénes afecta (y por qué se
 * omiten algunos) y «Abrir renovaciones» muestra un aviso con lo que pasará; solo al confirmarlo se crea la campaña.
 */
export function PaginaNuevaCampania() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const mostrar = useAvisos((s) => s.mostrar);
  const tipos = useTiposVinculo(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const previsualizar = usePrevisualizarCampania(empresa.id);
  const crear = useCrearCampania(empresa.id);
  const [borrador, setBorrador] = useState<BorradorDeFiltro>(BORRADOR_INICIAL);
  const [vista, setVista] = useState<VistaPreviaCampaniaDto | null>(null);
  const [nombre, setNombre] = useState('');
  const [confirmando, setConfirmando] = useState(false);

  if (!permisos.puede(SCOPES.expedientesAdministrar)) return <SinPermiso seccion={t('campanias.titulo')} />;

  // Cambiar el filtro descarta la vista previa: no queda en pantalla un resultado de otro filtro.
  const cambiar = (nuevo: BorradorDeFiltro) => {
    setBorrador(nuevo);
    setVista(null);
    previsualizar.reset();
  };
  const verVistaPrevia = () => previsualizar.mutate(filtroDeBorrador(borrador), { onSuccess: setVista });
  const confirmar = () => {
    if (!vista) return;
    crear.mutate(
      { filtro: vista.filtro, nombre: nombre.trim() || null },
      {
        onSuccess: (campania) => {
          mostrar(t('campanias.nuevaCampania.creada'), 'success');
          void navegar(`/e/${empresa.id}/vencimientos/campanias/${campania.id}`);
        },
        onError: (error) => {
          setConfirmando(false);
          mostrar(mensajeDeError(error, t), 'error');
        },
      },
    );
  };

  return (
    <>
      <EncabezadoPagina
        antes={<EnlaceVolver a={`/e/${empresa.id}/vencimientos/campanias`}>{t('campanias.titulo')}</EnlaceVolver>}
        titulo={t('campanias.nuevaCampania.titulo')}
        subtitulo={t('campanias.nuevaCampania.subtitulo')}
      />
      <Stack spacing={2}>
        {tipos.isLoading ? (
          <EsqueletoTarjeta alto={120} />
        ) : (
          <Tarjeta titulo={t('campanias.nuevaCampania.pasoFiltro')}>
            <FiltroCampania borrador={borrador} tipos={tipos.data} deshabilitado={previsualizar.isPending} onCambiar={cambiar} />
            <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 2 }}>
              <Button
                variant={vista ? 'outlined' : 'contained'}
                startIcon={previsualizar.isPending ? <IconoCarga /> : <Search size={16} aria-hidden />}
                disabled={!filtroValido(borrador) || previsualizar.isPending}
                onClick={verVistaPrevia}
              >
                {t('campanias.nuevaCampania.verVistaPrevia')}
              </Button>
            </Box>
          </Tarjeta>
        )}

        {previsualizar.isPending && <EsqueletoTarjeta alto={180} />}
        {previsualizar.isError && <Alert severity="error">{`${t('campanias.nuevaCampania.errorVistaPrevia')} ${mensajeDeError(previsualizar.error, t)}`}</Alert>}

        {vista && (
          <Tarjeta titulo={t('campanias.nuevaCampania.pasoRevision')}>
            <Stack spacing={2}>
              {!hayQueHacer(vista) && <Alert severity="info">{t('campanias.nuevaCampania.sinTrabajo')}</Alert>}
              <ResumenVistaPrevia vista={vista} />
              {vista.filas.length > 0 && <TablaVistaPreviaCampania filas={vista.filas} />}
              {vista.truncada && (
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  {t('campanias.nuevaCampania.truncada', { mostrados: String(vista.filas.length), total: String(vista.total) })}
                </Typography>
              )}
              <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-end', justifyContent: 'space-between' }}>
                <TextField
                  label={t('campanias.nuevaCampania.nombre')}
                  helperText={t('campanias.nuevaCampania.nombreAyuda')}
                  value={nombre}
                  onChange={(e) => setNombre(e.target.value)}
                  slotProps={{ htmlInput: { maxLength: 150 } }}
                  sx={{ width: 360, maxWidth: '100%' }}
                />
                <Button variant="contained" startIcon={<Megaphone size={16} aria-hidden />} disabled={!hayQueHacer(vista)} onClick={() => setConfirmando(true)}>
                  {t('campanias.nuevaCampania.abrirRenovaciones')}
                </Button>
              </Box>
            </Stack>
          </Tarjeta>
        )}
      </Stack>
      {confirmando && vista && <DialogoAvisoCampania vista={vista} ocupado={crear.isPending} onConfirmar={confirmar} onCerrar={() => setConfirmando(false)} />}
    </>
  );
}
