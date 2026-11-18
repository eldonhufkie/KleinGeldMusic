import { defineConfig, devices } from '@playwright/test';
import 'dotenv/config';

/**
 * Browser tests for KleinGeld Music.
 *
 * Playwright starts the whole app through the Aspire AppHost: PostgreSQL (no data volume, so every run
 * starts empty), Azurite, MailPit on a fixed port, the migrator (which seeds the Admin), the worker and the web host.
 * Locally an already-running AppHost is reused; it must use the same Admin and MailPit settings (see README).
 */
const baseURL = process.env.BASE_URL ?? 'https://localhost:7201';
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@kleingeld.test';
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin-Pa55word!';
const mailPitPort = process.env.MAILPIT_PORT ?? '8025';
const isCI = !!process.env.CI;

export default defineConfig({
  testDir: './tests',
  globalSetup: './support/global-setup.ts',
  outputDir: './test-results',

  timeout: 60_000,
  expect: { timeout: 10_000 },

  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  workers: isCI ? 2 : undefined,

  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['junit', { outputFile: 'test-results/junit.xml' }],
  ],

  use: {
    baseURL,
    // The ASP.NET Core development certificate; trusted on dev machines, not always inside CI containers.
    ignoreHTTPSErrors: true,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure-and-retries',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] }, grepInvert: /@phone/ },
    // The phone layout (bottom tab bar, mini player) only shows below ~700 px.
    { name: 'phone', use: { ...devices['Pixel 7'] }, grep: /@phone/ },
  ],

  webServer: {
    command: [
      'dotnet run --project ../src/KleinGeld.AppHost --launch-profile https --',
      '--Postgres:DataVolume=false',
      `--MailPit:Port=${mailPitPort}`,
      `--Parameters:admin-email=${adminEmail}`,
      `--Parameters:admin-password=${adminPassword}`,
    ].join(' '),
    url: `${baseURL}/health`,
    ignoreHTTPSErrors: true,
    reuseExistingServer: !isCI,
    // First start pulls PostgreSQL, Azurite and MailPit images and builds the solution.
    timeout: 5 * 60_000,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
