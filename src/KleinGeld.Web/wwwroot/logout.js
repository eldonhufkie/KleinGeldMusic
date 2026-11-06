// Clears the player's saved state when the Member logs out, so the next person on this device doesn't find it.
// A script of its own because the Identity pages render statically, without the player, and have a Log out button too.
document.addEventListener("submit", event => {
    if (!event.target.action?.endsWith("/Account/Logout")) {
        return;
    }

    // On the way out rather than now: the player could still save once more before the page goes.
    window.addEventListener("pagehide", () => {
        try {
            localStorage.removeItem("kleingeld.player");
        } catch {
            // Storage is off, so nothing was saved.
        }
    }, { once: true });
});
