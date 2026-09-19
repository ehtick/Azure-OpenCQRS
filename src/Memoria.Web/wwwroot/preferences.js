// The rows-per-page choice, remembered in this browser.
//
// How big a table's pages are is said in the address: every link, every sort arrow and every filter
// on those pages carries the size along with it, so the server needs no memory of the reader and a
// link means the same thing to whoever opens it. What is missing is only what to do when the
// address says nothing — the answer there is always ten, and a reader who works at fifty asks for
// fifty again on every table they open.
//
// So this writes the pick down when it is made, and puts it back into the address on arrival at a
// table that was not asked for at a size. Nothing else moves: the page is still rendered from the
// address, and the server still decides whether a size is one it offers.

// One key for the whole application. Both places the size can be picked — the picker under a table
// and the one on the settings page — are the same preference under two labels, not two settings
// that could disagree.
const key = "memoria.rows-per-page";

// Whether the note about approximate ordering has been waved away for good. Its own key rather than
// a field beside the size, so one preference cannot be lost by writing the other.
const orderingKey = "memoria.hide-ordering-notice";

// How often a table asks its store again, in seconds. Its own key, like the two above: one
// preference lost or written should not take another with it. Absent, or zero, means it does not —
// which is the state the server renders every time, because this is the browser's own choice and
// nothing is ever sent about it.
const refreshKey = "memoria.auto-refresh";

// Which theme was chosen, if one was. Absent means nothing was chosen and the operating system is
// answering, which is a third state rather than a synonym for light — so it is stored as absent
// rather than written down as a guess. The value is read a second time by the small script in the
// head, which is what puts the theme on before the page paints; this key is the contract between
// the two, and renaming it here means renaming it there.
const themeKey = "memoria.theme";

// Storage can be missing or refused outright: a private window, a browser set to block site data,
// an embedded view. A reader who cannot be remembered still gets a working table at the size the
// address asks for, so both ways in swallow the refusal rather than letting it reach the page.
function stored() {
    try {
        return window.localStorage.getItem(key);
    } catch {
        return null;
    }
}

// Written down twice: in storage, which the script reads back, and in a cookie under the same
// name, which the server reads. The cookie is what lets a table arrive already at the remembered
// size — without it the first render was at the default and the reload below ran every query a
// second time. The reload stays for a browser that remembers in storage but not in cookies.
function remember(size) {
    try {
        window.localStorage.setItem(key, size);
    } catch {
    }
    try {
        document.cookie = `${key}=${encodeURIComponent(size)}; path=/; max-age=31536000; samesite=lax`;
    } catch {
    }
}

// The ordering note is hidden only when it was asked to be. Storage that cannot be read answers
// "no", so a reader who cannot be remembered sees the note rather than silently losing it.
function noticeHidden() {
    try {
        return window.localStorage.getItem(orderingKey) === "true";
    } catch {
        return false;
    }
}

function rememberNotice(hidden) {
    try {
        if (hidden) {
            window.localStorage.setItem(orderingKey, "true");
        } else {
            window.localStorage.removeItem(orderingKey);
        }
    } catch {
    }
}

function hideNotices() {
    for (const notice of document.querySelectorAll('[data-notice="ordering"]')) {
        notice.hidden = true;
    }
}

// Closing and hiding are different answers to the same note. Close takes away the one on this page
// and nothing more — the note is true, and it comes back on the next page that has to say it. Hide
// is the standing answer, and is written down.
document.addEventListener("click", event => {
    const control = event.target;

    if (!(control instanceof HTMLElement)) {
        return;
    }

    if (control.closest("[data-notice-close]")) {
        control.closest('[data-notice="ordering"]').hidden = true;
        return;
    }

    if (control.closest("[data-notice-hide]")) {
        rememberNotice(true);
        hideNotices();
    }
});

// The x beside what the last press did. It is a link to this address without the message, which is
// what happens with no script; here the box is taken away in place and that address put in the bar
// instead, so the page neither scrolls nor flashes and a reload still leaves the message gone. Heard
// while the click is on its way down, because Blazor's own link handling listens on the way back up
// and would otherwise navigate first; it leaves alone a click already answered.
document.addEventListener("click", event => {
    const control = event.target instanceof Element ? event.target.closest("[data-dismiss]") : null;

    if (!control || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
        return;
    }

    event.preventDefault();
    control.closest("[data-dismissible]")?.remove();
    history.replaceState(history.state, "", control.href);
}, true);

