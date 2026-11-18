import type { APIRequestContext } from '@playwright/test';
import { recurse } from '@seontechnologies/playwright-utils/recurse';
import { environment } from '../environment';

type MessageSummary = { ID: string; Subject: string };
type SearchResult = { messages: MessageSummary[] };
type Message = { Text: string };

/**
 * Waits for the newest email to this address and returns its plain text, read from MailPit's API.
 *
 * playwright-utils deviation: MailPit is a test mail server, not an application endpoint, and apiRequest
 * would resolve paths against the app's baseURL; the raw request context is used against MailPit only.
 */
export async function waitForEmail(request: APIRequestContext, to: string): Promise<string> {
  const search = await recurse(
    async () => {
      const response = await request.get(`${environment.mailPitUrl}/api/v1/search`, {
        params: { query: `to:"${to}"` },
      });
      return (await response.json()) as SearchResult;
    },
    (result) => result.messages.length > 0,
    { timeout: 30_000, interval: 500, error: `No email to ${to} arrived in MailPit` },
  );

  const message = await request.get(`${environment.mailPitUrl}/api/v1/message/${search.messages[0].ID}`);
  return ((await message.json()) as Message).Text;
}

/** The first https link in an email, e.g. the set-password link in email 1. */
export const firstLink = (text: string): URL => {
  const match = /https:\/\/\S+/.exec(text);
  if (!match) {
    throw new Error(`No link in email:\n${text}`);
  }
  return new URL(match[0]);
};
