import { defineConfig } from '@playwright/test';

try { process.loadEnvFile('.env'); } catch { /* env may come from the shell */ }

export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.UAT_UI_URL ?? 'http://localhost:4200',
    ignoreHTTPSErrors: true,
    channel: 'chrome',
    storageState: '.auth/uat.json',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
});