// The pick, wherever it was made. The picker under a table submits its form and the size lands in
// the address; the one on the settings page has no form to submit and this is all that happens. One
// listener on the document rather than one per select: the selects come and go as pages are swapped
// in, and the document does not.
document.addEventListener("change", event => {
    const select = event.target;

    if (!(select instanceof HTMLSelectElement)) {
        return;
    }

    if (select.dataset.preference === "rows-per-page" || select.closest("form.page-size")) {
        remember(select.value);
    }
});

// The settings page's own switch for the ordering note, which is the way back once the note has
// been hidden: the note is what offers to hide it, so hiding it takes the offer away with it.
document.addEventListener("change", event => {
    const box = event.target;

    if (box instanceof HTMLInputElement && box.dataset.preference === "hide-ordering-notice") {
        rememberNotice(box.checked);

        if (box.checked) {
            hideNotices();
        }
    }
});

function storedTheme() {
    try {
        return window.localStorage.getItem(themeKey);
    } catch {
        return null;
    }
}

// Following the operating system is the absence of a choice rather than a choice of its own, so
// picking it takes the value away instead of writing "system" down. The head script and the stamp
// below both read this key as "was anything chosen at all", and a third word stored here would
// have had to be understood in two more places to mean nothing.
function rememberTheme(theme) {
    try {
        if (theme === "dark" || theme === "light") {
            window.localStorage.setItem(themeKey, theme);
        } else {
            window.localStorage.removeItem(themeKey);
        }
    } catch {
    }
}

// Puts the chosen theme back on the root if it has gone missing, and takes it off when nothing is
// chosen any more. Enhanced navigation is why it goes missing: Blazor swaps a page in by diffing
// the new document against this one, and the document the server sends has no data-theme on it,
// because the choice is the browser's and the server has never heard of it. So the attribute is
// stripped on the way through, and the page arrives in whatever theme the operating system asks
// for. Nothing else writes the attribute, which is what makes removing it safe: with nothing
// stored there is nothing it could be saying.
function stampTheme() {
    const choice = storedTheme();
    const root = document.documentElement;

    if (choice !== "dark" && choice !== "light") {
        if (root.hasAttribute("data-theme")) {
            root.removeAttribute("data-theme");
        }

        return;
    }

    if (root.getAttribute("data-theme") !== choice) {
        root.setAttribute("data-theme", choice);
    }
}

// The row is rendered hidden and shown here, so the select never appears where nothing can drive
// it — and the page under it still follows the operating system, which is what the select would
// have said anyway.
function applyTheme() {
    stampTheme();

    const choice = storedTheme();
    const value = choice === "dark" || choice === "light" ? choice : "system";

    for (const select of document.querySelectorAll('select[data-preference="theme"]')) {
        select.value = value;
    }

    for (const row of document.querySelectorAll('[data-preference-row="theme"]')) {
        row.hidden = false;
    }
}

// Watching the attribute rather than only putting it back on enhancedload, because the strip
// happens after that event rather than before it — re-stamping there would be undone a moment
// later. An observer catches the removal whenever it comes, and its callback runs before the
// browser paints, so the theme does not flicker on the way between pages. The guard inside
// stampTheme is what stops this from answering its own write.
new MutationObserver(stampTheme).observe(document.documentElement, {
    attributes: true,
    attributeFilter: ["data-theme"]
});

// The theme changes as the select does, with nothing to submit: the choice is this browser's and
// the server is never told. Written down and put on the root in the same breath, so the page
// answers the pick rather than the next navigation.
document.addEventListener("change", event => {
    const select = event.target;

    if (!(select instanceof HTMLSelectElement) || select.dataset.preference !== "theme") {
        return;
    }

    rememberTheme(select.value);
    stampTheme();
});

// A table is read while its store is still being written to, so every page that lists what one
// holds offers two ways of asking it again: a link that asks now, and a picker that keeps asking.
//
// The link needs nothing from here — it points at the address the page is already at, and the whole
// of what is shown is in that address, so following it draws this same table from the store again.
// What is here is the timer, which is nothing without scripting, and the one thing the link cannot
// say for itself: that a refresh is not a place to go back to.

