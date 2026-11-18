import { faker } from '@faker-js/faker';
import type { APIRequestContext } from '@playwright/test';
import { recurse } from '@seontechnologies/playwright-utils/recurse';
import { asMember } from '../environment';
import { statSync } from 'node:fs';
import { fixturePath, putBlob } from '../helpers/blob-storage';

/** The shape of the `apiRequest` fixture, so factories can take it as a parameter. */
export type ApiRequest = <T = unknown>(params: {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  path: string;
  body?: unknown;
  headers?: Record<string, string>;
}) => Promise<{ status: number; body: T }>;

type ArtistSummary = { id: string; name: string; slug: string };
type ReleaseDetails = { id: string; slug: string; title: string; artistId: string };
type UploadLink = { uploadId: string; url: string };
type TrackSummary = { id: string; title: string; status: number };
type TrackUpload = { track: TrackSummary; uploadLink: string };

// TrackStatus is serialized as a number: Uploading, Converting, Ready, Failed.
const READY = 2;
const FAILED = 3;

export type PublishedRelease = { artist: ArtistSummary; release: ReleaseDetails; tracks: TrackSummary[] };

/** A Release form nobody has used. Override any field. */
export const releaseFormFactory = (overrides: Partial<{ title: string; type: string; releaseDate: string }> = {}) => ({
  title: faker.music.album(),
  type: 'Single',
  releaseDate: '2026-01-01',
  ...overrides,
});

/**
 * Creates an Artist managed by `managerToken`'s Member and a Published Release with the given audio fixtures,
 * the way a band would: cover and audio uploaded straight to blob storage, conversion awaited, submitted with a
 * Rights Declaration, approved by the Admin.
 */
export async function createPublishedRelease(
  apiRequest: ApiRequest,
  request: APIRequestContext,
  tokens: { manager: string; admin: string },
  options: { tracks?: string[]; form?: Parameters<typeof releaseFormFactory>[0] } = {},
): Promise<PublishedRelease> {
  const manager = asMember(tokens.manager);
  const fixtures = options.tracks ?? ['tone-60s.mp3'];

  const { body: artist } = await apiRequest<ArtistSummary>({
    method: 'POST',
    path: '/api/artists',
    body: { name: `${faker.music.artist()} ${faker.string.alphanumeric(4)}` },
    headers: manager,
  });

  const { body: release } = await apiRequest<ReleaseDetails>({
    method: 'POST',
    path: `/api/artists/${artist.id}/releases`,
    body: releaseFormFactory(options.form),
    headers: manager,
  });

  const { body: coverLink } = await apiRequest<UploadLink>({
    method: 'POST',
    path: `/api/releases/${release.id}/cover/upload-link`,
    headers: manager,
  });
  await putBlob(request, coverLink.url, 'cover-1000.png');
  await apiRequest({
    method: 'POST',
    path: `/api/releases/${release.id}/cover`,
    body: { uploadId: coverLink.uploadId },
    headers: manager,
  });

  for (const fixture of fixtures) {
    const { body: upload } = await apiRequest<TrackUpload>({
      method: 'POST',
      path: `/api/releases/${release.id}/tracks`,
      body: { fileName: fixture, size: statSync(fixturePath(fixture)).size },
      headers: manager,
    });
    await putBlob(request, upload.uploadLink, fixture);
    await apiRequest({
      method: 'POST',
      path: `/api/releases/${release.id}/tracks/${upload.track.id}/confirm`,
      headers: manager,
    });
  }

  const tracks = await recurse(
    async () =>
      (
        await apiRequest<TrackSummary[]>({
          method: 'GET',
          path: `/api/tracks?releaseId=${release.id}`,
          headers: manager,
        })
      ).body,
    (list) => list.length === fixtures.length && list.every((t) => t.status === READY || t.status === FAILED),
    { timeout: 60_000, interval: 500, error: `Tracks on ${release.title} did not finish converting` },
  );
  if (tracks.some((t) => t.status === FAILED)) {
    throw new Error(`Conversion failed for a Track on ${release.title}`);
  }

  await apiRequest({
    method: 'POST',
    path: `/api/releases/${release.id}/submit`,
    body: { rightsDeclared: true },
    headers: manager,
  });
  await apiRequest({
    method: 'POST',
    path: `/api/admin/review/releases/${release.id}/approve`,
    headers: asMember(tokens.admin),
  });

  return { artist, release, tracks };
}
