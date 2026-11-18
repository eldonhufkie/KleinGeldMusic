import { faker } from '@faker-js/faker';
import { asMember } from '../environment';
import type { ApiRequest } from './release-factory';

type PlaylistSummary = { id: string; name: string; slug: string };
export type PlaylistTrack = { entryId: string; trackId: string | null; title: string; isAvailable: boolean };
export type PlaylistDetails = { id: string; name: string; slug: string; tracks: PlaylistTrack[] };

/** Creates a Playlist owned by `token`'s Member holding these Tracks, in this order. */
export async function createPlaylist(
  apiRequest: ApiRequest,
  token: string,
  trackIds: string[],
  name = `${faker.word.adjective()} ${faker.word.noun()}`,
): Promise<PlaylistDetails> {
  const headers = asMember(token);
  const { body: playlist } = await apiRequest<PlaylistSummary>({
    method: 'POST',
    path: '/api/playlists',
    body: { name },
    headers,
  });
  for (const trackId of trackIds) {
    await apiRequest({ method: 'POST', path: `/api/playlists/${playlist.id}/tracks`, body: { trackId }, headers });
  }
  return readPlaylist(apiRequest, token, playlist.id);
}

/** The Playlist as the API stores it now. */
export async function readPlaylist(apiRequest: ApiRequest, token: string, id: string): Promise<PlaylistDetails> {
  const { body } = await apiRequest<PlaylistDetails>({
    method: 'GET',
    path: `/api/playlists/${id}`,
    headers: asMember(token),
  });
  return body;
}
