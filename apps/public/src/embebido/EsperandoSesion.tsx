import { Button } from '@mui/material';
import { EsqueletoFormulario, PaginaEstado } from '@validasoft/ui';
import { ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useEstadoPuente } from './estadoPuente';

/**
 * Lo que se ve dentro del iframe mientras el sistema anfitrión no entrega la sesión (nunca el formulario de «Entrar con mi correo»:
 * la identidad la puso el sistema). Si el contenedor no es uno permitido o el token no corresponde, un aviso y no datos.
 */
export function EsperandoSesion() {
  const { t } = useTranslation();
  const puente = useEstadoPuente((s) => s.estado);
  if (puente === 'sin-contenedor' || puente === 'rechazada') {
    return (
      <PaginaEstado
        icono={ShieldAlert}
        titulo={t('embebido.noDisponible.titulo')}
        texto={t(puente === 'sin-contenedor' ? 'embebido.noDisponible.contenedor' : 'embebido.noDisponible.sesion')}
        acciones={
          <Button variant="contained" onClick={() => window.location.reload()}>
            {t('embebido.reintentar')}
          </Button>
        }
      />
    );
  }

  return <EsqueletoFormulario campos={2} conTarjeta etiqueta={t('embebido.esperando')} sx={{ maxWidth: 420, mx: 'auto', mt: 4 }} />;
}
