// PROTOTYPE variant A: thin wrapper around the one <audio> element; all state lives in PlayerService (C#).
let audio, dotnet;

export function attach(el, ref) {
  audio = el;
  dotnet = ref;
  audio.dataset.bornAt ??= new Date().toISOString().slice(11, 23);
  audio.addEventListener('timeupdate', () => dotnet.invokeMethodAsync('OnTime', audio.currentTime, audio.duration || 0));
  audio.addEventListener('play', () => dotnet.invokeMethodAsync('OnPlayState', true));
  audio.addEventListener('pause', () => dotnet.invokeMethodAsync('OnPlayState', false));
  audio.addEventListener('ended', () => dotnet.invokeMethodAsync('OnEnded'));
  return { bornAt: audio.dataset.bornAt, nowMs: performance.now() };
}

export function load(url) { audio.src = url; audio.play().catch(() => {}); }
export function play() { audio.play().catch(() => {}); }
export function pause() { audio.pause(); }
export function seek(seconds) { audio.currentTime = seconds; }
