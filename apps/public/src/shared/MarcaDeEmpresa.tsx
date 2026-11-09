import { Box, Link, Stack, ThemeProvider, Typography } from '@mui/material';
import type { EmpresaPublicaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { useMemo, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { LogoEmpresa } from './componentes';

/**
 * La marca de la empresa en lo que el tercero hace con ella (CF-1): su logo arriba, su paleta (derivada del color de
 * marca con contraste garantizado, `marcaEmpresa`) en botones y enlaces, y su correo de soporte al pie. Sin color, el
 * tema de Validasoft; la bóveda y la navegación siguen siendo de Validasoft porque el tercero tiene varias empresas.
 */
export function MarcaDeEmpresa({ empresa, children }: { empresa: EmpresaPublicaDto; children: ReactNode }) {
  const { t } = useTranslation();
  const color = empresa.colorMarca ?? null;
  const tema = useMemo(() => (color ? createValidasoftTheme('light', { app: 'public', colorEmpresa: color }) : null), [color]);
  const contenido = (
    <>
      <Stack direction="row" spacing={1.25} alignItems="center" sx={{ mb: 1.5 }}>
        <LogoEmpresa nombre={empresa.nombre} logoUrl={empresa.logoUrl} tamano={32} />
        <Typography component="span" sx={{ fontSize: 14, color: 'text.secondary' }}>
          {empresa.nombre}
        </Typography>
      </Stack>
      {children}
      {empresa.contactoSoporte && (
        <Box component="p" sx={{ m: 0, mt: 3, fontSize: 13, color: 'text.secondary', textAlign: 'center' }}>
          {t('marca.soporte', { empresa: empresa.nombre })}{' '}
          <Link href={`mailto:${empresa.contactoSoporte}`}>{empresa.contactoSoporte}</Link>
        </Box>
      )}
    </>
  );
  return tema ? <ThemeProvider theme={tema}>{contenido}</ThemeProvider> : contenido;
}
