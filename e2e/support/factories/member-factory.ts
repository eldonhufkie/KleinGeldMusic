import { faker } from '@faker-js/faker';
import type { APIRequestContext } from '@playwright/test';
import { environment } from '../environment';
import { readIdentityForm } from '../helpers/identity-forms';
import { firstLink, waitForEmail } from '../helpers/mailpit';

export type NewMember = { name: string; email: string; password: string };

/** A Join Request nobody has sent before. Override any field. */
export const joinRequestFactory = (overrides: Partial<{ name: string; email: string; message: string }> = {}) => ({
  name: faker.person.fullName(),
  email: faker.internet.email({ provider: 'example.test' }).toLowerCase(),
  message: faker.lorem.sentence(),
  ...overrides,
});

/**
 * Brings a Member in the way real ones arrive: Join Request, Admin approval, set-password link from email 1.
 * Uses the request contexts it is given; `admin` must already hold the Admin's session cookie.
 *
 * playwright-utils deviation: runs inside the auth provider, before any fixture exists, so it uses the raw
 * request contexts (the mandate's "auth provider implementation" exception).
 */
export async function createMember(outsider: APIRequestContext, admin: APIRequestContext): Promise<NewMember> {
  const joinRequest = joinRequestFactory();

  const submitted = await outsider.post('/api/join-requests', { data: joinRequest });
  if (!submitted.ok()) {
    throw new Error(`Join Request answered ${submitted.status()}`);
  }

  const open = (await (await admin.get('/api/admin/join-requests')).json()) as { id: string; email: string }[];
  const mine = open.find((r) => r.email === joinRequest.email);
  if (!mine) {
    throw new Error(`Join Request for ${joinRequest.email} is not listed for the Admin`);
  }
  const approved = await admin.post(`/api/admin/join-requests/${mine.id}/approve`);
  if (!approved.ok()) {
    throw new Error(`Approving answered ${approved.status()}`);
  }

  const setupLink = firstLink(await waitForEmail(outsider, joinRequest.email));
  const setupPath = setupLink.pathname + setupLink.search;
  const form = await readIdentityForm(outsider, setupPath);
  form['Input.Password'] = environment.memberPassword;
  form['Input.ConfirmPassword'] = environment.memberPassword;
  const passwordSet = await outsider.post(setupPath, { form });
  if (!passwordSet.ok()) {
    throw new Error(`Setting the password answered ${passwordSet.status()}`);
  }

  return { name: joinRequest.name, email: joinRequest.email, password: environment.memberPassword };
}
