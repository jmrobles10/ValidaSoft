import { Alert, Box, Button, FormControlLabel, Stack, Switch, Typography } from '@mui/material';
import type { MatrizDeRiesgoDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFecha } from '@validasoft/ui';
import { Gauge, Plus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, IconoCarga, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useGuardarMatriz } from './api';
import { SimuladorMatriz } from './SimuladorMatriz';
import { TarjetaFactor } from './TarjetaFactor';
import { TarjetaLimites } from './TarjetaLimites';
import { TarjetaNiveles } from './TarjetaNiveles';
import {
  actualizarFactor,
  actualizarOpcion,
  agregarFactor,
  agregarOpcion,
  borradorDeMatriz,
  cambiarNivel,
  hayCambios,
  matrizADto,
  MAXIMO_FACTORES,
  pesoRelativo,
  quitarFactor,
  quitarOpcion,
  validarMatriz,
  type BorradorMatriz,
} from './modelo';

/**
 * Editor de la matriz de la empresa: activar o no, factores como tarjetas, límites con su barra, qué exige cada nivel y una
 * prueba con el resultado en vivo. Todo se valida aquí antes de enviar; guardar avisa qué pasará con los estudios.
 */
export function EditorMatriz({ empresaId, nombreEmpresa, matriz, puedeEscribir }: { empresaId: string; nombreEmpresa: string; matriz: MatrizDeRiesgoDto; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useGuardarMatriz(empresaId);
  const inicial = useMemo(() => borradorDeMatriz(matriz), [matriz]);
  const [borrador, setBorrador] = useState<BorradorMatriz>(inicial);
  const [confirmando, setConfirmando] = useState<'guardar' | 'plantilla' | null>(null);
  const errores = validarMatriz(borrador);
  const cambios = hayCambios(inicial, borrador);
  const cambiar = (cambio: (b: BorradorMatriz) => BorradorMatriz) => setBorrador(cambio);
  const factores = borrador.factores;

  const enviar = () => {
    const dto = confirmando === 'plantilla' ? { ...matrizADto(inicial), activa: true } : matrizADto(borrador);
    guardar.mutate(dto, {
      onSuccess: () => {
        mostrar(t('matrizRiesgo.guardar.guardada'));
        setConfirmando(null);
      },
      onError: () => setConfirmando(null),
    });
  };

  const acciones = puedeEscribir && (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
      {cambios && (
        <Typography component="span" role="status" sx={{ color: errores.length > 0 ? 'error.main' : 'text.secondary', fontSize: 12 }}>
          {errores.length > 0 ? t('matrizRiesgo.guardar.corrige', { count: errores.length }) : t('matrizRiesgo.guardar.pendientes')}
        </Typography>
      )}
      {cambios && (
        <Button variant="text" onClick={() => setBorrador(inicial)} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
          {t('matrizRiesgo.guardar.descartar')}
        </Button>
      )}
      <Button variant="contained" onClick={() => setConfirmando('guardar')} disabled={!cambios || errores.length > 0 || guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
        {t('matrizRiesgo.guardar.boton')}
      </Button>
    </Stack>
  );

  return (
    <>
      <EncabezadoPagina titulo={t('matrizRiesgo.titulo')} subtitulo={t('matrizRiesgo.subtitulo', { empresa: nombreEmpresa })} acciones={acciones || undefined} />
      <Stack spacing={2}>
        {!puedeEscribir && <Alert severity="info">{t('matrizRiesgo.soloLectura')}</Alert>}
        {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
        {matriz.esPorDefecto && (
          <Alert
            severity="info"
            action={
              puedeEscribir && !cambios ? (
                <Button size="small" variant="outlined" onClick={() => setConfirmando('plantilla')}>
                  {t('matrizRiesgo.plantilla.usar')}
                </Button>
              ) : undefined
            }
          >
            <Typography component="span" sx={{ fontWeight: 600, display: 'block' }}>
              {t('matrizRiesgo.plantilla.titulo')}
            </Typography>
            {t('matrizRiesgo.plantilla.texto')}
          </Alert>
        )}

        <Tarjeta titulo={t('matrizRiesgo.uso.titulo')} etiqueta={t('matrizRiesgo.uso.titulo')}>
          <FormControlLabel
            control={<Switch checked={borrador.activa} onChange={(e) => cambiar((b) => ({ ...b, activa: e.target.checked }))} disabled={!puedeEscribir} />}
            label={t('matrizRiesgo.uso.etiqueta')}
            sx={{ mx: 0 }}
          />
          <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{borrador.activa ? t('matrizRiesgo.uso.activa') : t('matrizRiesgo.uso.inactiva')}</Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5 }}>
            {matriz.actualizadaEn ? t('matrizRiesgo.uso.version', { version: String(matriz.version), fecha: formatearFecha(matriz.actualizadaEn) }) : t('matrizRiesgo.uso.sinVersion')}
          </Typography>
          {errores.some((e) => e.codigo === 'sin-factores') && (
            <Typography role="alert" sx={{ color: 'error.main', fontSize: 12, mt: 0.5 }}>
              {t('matrizRiesgo.validacion.sin-factores')}
            </Typography>
          )}
        </Tarjeta>

        <Tarjeta
          titulo={t('matrizRiesgo.factores.titulo')}
          etiqueta={t('matrizRiesgo.factores.titulo')}
          acciones={
            puedeEscribir && (
              <Button size="small" variant="outlined" startIcon={<Plus size={16} aria-hidden />} onClick={() => cambiar(agregarFactor)} disabled={factores.length >= MAXIMO_FACTORES}>
                {t('matrizRiesgo.factores.agregar')}
              </Button>
            )
          }
        >
          <Typography sx={{ color: 'text.secondary', fontSize: 13, mb: 1.5 }}>
            {t('matrizRiesgo.factores.ayuda')} {t('matrizRiesgo.factores.cuenta', { n: String(factores.length), maximo: String(MAXIMO_FACTORES) })}
          </Typography>
          {factores.length === 0 ? (
            <EstadoVacio icono={Gauge} titulo={t('matrizRiesgo.factores.vacio')} texto={t('matrizRiesgo.factores.vacioAyuda')} compacto />
          ) : (
            <Box sx={{ columnWidth: 440, columnGap: '12px' }}>
              {factores.map((f) => (
                <TarjetaFactor
                  key={f.clave}
                  factor={f}
                  porcentaje={pesoRelativo(borrador, f)}
                  errores={errores}
                  deshabilitado={!puedeEscribir}
                  onCambiar={(cambio) => cambiar((b) => actualizarFactor(b, f.clave, cambio))}
                  onQuitar={() => cambiar((b) => quitarFactor(b, f.clave))}
                  onCambiarOpcion={(opcion, cambio) => cambiar((b) => actualizarOpcion(b, f.clave, opcion, cambio))}
                  onAgregarOpcion={() => cambiar((b) => agregarOpcion(b, f.clave))}
                  onQuitarOpcion={(opcion) => cambiar((b) => quitarOpcion(b, f.clave, opcion))}
                />
              ))}
            </Box>
          )}
        </Tarjeta>

        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', lg: 'repeat(2, minmax(0, 1fr))' }, gap: 2, alignItems: 'start' }}>
          <Tarjeta titulo={t('matrizRiesgo.limites.titulo')} etiqueta={t('matrizRiesgo.limites.titulo')}>
            <TarjetaLimites
              limiteBajo={borrador.limiteBajo}
              limiteMedio={borrador.limiteMedio}
              invalido={errores.some((e) => e.codigo === 'limites')}
              deshabilitado={!puedeEscribir}
              onCambiar={(cambio) => cambiar((b) => ({ ...b, ...cambio }))}
            />
          </Tarjeta>
          <Tarjeta titulo={t('matrizRiesgo.niveles.titulo')} etiqueta={t('matrizRiesgo.niveles.titulo')}>
            <TarjetaNiveles niveles={borrador.niveles} errores={errores} deshabilitado={!puedeEscribir} onCambiar={(nivel, cambio) => cambiar((b) => cambiarNivel(b, nivel, cambio))} />
          </Tarjeta>
        </Box>

        <Tarjeta titulo={t('matrizRiesgo.prueba.titulo')} etiqueta={t('matrizRiesgo.prueba.titulo')}>
          <SimuladorMatriz borrador={borrador} />
        </Tarjeta>
      </Stack>

      {confirmando && (
        <DialogoConfirmacion
          tono="primary"
          titulo={confirmando === 'plantilla' ? t('matrizRiesgo.plantilla.usarTitulo') : t('matrizRiesgo.guardar.titulo')}
          mensaje={
            confirmando === 'plantilla'
              ? t('matrizRiesgo.plantilla.usarAviso')
              : `${t('matrizRiesgo.guardar.aviso')} ${borrador.activa ? t('matrizRiesgo.guardar.avisoActiva') : t('matrizRiesgo.guardar.avisoInactiva')}`
          }
          confirmar={confirmando === 'plantilla' ? t('matrizRiesgo.plantilla.usarConfirmar') : t('matrizRiesgo.guardar.confirmar')}
          ocupado={guardar.isPending}
          onConfirmar={enviar}
          onCerrar={() => setConfirmando(null)}
        />
      )}
    </>
  );
}
