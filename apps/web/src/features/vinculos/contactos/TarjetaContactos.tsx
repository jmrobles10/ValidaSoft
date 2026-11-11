import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from '@mui/material';
import { SCOPES, type VinculoDto } from '@validasoft/api-client';
import { aEditables, desdeEditables, EditorDeContactos, FormularioDeDialogo, ROLES_DE_CONTACTO, validarContactos, type ContactoEditable } from '@validasoft/ui';
import { Pencil } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useActualizarContactos } from './api';

/**
 * Contactos del tercero con esta empresa (IO-3): comercial, pagos, cumplimiento y notificaciones. Los edita la empresa desde aquí o el
 * tercero desde su ficha; salen en la API y en el webhook para los sistemas integradores. Solo con `vinculos:escribir` se editan.
 */
export function TarjetaContactos({ vinculo }: { vinculo: Pick<VinculoDto, 'id' | 'contactos' | 'estado'> }) {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const [editando, setEditando] = useState(false);
  const contactos = vinculo.contactos ?? [];
  const puedeEditar = permisos.puede(SCOPES.vinculosEscribir) && vinculo.estado !== 'Anulado';

  return (
    <Tarjeta
      titulo={t('vinculos.contactos.titulo')}
      etiqueta={t('vinculos.contactos.titulo')}
      acciones={
        puedeEditar ? (
          <Button variant="text" size="small" startIcon={<Pencil size={16} aria-hidden />} onClick={() => setEditando(true)}>
            {t('vinculos.contactos.editar')}
          </Button>
        ) : undefined
      }
    >
      {contactos.length === 0 ? (
        <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('vinculos.contactos.vacio')}</Typography>
      ) : (
        <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {contactos.map((c) => (
            <Box component="li" key={`${c.correo}|${c.rol}`} sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', sm: '120px minmax(0,1fr)' }, columnGap: 2 }}>
              <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '20px' }}>{t(`vinculos.contactos.roles.${c.rol}`, { defaultValue: c.rol })}</Typography>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontSize: 14, lineHeight: '20px' }}>
                  {c.nombre}
                  {c.cargo ? ` · ${c.cargo}` : ''}
                </Typography>
                <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px', overflowWrap: 'anywhere' }}>
                  {c.correo}
                  {c.telefono ? ` · ${c.telefono}` : ''}
                </Typography>
              </Box>
            </Box>
          ))}
        </Stack>
      )}
      {editando && <DialogoContactos vinculo={vinculo} empresaId={empresa.id} onCerrar={() => setEditando(false)} />}
    </Tarjeta>
  );
}

function DialogoContactos({ vinculo, empresaId, onCerrar }: { vinculo: Pick<VinculoDto, 'id' | 'contactos'>; empresaId: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useActualizarContactos(empresaId, vinculo.id);
  const [contactos, setContactos] = useState<ContactoEditable[]>(() => aEditables(vinculo.contactos));
  const [intentado, setIntentado] = useState(false);
  const errores = validarContactos(contactos, { nombre: t('vinculos.contactos.errores.nombre'), correo: t('vinculos.contactos.errores.correo'), repetido: t('vinculos.contactos.errores.repetido') });

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (Object.keys(errores).length > 0) return;
    guardar.mutate(desdeEditables(contactos), {
      onSuccess: () => {
        mostrar(t('vinculos.contactos.exito'));
        onCerrar();
      },
    });
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="titulo-contactos">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-contactos">{t('vinculos.contactos.dialogo')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography sx={{ color: 'text.secondary' }}>{t('vinculos.contactos.ayuda')}</Typography>
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            <EditorDeContactos
              contactos={contactos}
              onCambiar={setContactos}
              roles={ROLES_DE_CONTACTO.map((r) => ({ valor: r, etiqueta: t(`vinculos.contactos.roles.${r}`) }))}
              etiquetas={{
                nombre: t('vinculos.contactos.nombre'),
                cargo: t('vinculos.contactos.cargo'),
                correo: t('vinculos.contactos.correo'),
                telefono: t('vinculos.contactos.telefono'),
                rol: t('vinculos.contactos.rol'),
                agregar: t('vinculos.contactos.agregar'),
                quitar: (n) => t('vinculos.contactos.quitar', { n: String(n) }),
                vacio: t('vinculos.contactos.vacioEditor'),
                contacto: (n) => t('vinculos.contactos.contacto', { n: String(n) }),
              }}
              errores={intentado ? errores : {}}
              deshabilitado={guardar.isPending}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardar.isPending}>
            {t('vinculos.contactos.guardar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
