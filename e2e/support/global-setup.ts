import { request } from '@playwright/test';
import {
  authStorageInit,
  configureAuthSession,
  getAuthToken,
  getStorageDir,
  setAuthProvider,
} from '@seontechnologies/playwright-utils/auth-session';
import { rmSync } from 'node:fs';
import path from 'node:path';
import kleingeldAuthProvider, { ADMIN, MEMBER } from './auth/kleingeld-auth-provider';
import { environment } from './environment';

/**
 * Runs once before all tests, after the web server is healthy.
 *
 * Saved sessions only make sense against the database they were made in, and a fresh AppHost starts with an empty
 * one, so they are cleared every run. The Admin and the default Member are then created once here, so parallel
 * workers never race to create the same Member.
 */
export default async function globalSetup() {
  // auth-session keeps everything under <cwd>/.auth (see AuthStorageConfig); remove only that folder.
  const authDir = path.join(process.cwd(), '.auth');
  if (!getStorageDir().startsWith(authDir)) {
    throw new Error(`auth-session stores sessions in ${getStorageDir()}, outside ${authDir}; not clearing anything`);
  }
  rmSync(authDir, { recursive: true, force: true });

  authStorageInit();
  configureAuthSession({ debug: false });
  setAuthProvider(kleingeldAuthProvider);

  const context = await request.newContext({ baseURL: environment.baseUrl, ignoreHTTPSErrors: true });
  try {
    await getAuthToken(context, { userIdentifier: ADMIN });
    await getAuthToken(context, { userIdentifier: MEMBER });
  } finally {
    await context.dispose();
  }
}
