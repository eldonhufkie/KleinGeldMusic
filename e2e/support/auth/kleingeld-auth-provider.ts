import { request as playwrightRequest, type APIRequestContext } from '@playwright/test';
import {
  type AuthProvider,
  getStorageStatePath,
  loadStorageState,
  saveStorageState,
} from '@seontechnologies/playwright-utils/auth-session';
import { environment, identityCookieName } from '../environment';
import { createMember } from '../factories/member-factory';
import { readIdentityForm } from '../helpers/identity-forms';

/**
 * Users the tests can be: `admin` (seeded by the migrator from the AppHost parameters) and any other
 * identifier, which becomes a fresh Member created through the real Join Request flow the first time it is asked for.
 * Sessions are ASP.NET Core Identity cookies, saved as Playwright storage state per identifier.
 */
export const ADMIN = 'admin';
export const MEMBER = 'member';

type StorageState = { cookies: { name: string; value: string; expires?: number }[] };

const cookieOf = (state: Record<string, unknown>) =>
  (state as StorageState).cookies?.find((c) => c.name === identityCookieName);

/** A request context with its own cookie jar, like a separate browser. */
const newSession = () => playwrightRequest.newContext({ baseURL: environment.baseUrl, ignoreHTTPSErrors: true });

async function logIn(session: APIRequestContext, email: string, password: string) {
  const form = await readIdentityForm(session, '/Account/Login');
  form['Input.Email'] = email;
  form['Input.Password'] = password;
  const response = await session.post('/Account/Login', { form });
  const state = await session.storageState();
  if (!response.ok() || !cookieOf(state)) {
    throw new Error(`Logging in as ${email} gave no session cookie (status ${response.status()})`);
  }
  return state;
}

const kleingeldAuthProvider: AuthProvider = {
  getEnvironment: (options) => options?.environment ?? process.env.TEST_ENV ?? 'local',

  getUserIdentifier: (options) => options?.userIdentifier ?? MEMBER,

  getBaseUrl: () => environment.baseUrl,

  extractToken: (tokenData) => cookieOf(tokenData)?.value ?? null,

  extractCookies: (tokenData) => (tokenData as StorageState).cookies ?? [],

  // Identity's cookie is a session cookie (expires -1) unless "Remember me"; the database behind it is what
  // really expires, and global-setup clears saved sessions at the start of every run for that reason.
  isTokenExpired: () => false,

  manageAuthToken: async (_request, options) => {
    const identifier = kleingeldAuthProvider.getUserIdentifier(options);
    const statePath = getStorageStatePath({
      environment: kleingeldAuthProvider.getEnvironment(options),
      userIdentifier: identifier,
    });

    const saved = loadStorageState(statePath);
    if (saved && cookieOf(saved)) {
      return saved;
    }

    const admin = await newSession();
    try {
      const adminState = await logIn(admin, environment.admin.email, environment.admin.password);
      if (identifier === ADMIN) {
        saveStorageState(statePath, adminState);
        return adminState;
      }

      const outsider = await newSession();
      const member = await newSession();
      try {
        const created = await createMember(outsider, admin);
        const memberState = await logIn(member, created.email, created.password);
        saveStorageState(statePath, memberState);
        return memberState;
      } finally {
        await outsider.dispose();
        await member.dispose();
      }
    } finally {
      await admin.dispose();
    }
  },

  clearToken: (options) => {
    const statePath = getStorageStatePath({
      environment: kleingeldAuthProvider.getEnvironment(options),
      userIdentifier: kleingeldAuthProvider.getUserIdentifier(options),
    });
    saveStorageState(statePath, { cookies: [], origins: [] });
  },
};

export default kleingeldAuthProvider;
