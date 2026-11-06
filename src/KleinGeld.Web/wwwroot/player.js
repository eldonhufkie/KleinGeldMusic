// Drives the one <audio> element in the player bar and reports back what it does.
let audio;
let listener;

function report() {
    const duration = Number.isFinite(audio.duration) ? audio.duration : 0;
    listener.invokeMethodAsync("Report", audio.currentTime, duration, !audio.paused && !audio.ended);
}

export function attach(element, dotNet) {
    audio = element;
    listener = dotNet;
    for (const event of ["timeupdate", "durationchange", "play", "pause", "ended", "seeked"]) {
        audio.addEventListener(event, report);
    }
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
