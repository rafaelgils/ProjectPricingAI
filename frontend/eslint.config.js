import js from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'coverage'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.strict],
    rules: {
      // standards.md §4: `any` só com comentário justificando (eslint-disable na linha).
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },
);