function storedRefresh() {
    try {
        return window.localStorage.getItem(refreshKey);
    } catch {
        return null;
    }
}

// Off is the absence of an interval rather than one of them, so choosing it takes the value away
// instead of writing a zero down — the same shape as following the operating system for the theme.
function rememberRefresh(seconds) {
    try {
        if (seconds) {
            window.localStorage.setItem(refreshKey, seconds);
        } else {
            window.localStorage.removeItem(refreshKey);
        }
    } catch {
    }
}

// Through Blazor where it is running, which swaps the table in without reloading the document or
// losing the place on the page, and replaces this page in the history rather than pushing another:
// Back means the page before this table, never this table a minute ago. A plain reload is the
// answer where Blazor is not running or will not say.
function refreshNow(address) {
    try {
        if (window.Blazor && typeof Blazor.navigateTo === "function") {
            Blazor.navigateTo(address ?? location.href, { replaceHistoryEntry: true });
            return;
        }
    } catch {
    }

    location.reload();
}

let refreshTimer = null;

// A tick that fell while the tab was out of sight, so the table can be caught up the moment it is
// looked at again rather than at the end of another whole interval — which at an hour would be no
// better than not refreshing at all.
let missedRefresh = false;

// One timer at a time, set again on every page. It is cleared first because this runs on every
// arrival, including the arrival this timer itself asked for: the interval is the gap between one
// table landing and the next being asked for, rather than a drumbeat a slow store falls behind.
//
// Only where there is a table to ask about again, which the refresh link is the mark of — not the
// picker, which the preferences page carries too, and which would have that page reloading itself
// every five seconds while a reader was trying to change a setting on it. Every page arrives
// through here, so a page with no refresh on it — the home page, a type listing, the settings —
// must not inherit the timer from the table the reader came from either.
function scheduleRefresh() {
    if (refreshTimer !== null) {
        window.clearInterval(refreshTimer);
        refreshTimer = null;
    }

    if (!document.querySelector("[data-refresh]")) {
        return;
    }

    const seconds = Number(storedRefresh());

    if (!Number.isFinite(seconds) || seconds <= 0) {
        return;
    }

    refreshTimer = window.setInterval(() => {
        // Nothing is asked of the store while nobody is looking at the answer: a tab left open on a
        // table all afternoon would otherwise query it all afternoon.
        if (document.visibilityState === "hidden") {
            missedRefresh = true;
            return;
        }

        refreshNow();
    }, seconds * 1000);
}

// The catch-up, on the way back to a tab that was left. Only while a timer is still running: the
// reader may have turned the refresh off, or walked to a page with no table, while they were away.
document.addEventListener("visibilitychange", () => {
    if (document.visibilityState !== "visible") {
        return;
    }

    const missed = missedRefresh;
    missedRefresh = false;

    if (missed && refreshTimer !== null) {
        refreshNow();
    }
});

// The pick, which is this browser's and is never sent anywhere. Written down and acted on in the
// same breath, so the table starts refreshing at the interval just chosen rather than at whatever
// the reader does next.
document.addEventListener("change", event => {
    const select = event.target;

    if (select instanceof HTMLSelectElement && select.dataset.preference === "auto-refresh") {
        rememberRefresh(select.value);
        scheduleRefresh();
    }
});

// Taken in the capture phase rather than the bubble one, because Blazor listens for clicks on the
// document too and whichever of the two was added first would otherwise win. Capture runs before
// either, and preventing the default is what tells Blazor's own handler to leave this one alone.
// The modified clicks are left to the browser: a refresh opened in a new tab is a new tab on this
// table, which is a reasonable thing to have asked for.
document.addEventListener("click", event => {
    const control = event.target;

    if (!(control instanceof HTMLElement)) {
        return;
    }

    const link = control.closest("[data-refresh]");

    if (!link || event.defaultPrevented || event.button !== 0) {
        return;
    }

    if (event.ctrlKey || event.shiftKey || event.altKey || event.metaKey) {
        return;
    }

    if (!window.Blazor || typeof Blazor.navigateTo !== "function") {
        return;
    }

    event.preventDefault();
    refreshNow(link.href);
}, true);

