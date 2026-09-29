'use strict';

/*
 * DitenZonedTime — UTC instants ⇄ wall-clock time in a NAMED time zone, with nothing but the browser's Intl.
 *
 * ── WHY IT EXISTS (WP-UI-CALENDAR-VIEW-01) ─────────────────────────────────────────────────────────────────
 * The calendar shows a tenant's day in the TENANT's zone (the feed names it), not in whatever zone the reader's
 * laptop happens to be set to. FullCalendar can only do named zones with a plugin we do not ship, so the
 * calendar is run in 'UTC' mode and handed WALL-CLOCK times: 09:00 in Istanbul is given to it as "09:00Z". This
 * module is the only place that converts in either direction, so a DST rule lives once.
 *
 * ── THE RULES AT A DST EDGE ──────────────────────────────────────────────────────────────────────────────
 * • A wall time that does not exist (spring-forward gap, e.g. 02:30 in Berlin on the last Sunday of March)
 *   moves FORWARD by the gap — the same answer the engine's working-hours seam gives.
 * • A wall time that exists twice (autumn fall-back, e.g. 02:30 in Berlin on the last Sunday of October) is
 *   the EARLIER of the two instants.
 *
 * Wall-clock strings are "YYYY-MM-DDTHH:mm[:ss]" with no zone; a bare "YYYY-MM-DD" is midnight.
 */
(function (global) {
    const formatters = new Map();

    const formatterFor = (zone) => {
        if (!formatters.has(zone)) {
            formatters.set(zone, new Intl.DateTimeFormat('en-US', { timeZone: zone, timeZoneName: 'longOffset' }));
        }
        return formatters.get(zone);
    };

    /** True when the host's Intl knows the zone. */
    const isValidZone = (zone) => {
        if (!zone || typeof zone !== 'string') { return false; }
        try { formatterFor(zone); return true; } catch (_) { return false; }
    };

    /** The zone's offset from UTC, in minutes, at a given instant (ms since epoch). "GMT" alone is +0. */
    const offsetMinutes = (ms, zone) => {
        const name = (formatterFor(zone).formatToParts(new Date(ms)).find((p) => p.type === 'timeZoneName') || {}).value || '';
        const match = /GMT([+-])(\d{1,2})(?::?(\d{2}))?/.exec(name);
        if (!match) { return 0; }
        const minutes = Number(match[2]) * 60 + Number(match[3] || 0);
        return match[1] === '-' ? -minutes : minutes;
    };

    const pad = (n) => String(n).padStart(2, '0');

    /** Epoch ms of a wall-clock string read AS IF it were UTC (the arithmetic base for both directions). */
    const wallAsUtcMs = (wall) => {
        const text = String(wall || '').trim().replace(/Z$/, '');
        const normalized = /^\d{4}-\d{2}-\d{2}$/.test(text) ? text + 'T00:00:00' : text;
        const ms = Date.parse(normalized + 'Z');
        if (Number.isNaN(ms)) { throw new RangeError(`DitenZonedTime: not a wall-clock time: "${wall}"`); }
        return ms;
    };

    const toMs = (instant) => (instant instanceof Date ? instant.getTime() : typeof instant === 'number' ? instant : Date.parse(instant));

    /** UTC instant → wall clock in the zone, "YYYY-MM-DDTHH:mm:ss". */
    const toWall = (instant, zone) => {
        const ms = toMs(instant);
        return new Date(ms + offsetMinutes(ms, zone) * 60000).toISOString().slice(0, 19);
    };

    /** UTC instant → the local calendar day in the zone, "YYYY-MM-DD". */
    const localDate = (instant, zone) => toWall(instant, zone).slice(0, 10);

    /** Wall clock in the zone → UTC epoch ms, with the gap/overlap rules above. */
    const wallToUtcMs = (wall, zone) => {
        const base = wallAsUtcMs(wall);
        // The offsets in force half a day either side cover every real transition (none is longer than that).
        const before = offsetMinutes(base - 12 * 3600000, zone);
        const after = offsetMinutes(base + 12 * 3600000, zone);
        const valid = [before, after]
            .map((offset) => base - offset * 60000)
            .filter((candidate, index) => offsetMinutes(candidate, zone) === [before, after][index]);
        if (valid.length) { return Math.min(...valid); }        // overlap → the earlier instant
        return base - before * 60000;                            // gap → forward by the gap
    };

    /** Wall clock in the zone → UTC ISO "YYYY-MM-DDTHH:mm:ss.sssZ". */
    const toUtcIso = (wall, zone) => new Date(wallToUtcMs(wall, zone)).toISOString();

    /**
     * Wall clock in the zone → the same wall clock with the zone's offset attached, "YYYY-MM-DDTHH:mm:ss+03:00".
     * Used for a day plan: the day stays the day the reader chose whatever the server's own zone is.
     */
    const toOffsetIso = (wall, zone) => {
        const ms = wallToUtcMs(wall, zone);
        const offset = offsetMinutes(ms, zone);
        const sign = offset < 0 ? '-' : '+';
        const abs = Math.abs(offset);
        return toWall(ms, zone) + sign + pad(Math.floor(abs / 60)) + ':' + pad(abs % 60);
    };

    /* ── FullCalendar's 'UTC' mode carries wall clock as "…Z" ─────────────────────────────────────────── */

    /** UTC instant → the string handed to FullCalendar (wall clock dressed as UTC). */
    const toCalendar = (instant, zone) => toWall(instant, zone) + 'Z';

    /** A Date FullCalendar hands back in 'UTC' mode → the real UTC ISO instant in the zone. */
    const fromCalendar = (date, zone) => toUtcIso(new Date(toMs(date)).toISOString().slice(0, 19), zone);

    global.DitenZonedTime = {
        isValidZone,
        offsetMinutes,
        toWall,
        localDate,
        wallToUtcMs,
        toUtcIso,
        toOffsetIso,
        toCalendar,
        fromCalendar
    };
})(typeof window !== 'undefined' ? window : globalThis);
