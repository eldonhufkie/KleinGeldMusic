import type { APIRequestContext } from '@playwright/test';

/**
 * Reads an Identity page (static SSR) and returns its hidden form fields: the antiforgery token and Blazor's
 * `_handler`. The caller adds the visible fields and posts the result. Mirrors IdentityForms in the API tests.
 */
export async function readIdentityForm(request: APIRequestContext, path: string): Promise<Record<string, string>> {
  const response = await request.get(path);
  if (!response.ok()) {
    throw new Error(`GET ${path} answered ${response.status()}`);
  }

  const html = await response.text();
  const fields: Record<string, string> = {};
  for (const [input] of html.matchAll(/<input[^>]*type="hidden"[^>]*>/g)) {
    const name = /name="([^"]+)"/.exec(input)?.[1];
    const value = /value="([^"]*)"/.exec(input)?.[1];
    if (name) {
      fields[name] = decodeHtml(value ?? '');
    }
  }
  return fields;
}

const decodeHtml = (value: string) =>
  value.replaceAll('&amp;', '&').replaceAll('&quot;', '"').replaceAll('&#x2B;', '+').replaceAll('&#x2F;', '/');
