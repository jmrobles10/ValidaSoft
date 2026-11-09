import { Divider, Stack, Typography } from '@mui/material';
import type { EmpresaDto, VinculoEnOtraEmpresaDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { Building2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../shared/componentes';
import { esEstadoVinculo, nombreEmpresaVisible } from './modelo';

/**
 * Otras empresas del grupo con vínculo con el tercero: solo que existe y su estado, nunca sus decisiones. El nombre se
 * muestra si la persona tiene acceso a esa empresa; si no, «Otra empresa del grupo». Sin otras empresas no se pinta.
 */
export function OtrasEmpresas({ otras, empresas }: { otras: readonly VinculoEnOtraEmpresaDto[]; empresas: readonly EmpresaDto[] }) {
  const { t } = useTranslation();
  if (otras.length === 0) return null;

  return (
    <Tarjeta titulo={t('terceros.ficha.otrasEmpresas.titulo')} etiqueta={t('terceros.ficha.otrasEmpresas.titulo')}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1 }}>
        {t('terceros.ficha.otrasEmpresas.ayuda')}
      </Typography>
      <Stack component="ul" divider={<Divider component="li" aria-hidden />} sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {otras.map((otra) => {
          const nombre = nombreEmpresaVisible(otra.empresaId, empresas);
          return (
            <Stack component="li" key={otra.empresaId} direction="row" spacing={1.25} sx={{ alignItems: 'center', py: 0.75, minHeight: 40 }}>
              <Building2 size={18} strokeWidth={1.75} aria-hidden style={{ flex: 'none', opacity: 0.7 }} />
              <Typography noWrap sx={{ flex: 1, minWidth: 0, color: nombre ? 'text.primary' : 'text.secondary' }}>
                {nombre ?? t('terceros.ficha.otrasEmpresas.sinAcceso')}
              </Typography>
              {esEstadoVinculo(otra.estado) ? <ChipEstado estado={otra.estado} /> : <Typography variant="body2">{otra.estado}</Typography>}
            </Stack>
          );
        })}
      </Stack>
    </Tarjeta>
  );
}
