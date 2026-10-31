# PROTOTYPE: front-end player (throwaway, never merge)

Answers [Front end: Blazor WebAssembly vs static SSR with a persistent player](../../.scratch/music-service/issues/06-front-end-prototype.md):
which approach keeps the audio playing, uninterrupted, while a Member moves between pages?

The same tiny app is built twice. Both have the same pages (Artists → Artist → Release, My Playlist), sample data and generated Tracks.
Each Track is a tone with a note every second and a double beep every 10 s, so a restart or a stutter is easy to hear.

| | Run | Open |
|---|---|---|
| **A** Blazor WebAssembly, global interactivity, player in `MainLayout` | `dotnet run --project prototypes/front-end-player/AWasm/AWasm` | http://localhost:5101 |
| **C** static SSR + enhanced navigation, plain-JS player in a `data-permanent` element | `dotnet run --project prototypes/front-end-player/CStaticSsr` | http://localhost:5103 |

The grey line under each player bar shows the state that matters: when the player became usable, when the `<audio>` element was created (it must never change while you browse), and how many in-page navigations have happened.

## Things to try

1. Play a Track, then click around: Artist links, header links, the Release link in the player bar, browser Back/Forward. Listen for a restart or a gap.
2. Press **+ Playlist** while music plays. In A this is a C# click handler. In C it is a real form post, enhanced by `data-enhance`; remove that attribute and the post reloads the page and stops the music.
3. Hard-reload a Release page (Ctrl+Shift+R). In A, see how long the red "prerendered" badge stays before the buttons work. C works almost at once.
4. Read the player code, which is what you'd live with:
   - A: [PlayerService.cs](AWasm/AWasm.Client/PlayerService.cs), [PlayerBar.razor](AWasm/AWasm.Client/Layout/PlayerBar.razor), [player.js](AWasm/AWasm.Client/wwwroot/player.js), and a page that highlights the current Track: [ReleasePage.razor](AWasm/AWasm.Client/Pages/ReleasePage.razor).
   - C: [player.js](CStaticSsr/wwwroot/player.js), [MainLayout.razor](CStaticSsr/Components/Layout/MainLayout.razor), [ReleasePage.razor](CStaticSsr/Components/Pages/ReleasePage.razor).

## Shortcuts taken (prototype only)

- No auth. Audio is generated WAV served with range requests.
- The Playlist is in browser memory in A (lost on reload) and a single server-side list shared by everyone in C.
- No tests, no error handling, CSS shared by copying `Shared/app.css`.
