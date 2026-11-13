// ESLint flat config del frontend (docs/06 §1.2): typescript-eslint strict-type-checked, sin `any`,
// hooks con dependencias exhaustivas, accesibilidad y MUI importado solo desde @mui/material.
import js from '@eslint/js';
import globals from 'globals';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      // Copias de trabajo (git worktrees) de Claude Code dentro del repo: cada una se valida en su propia carpeta.
      '.claude/**',
      '**/dist/**',
      '**/node_modules/**',
      '**/coverage/**',
      'packages/api-client/src/generado/**',
      'src/**',
      'tests/**',
      'infra/**',
      'docs/**',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        projectService: {
          allowDefaultProject: ['eslint.config.js', '*.mjs', 'apps/web/scripts/*.mjs', 'e2e/soporte/*.mjs', 'packages/api-client/openapi/*.mjs', 'marca/*.mjs'],
        },
        tsconfigRootDir: import.meta.dirname,
      },
      globals: { ...globals.browser },
    },
  },
  {
    files: ['**/*.{ts,tsx}'],
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-hooks/exhaustive-deps': 'error',
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
      '@typescript-eslint/no-confusing-void-expression': ['error', { ignoreArrowShorthand: true }],
      '@typescript-eslint/no-misused-promises': ['error', { checksVoidReturn: { attributes: false } }],
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            { group: ['@mui/system', '@mui/system/*'], message: 'Importa solo desde @mui/material.' },
            { group: ['**/mocks/**'], message: 'Los mocks solo se usan en pruebas.' },
          ],
        },
      ],
    },
  },
  {
    files: ['**/*.tsx'],
    ...jsxA11y.flatConfigs.recommended,
  },
  {
    // Biblioteca y herramientas: exportan componentes junto con sus tipos y helpers puros (o ninguno: el arnés de la marca); el HMR no aplica.
    files: ['packages/**/*.{ts,tsx}', 'marca/**/*.{ts,tsx}'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
  {
    files: ['**/*.test.{ts,tsx}', '**/pruebas/**'],
    rules: {
      'no-restricted-imports': 'off',
      '@typescript-eslint/no-non-null-assertion': 'off',
    },
  },
  {
    files: ['**/*.{js,mjs}'],
    ...tseslint.configs.disableTypeChecked,
    languageOptions: { ...tseslint.configs.disableTypeChecked.languageOptions, globals: { ...globals.node } },
  },
);
