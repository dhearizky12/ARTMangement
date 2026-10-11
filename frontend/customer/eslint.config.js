import js from '@eslint/js'
import globals from 'globals'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'

export default tseslint.config(
  { ignores: ['dist/**', 'node_modules/**', 'src/api/schema.d.ts'] },

  js.configs.recommended,
  ...tseslint.configs.recommended,

  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: { ...globals.browser },
    },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // Styling goes through utilities and data-* keys, never inline.
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='style']",
          message: 'No inline styles. Use a utility class or a data-* key bound in tokens.css.',
        },
        {
          selector: "MemberExpression[property.name='setProperty'][object.property.name='style']",
          message: 'Only applyAgentColors() writes styles at runtime.',
        },
      ],
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },

  // src/ui is presentation only: no domain imports.
  {
    files: ['src/ui/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['**/data/**', '**/session/**', '**/booking/**', '**/agent/**', '**/screens/**'],
              message: 'src/ui is domain-free. Pass values in as props.',
            },
          ],
        },
      ],
    },
  },

  {
    files: ['src/agent/applyAgentColors.ts'],
    rules: { 'no-restricted-syntax': 'off' },
  },

  {
    files: ['tooling/**/*.{ts,mjs}', '*.config.{ts,js}'],
    languageOptions: { globals: { ...globals.node } },
  },
)
