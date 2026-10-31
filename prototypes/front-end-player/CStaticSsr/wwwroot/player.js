// PROTOTYPE variant C: the whole player is plain JS living inside the data-permanent #player element.
// Pages are static HTML; they describe what can be played with data- attributes:
//   [data-queue]  JSON list of QueueItems on a container
//   [data-play]   button inside it, with data-index into that list
//   [data-track-id] rows that should light up while that Track plays
const root = document.getElementById('player');
const audio = root.querySelector('audio');
const el = name => root.querySelector(`[data-el=${name}]`);
const button = action => root.querySelector(`[data-action=${action}]`);

const bornAt = new Date().toISOString().slice(11, 23);
const interactiveAt = performance.now();
let queue = [];
let index = -1;
let navigations = 0;
const current = () => queue[index];

document.addEventListener('click', e => {
  const play = e.target.closest('[data-play]');
  if (!play) return;
  queue = JSON.parse(play.closest('[data-queue]').dataset.queue);
  index = Number(play.dataset.index);
  loadCurrent();
});

button('toggle').onclick = () => (audio.paused ? audio.play() : audio.pause());
button('next').onclick = next;
button('previous').onclick = () => {
  if (audio.currentTime > 3 || index <= 0) audio.currentTime = 0;
  else { index--; loadCurrent(); }
};
el('seek').onchange = e => (audio.currentTime = Number(e.target.value));

for (const type of ['timeupdate', 'play', 'pause', 'loadedmetadata']) audio.addEventListener(type, render);
audio.addEventListener('ended', next);

// Fires after every enhanced navigation and enhanced form post: the page is new, the player is not.
Blazor.addEventListener('enhancedload', () => { navigations++; render(); });

function next() {
  if (index + 1 >= queue.length) return;
  index++;
  loadCurrent();
}

function loadCurrent() {
  audio.src = current().audioUrl;
  audio.play().catch(() => {});
  render();
}

function render() {
  const c = current();
  for (const b of root.querySelectorAll('[data-action]')) b.disabled = !c;
  button('toggle').textContent = audio.paused ? '▶' : '⏸';
  el('now').innerHTML = c
    ? `<strong>${escape(c.title)}</strong>${c.isSingle ? '<span class="single">S</span>' : ''} · ${escape(c.artistName)} · <a href="releases/${c.releaseId}">${escape(c.releaseTitle)}</a>`
    : 'Nothing playing';
  el('time').textContent = `${fmt(audio.currentTime)} / ${fmt(audio.duration)}`;
  el('seek').max = audio.duration || 0;
  if (document.activeElement !== el('seek')) el('seek').value = audio.currentTime;
  el('diag').textContent =
    `player script running since ${interactiveAt.toFixed(0)} ms after page start · audio element born ${bornAt} · enhanced navigations: ${navigations}`;
  for (const row of document.querySelectorAll('[data-track-id]'))
    row.classList.toggle('now-playing', Number(row.dataset.trackId) === c?.trackId);
}

function fmt(s) {
  s = Number.isFinite(s) ? Math.floor(s) : 0;
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

function escape(text) {
  const span = document.createElement('span');
  span.textContent = text;
  return span.innerHTML;
}

render();