// The picker is rendered hidden and shown here, so a timer is never offered where nothing can run
// one — the same bargain the copy button on a payload makes. What was picked is put back from
// storage, because the server renders Off every time: it has never heard of this preference.
function applyRefresh() {
    const chosen = storedRefresh() ?? "";

    for (const select of document.querySelectorAll('select[data-preference="auto-refresh"]')) {
        // Left showing Off when what was stored is not on offer, so the picker says what is
        // actually going to happen rather than an interval nothing will use.
        if ([...select.options].some(option => option.value === chosen)) {
            select.value = chosen;
        }
    }

    for (const row of document.querySelectorAll('[data-preference-row="auto-refresh"]')) {
        row.hidden = false;
    }

    scheduleRefresh();
}

// The copy button on the Json tab. Rendered hidden — it is the one control on these pages that does
// nothing without script — and shown here only where the clipboard can be written to, which the
// browser allows in a secure context alone: localhost or https. Shown as a strip rather than one
// button at a time, so the card has no strip with nothing in it when the clipboard is refused.
function showCopyButtons() {
    const canCopy = Boolean(navigator.clipboard && navigator.clipboard.writeText);

    for (const tools of document.querySelectorAll("[data-json-tools]")) {
        tools.hidden = !canCopy;
    }
}

// What is copied is the text of the box, so a reader gets the payload laid out as they see it
// rather than the one line the store holds. The button says what happened for a moment and then
// goes back to offering, so it can be pressed again; the label it goes back to is remembered on the
// button the first time, so a second press during that moment does not remember "Copied".
document.addEventListener("click", async event => {
    const control = event.target;

    if (!(control instanceof HTMLElement)) {
        return;
    }

    const button = control.closest("[data-copy-json]");

    if (!button) {
        return;
    }

    const source = button.closest(".json-view")?.querySelector("pre.json");

    if (!source) {
        return;
    }

    button.dataset.label ??= button.textContent;

    try {
        await navigator.clipboard.writeText(source.textContent);
        button.textContent = "Copied";
    } catch {
        button.textContent = "Could not copy";
    }

    window.setTimeout(() => {
        button.textContent = button.dataset.label;
    }, 2000);
});

function apply() {
    // First, and before the early return below: the theme is the whole application's, and a reader
    // who never picked a rows-per-page size still has one.
    applyTheme();

    // Before the early return below, for the same reason: a reader who never picked a size can
    // still be looking at a payload.
    showCopyButtons();

    // Before it too, and for the third time for the same reason: the timer is this browser's, a
    // table swapped in has none running until this sets one, and a reader who never picked a size
    // may well have picked an interval.
    applyRefresh();

    // Both settings are put back on every page, and this one first: the note is rendered for
    // everyone, so a reader who hid it should not watch it go. Before the early return below,
    // because a reader who never picked a size may still have hidden the note.
    const hidden = noticeHidden();

    if (hidden) {
        hideNotices();
    }

    for (const box of document.querySelectorAll('input[data-preference="hide-ordering-notice"]')) {
        box.checked = hidden;
    }

    const size = stored();

    if (!size) {
        return;
    }

    const picker = document.querySelector('form.page-size select[name="size"]');
    const url = new URL(window.location.href);

    // Only a page that offers a size, and only when the address has not already named one: a link
    // that says how big its page is means it, whether it came from the pager, a bookmark or someone
    // else. The stored size is checked against what this picker offers before it is used — the
    // server would fall back to the default for anything else, and this way that is one round trip
    // that does not happen.
    if (picker &&
        !url.searchParams.has("size") &&
        picker.value !== size &&
        [...picker.options].some(option => option.value === size)) {
        url.searchParams.set("size", size);

        // Replaced rather than pushed: the page at the default size is not one the reader asked
        // for, and going back should leave the table rather than step through the redirect into it.
        window.location.replace(url);
        return;
    }

    // The settings picker is the preference itself rather than a size beside a table, so it is not
    // in the address at all and is filled in from what was stored. Left alone when what was stored
    // is not on offer, so the select shows a size rather than nothing.
    for (const select of document.querySelectorAll('select[data-preference="rows-per-page"]')) {
        if ([...select.options].some(option => option.value === size)) {
            select.value = size;
        }
    }
}

apply();

// Enhanced navigation swaps a page's contents in without loading a document, so this file does not
// run again — and moving from one table to another is exactly when the size has to be put back.
// Blazor says when it has swapped; that is the other moment a table appears.
if (window.Blazor) {
    Blazor.addEventListener("enhancedload", apply);
}
