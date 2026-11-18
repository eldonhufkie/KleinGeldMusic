// Drag-to-reorder for the Track list, by a handle, with mouse, touch or the arrow keys. The DOM belongs to Blazor, so a
// drag only moves rows for show: on drop they go back and .NET is told the new order, which it renders itself.
const rowsOf = list => [...list.querySelectorAll("[data-track-id]")];
const idsOf = rows => rows.map(row => row.dataset.trackId);

export function attach(list, dotNet) {
    list.addEventListener("pointerdown", event => {
        const handle = event.target.closest(".track-handle");
        if (!handle) {
            return;
        }
        event.preventDefault();
        const row = handle.closest("[data-track-id]");
        const original = rowsOf(list);
        const pointerId = event.pointerId;
        row.classList.add("track-dragging");

        // Listen on the document, not by capturing the pointer on the handle: moving the row takes the handle out of
        // the DOM, which releases any capture, and the drop would then never arrive.
        const move = moved => {
            if (moved.pointerId !== pointerId) {
                return;
            }
            const over = document.elementFromPoint(moved.clientX, moved.clientY)?.closest("[data-track-id]");
            if (!over || over === row || !list.contains(over)) {
                return;
            }
            const rows = rowsOf(list);
            over.parentNode.insertBefore(row, rows.indexOf(row) < rows.indexOf(over) ? over.nextSibling : over);
        };
        const finish = ended => {
            if (ended.pointerId !== pointerId) {
                return;
            }
            document.removeEventListener("pointermove", move);
            document.removeEventListener("pointerup", finish);
            document.removeEventListener("pointercancel", finish);
            row.classList.remove("track-dragging");

            const order = idsOf(rowsOf(list));
            original.forEach(r => r.parentNode.appendChild(r));
            if (ended.type === "pointerup" && order.join() !== idsOf(original).join()) {
                dotNet.invokeMethodAsync("Reorder", order);
            }
        };
        document.addEventListener("pointermove", move);
        document.addEventListener("pointerup", finish);
        document.addEventListener("pointercancel", finish);
    });

    list.addEventListener("keydown", event => {
        const step = { ArrowUp: -1, ArrowDown: 1 }[event.key];
        const handle = event.target.closest(".track-handle");
        if (!step || !handle) {
            return;
        }
        event.preventDefault();
        const order = idsOf(rowsOf(list));
        const from = order.indexOf(handle.closest("[data-track-id]").dataset.trackId);
        const to = from + step;
        if (to >= 0 && to < order.length) {
            [order[from], order[to]] = [order[to], order[from]];
            dotNet.invokeMethodAsync("Reorder", order);
        }
    });
}
