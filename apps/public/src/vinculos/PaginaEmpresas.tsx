import { Box, Button, Card, Stack, Typography } from '@mui/material';
import type { VinculoPublicoDto } from '@validasoft/api-client';
import { ChipEstado, EstadoVacio, formatearFecha } from '@validasoft/ui';
import { Building2, ChevronRight, Contact, Upload } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { useExpedientePublico, useVinculosPublicos } from '../shared/api';
import { CabeceraPagina, ErrorRecuperable, EsqueletoTarjetas, LogoEmpresa } from '../shared/componentes';
import { avanceDelTercero } from './avance';
import { AvisoRechazo } from './AvisoRechazo';
import { ChipAvance } from './ChipAvance';
import { DialogoContactos } from './DialogoContactos';
import { useTextoAvance } from './useTextoAvance';
import { situacionDelVinculo } from './situacion';

/** Mis empresas: las que invitaron al tercero, con lo que le toca con cada una y el siguiente paso. */
export function PaginaEmpresas() {
  const { t } = useTranslation();
  const consulta = useVinculosPublicos();

  return (
    <>
      <CabeceraPagina titulo={t('empresas.titulo')} sub={t('empresas.sub')} />
      {consulta.isPending ? (
        <EsqueletoTarjetas />
      ) : consulta.isError ? (
        <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />
      ) : consulta.data.length === 0 ? (
        <EstadoVacio icono={Building2} titulo={t('empresas.vacio')} texto={t('empresas.vacioTexto')} />
      ) : (
        <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {consulta.data.map((v) => (
            <TarjetaEmpresa key={v.id} vinculo={v} />
          ))}
        </Stack>
      )}
    </>
  );
}

/**
 * Una empresa: con la invitación aceptada se consulta su expediente para decir lo que falta («Faltan 3 documentos»,
 * «En revisión», «Tienes observaciones», «Aprobado») en vez del estado interno del vínculo («En validación»).
 */
function TarjetaEmpresa({ vinculo: v }: { vinculo: VinculoPublicoDto }) {
  const { t } = useTranslation();
  const textoAvance = useTextoAvance();
  const situacion = situacionDelVinculo(v.estado);
  const expedienteId = v.expedienteId ?? '';
  const conExpediente = expedienteId !== '' && situacion === 'vigente' && v.invitacionAceptada;
  const expediente = useExpedientePublico(expedienteId, conExpediente);
  const avance = avanceDelTercero(v, conExpediente ? expediente.data : null);
  // Mientras llega el expediente no se muestra «En validación»: el chip espera (salvo que el vínculo ya diga algo propio).
  const esperando = conExpediente && expediente.isPending && avance === null;
  const [editandoContactos, setEditandoContactos] = useState(false);

  return (
    <Card component="li" sx={{ p: 2 }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
        <LogoEmpresa nombre={v.empresa.nombre} logoUrl={v.empresa.logoUrl} />
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography component="h2" sx={{ fontWeight: 600, fontSize: 16, lineHeight: '22px' }}>
            {v.empresa.nombre}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>
            {v.tipoVinculo.nombre}
          </Typography>
        </Box>
        {avance ? (
          <ChipAvance avance={avance} />
        ) : v.reintento ? (
          <ChipEstado estado="Rechazado" size="medium" />
        ) : (
          !esperando && <ChipEstado estado={v.estado} size="medium" />
        )}
      </Stack>
      {v.reintento ? (
        <AvisoRechazo vinculo={v} />
      ) : (
        <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px', mt: 1.5 }}>
          {situacion !== 'vigente'
            ? t(`vinculoSituacion.${situacion}.corto`)
            : avance
              ? textoAvance(avance, v.empresa.nombre)
              : v.invitacionAceptada && v.invitacionAceptadaEn
                ? t('empresas.aceptada', { fecha: formatearFecha(v.invitacionAceptadaEn) })
                : t('empresas.pendienteAceptar')}
        </Typography>
      )}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.5 }}>
        {situacion === 'vigente' && !v.invitacionAceptada && (
          <Button component={RouterLink} to={`/empresas/${v.id}`} variant="contained" endIcon={<ChevronRight size={18} aria-hidden />}>
            {t('empresas.aceptar')}
          </Button>
        )}
        {v.expedienteId && situacion !== 'terminada' && !v.reintento && (
          <Button
            component={RouterLink}
            to={`/expediente/${v.expedienteId}`}
            variant={v.invitacionAceptada ? 'contained' : 'outlined'}
            startIcon={avance?.tipo === 'faltan' ? <Upload size={18} aria-hidden /> : undefined}
          >
            {avance?.tipo === 'faltan' ? t('empresas.subirDocumentos') : avance?.tipo === 'observado' ? t('empresas.verObservaciones') : t('empresas.verExpediente')}
          </Button>
        )}
        {v.invitacionAceptada && situacion !== 'terminada' && (
          <Button variant="outlined" startIcon={<Contact size={18} aria-hidden />} onClick={() => setEditandoContactos(true)} aria-label={t('contactos.abrirCon', { empresa: v.empresa.nombre })}>
            {t('contactos.abrir')} · {t('contactos.resumen', { count: v.contactos?.length ?? 0 })}
          </Button>
        )}
      </Stack>
      {editandoContactos && <DialogoContactos vinculo={v} onCerrar={() => setEditandoContactos(false)} />}
    </Card>
  );
}
