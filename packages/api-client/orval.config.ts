import { defineConfig } from 'orval';
import { transformarOpenApi } from './openapi/transformar';

// Cliente generado desde el snapshot del contrato (docs/06 §1.2): sin la API levantada.
// Regenerar: `pnpm generar:api` (y `openapi:actualizar` antes, si cambió el backend).
export default defineConfig({
  validasoft: {
    input: {
      target: './openapi/v1.json',
      override: { transformer: (documento) => transformarOpenApi(documento) as unknown as typeof documento },
    },
    output: {
      mode: 'tags-split',
      target: './src/generado/endpoints',
      schemas: './src/generado/modelos',
      client: 'react-query',
      httpClient: 'axios',
      clean: true,
      override: {
        mutator: { path: './src/fetcher.ts', name: 'clienteValidasoft' },
        query: { signal: true },
      },
    },
  },
});
