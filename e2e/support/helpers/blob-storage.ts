import type { APIRequestContext } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import path from 'node:path';

/**
 * What the browser does with an upload link from the API: one PUT straight to blob storage (Azurite here).
 *
 * playwright-utils deviation: the target is blob storage with a SAS link, not an application endpoint, and the
 * body is binary, which apiRequest would serialize as JSON.
 */
export async function putBlob(request: APIRequestContext, uploadLink: string, fixture: string): Promise<number> {
  const body = await readFile(fixturePath(fixture));
  const response = await request.put(uploadLink, {
    data: body,
    headers: { 'x-ms-blob-type': 'BlockBlob', 'Content-Type': 'application/octet-stream' },
  });
  if (response.status() !== 201) {
    throw new Error(`Uploading ${fixture} answered ${response.status()}: ${await response.text()}`);
  }
  return body.length;
}

export const fixturePath = (fixture: string) => path.join(__dirname, '..', '..', 'fixtures', fixture);
