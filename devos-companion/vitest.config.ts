import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    include: ['tests/**/*.test.ts'],
    coverage: { provider: 'v8' },
  },
  resolve: {
    alias: {
      vscode: new URL('./tests/mocks/vscode.ts', import.meta.url).pathname,
    },
  },
});
