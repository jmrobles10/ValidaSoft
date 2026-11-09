import { Divider, Stack, Typography } from '@mui/material';
import type { ContactoDto, TerceroDto } from '@validasoft/api-client';
import { Dato, formatearFecha, RejillaDatos } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../shared/componentes';
import { identificacionDe } from '../vinculos/tercero';
import { textoTipoPersona } from './modelo';

export const Mono = ({ children }: { children: string }) => (
  <Typography component="span" variant="mono">
    {children}
  </Typography>
);

/** Identificación del tercero (global, no depende de la empresa) y sus contactos. */
export function DatosTercero({ tercero }: { tercero: TerceroDto }) {
  const { t } = useTranslation();
  return (
    <Tarjeta titulo={t('terceros.ficha.identificacion.titulo')} etiqueta={t('terceros.ficha.identificacion.titulo')}>
      <Stack spacing={2}>
        <RejillaDatos columnas={2}>
          <Dato etiqueta={t('terceros.ficha.identificacion.nombre')}>{tercero.nombre}</Dato>
          {tercero.nombreComercial && <Dato etiqueta={t('terceros.ficha.identificacion.nombreComercial')}>{tercero.nombreComercial}</Dato>}
          <Dato etiqueta={t('terceros.ficha.identificacion.identificacion')}>
            <Mono>{identificacionDe(tercero)}</Mono>
          </Dato>
          <Dato etiqueta={t('terceros.ficha.identificacion.pais')}>
            {tercero.identificacion.pais}
            {tercero.esExtranjero ? ` · ${t('terceros.ficha.extranjero')}` : ''}
          </Dato>
          <Dato etiqueta={t('terceros.ficha.identificacion.tipoPersona')}>{textoTipoPersona(tercero.tipoPersona, t)}</Dato>
          <Dato etiqueta={t('terceros.ficha.identificacion.registrado')}>
            <Mono>{formatearFecha(tercero.creadoEn)}</Mono>
          </Dato>
        </RejillaDatos>
        <Stack component="section" aria-label={t('terceros.ficha.identificacion.contactos')} spacing={0.5}>
          <Typography variant="overline" component="h3" sx={{ color: 'text.secondary' }}>
            {t('terceros.ficha.identificacion.contactos')}
          </Typography>
          {tercero.contactos.length === 0 ? (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('terceros.ficha.identificacion.sinContactos')}
            </Typography>
          ) : (
            <Stack component="ul" divider={<Divider component="li" aria-hidden />} sx={{ p: 0, m: 0, listStyle: 'none' }}>
              {tercero.contactos.map((c) => (
                <Contacto key={`${c.correo}|${c.rol}`} contacto={c} />
              ))}
            </Stack>
          )}
        </Stack>
      </Stack>
    </Tarjeta>
  );
}

/** Un contacto: nombre y rol en la primera línea; correo y teléfono (en mono, para copiarlos tal cual) en la segunda. */
function Contacto({ contacto }: { contacto: ContactoDto }) {
  return (
    <Stack component="li" spacing={0.25} sx={{ py: 0.75, minWidth: 0 }}>
      <Typography sx={{ fontWeight: 500 }}>
        {contacto.nombre}
        {contacto.rol && (
          <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12, fontWeight: 400, ml: 1 }}>
            {contacto.rol}
          </Typography>
        )}
      </Typography>
      <Typography variant="body2" sx={{ color: 'text.secondary', overflowWrap: 'anywhere' }}>
        <Mono>{contacto.correo}</Mono>
        {contacto.telefono && (
          <>
            {' · '}
            <Mono>{contacto.telefono}</Mono>
          </>
        )}
      </Typography>
    </Stack>
  );
}
