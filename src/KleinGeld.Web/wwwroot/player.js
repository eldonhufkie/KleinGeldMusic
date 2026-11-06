// Drives the one <audio> element in the player bar and reports back what it does.
let audio;
let listener;

function report() {
    const duration = Number.isFinite(audio.duration) ? audio.duration : 0;
    const isPlaying = !audio.paused && !audio.ended;
    listener.invokeMethodAsync("Report", audio.currentTime, duration, isPlaying);
    showProgress(duration, isPlaying);
}

function showProgress(duration, isPlaying) {
    if (!("mediaSession" in navigator)) {
        return;
    }

    navigator.mediaSession.playbackState = isPlaying ? "playing" : "paused";
    if (duration > 0) {
        navigator.mediaSession.setPositionState({
            duration,
            playbackRate: audio.playbackRate,
            position: Math.min(audio.currentTime, duration),
        });
    }
}

// The names are the MediaAction values in IAudioElement.cs.
function listenForMediaKeys() {
    if (!("mediaSession" in navigator)) {
        return;
    }

    const handlers = {
        play: () => listener.invokeMethodAsync("Command", "Play", 0),
        pause: () => listener.invokeMethodAsync("Command", "Pause", 0),
        nexttrack: () => listener.invokeMethodAsync("Command", "Next", 0),
        previoustrack: () => listener.invokeMethodAsync("Command", "Previous", 0),
        seekto: details => listener.invokeMethodAsync("Command", "Seek", details.seekTime),
    };
    for (const [action, handler] of Object.entries(handlers)) {
        try {
            navigator.mediaSession.setActionHandler(action, handler);
        } catch {
            // This browser doesn't know the action; the others still work.
        }
    }
}

export function attach(element, dotNet) {
    audio = element;
    listener = dotNet;
    for (const event of ["timeupdate", "durationchange", "play", "pause", "ended", "seeked"]) {
        audio.addEventListener(event, report);
    }
    audio.addEventListener("ended", () => listener.invokeMethodAsync("End"));
    listenForMediaKeys();
}

export function showMetadata(title, artist, smallCover, largeCover) {
    if (!("mediaSession" in navigator)) {
        return;
    }

    const artwork = smallCover
        ? [{ src: smallCover, sizes: "300x300" }, { src: largeCover, sizes: "1000x1000" }]
        : [];
    navigator.mediaSession.metadata = new MediaMetadata({ title, artist, artwork });
}

export async function play(source) {
    audio.src = source;
    await resume();
}

export async function resume() {
    try {
        await audio.play();
    } catch {
        // Interrupted by the next Track, or the browser refused: the element's state says what happened.
        report();
    }
}

export function pause() {
    audio.pause();
}

export function seek(seconds) {
    audio.currentTime = seconds;
}
