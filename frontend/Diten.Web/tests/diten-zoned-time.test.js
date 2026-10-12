const { loadScript } = require("./load-script");

/*
 * WP-UI-CALENDAR-VIEW-01 (B) — DitenZonedTime: the calendar's only UTC ⇄ tenant wall-clock conversion.
 * Istanbul has no DST (UTC+3 all year); Berlin and New York have both transitions, which is where a naive
 * "offset at the wall time" conversion is off by an hour.
 */
beforeAll(() => {
  delete global.DitenZonedTime;
  loadScript("wwwroot/assets/js/shared/diten-zoned-time.js");
});
const Z = () => global.DitenZonedTime;

describe("Europe/Istanbul (UTC+3, no DST)", () => {
  const zone = "Europe/Istanbul";

  it("shows 06:00Z as 09:00 and converts 09:00 back to 06:00Z", () => {
    expect(Z().toWall("2026-10-05T06:00:00Z", zone)).toBe("2026-10-05T09:00:00");
    expect(Z().toUtcIso("2026-10-05T09:00", zone)).toBe("2026-10-05T06:00:00.000Z");
  });

  it("puts 22:00Z on the NEXT local day", () => {
    expect(Z().localDate("2026-10-05T22:00:00Z", zone)).toBe("2026-10-06");
  });

  it("dresses a day plan with the local offset so its date survives any server zone", () => {
    expect(Z().toOffsetIso("2026-10-06", zone)).toBe("2026-10-06T00:00:00+03:00");
  });

  it("round-trips through FullCalendar's UTC mode", () => {
    const shown = Z().toCalendar("2026-10-05T07:15:00Z", zone);
    expect(shown).toBe("2026-10-05T10:15:00Z");
    expect(Z().fromCalendar(new Date(shown), zone)).toBe("2026-10-05T07:15:00.000Z");
  });
});

describe("Europe/Berlin (CET/CEST)", () => {
  const zone = "Europe/Berlin";

  it("uses +1 in winter and +2 in summer", () => {
    expect(Z().toUtcIso("2026-01-15T10:00", zone)).toBe("2026-01-15T09:00:00.000Z");
    expect(Z().toUtcIso("2026-07-15T10:00", zone)).toBe("2026-07-15T08:00:00.000Z");
  });

  it("spring forward (2026-03-29): 02:30 does not exist and moves forward to 03:30 CEST", () => {
    expect(Z().toUtcIso("2026-03-29T02:30", zone)).toBe("2026-03-29T01:30:00.000Z");
    expect(Z().toWall("2026-03-29T01:30:00Z", zone)).toBe("2026-03-29T03:30:00");
    // either side of the gap is exact
    expect(Z().toUtcIso("2026-03-29T01:59", zone)).toBe("2026-03-29T00:59:00.000Z");
    expect(Z().toUtcIso("2026-03-29T03:00", zone)).toBe("2026-03-29T01:00:00.000Z");
  });

  it("fall back (2026-10-25): 02:30 exists twice and the EARLIER instant is taken", () => {
    expect(Z().toUtcIso("2026-10-25T02:30", zone)).toBe("2026-10-25T00:30:00.000Z");
    expect(Z().toWall("2026-10-25T00:30:00Z", zone)).toBe("2026-10-25T02:30:00");
    expect(Z().toWall("2026-10-25T01:30:00Z", zone)).toBe("2026-10-25T02:30:00");
  });

  it("a day plan on a transition day carries that day's midnight offset", () => {
    expect(Z().toOffsetIso("2026-03-29", zone)).toBe("2026-03-29T00:00:00+01:00");
    expect(Z().toOffsetIso("2026-10-25", zone)).toBe("2026-10-25T00:00:00+02:00");
  });

  it("round-trips every quarter hour of both transition days", () => {
    ["2026-03-29", "2026-10-25"].forEach((day) => {
      for (let m = 0; m < 24 * 60; m += 15) {
        const wall = `${day}T${String(Math.floor(m / 60)).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}:00`;
        const back = Z().toWall(Z().toUtcIso(wall, zone), zone);
        const inGap = day === "2026-03-29" && m >= 120 && m < 180;
        if (!inGap) { expect(back, wall).toBe(wall); }
      }
    });
  });
});

describe("America/New_York (EST/EDT)", () => {
  const zone = "America/New_York";

  it("spring forward (2026-03-08) and fall back (2026-11-01)", () => {
    expect(Z().toUtcIso("2026-03-08T02:30", zone)).toBe("2026-03-08T07:30:00.000Z"); // → 03:30 EDT
    expect(Z().toUtcIso("2026-11-01T01:30", zone)).toBe("2026-11-01T05:30:00.000Z"); // earlier (EDT)
    expect(Z().localDate("2026-10-06T02:00:00Z", zone)).toBe("2026-10-05");
  });
});

describe("guards", () => {
  it("knows a real zone from a made-up one", () => {
    expect(Z().isValidZone("Europe/Istanbul")).toBe(true);
    expect(Z().isValidZone("Mars/Olympus")).toBe(false);
  });

  it("refuses a string that is not a wall-clock time instead of guessing", () => {
    expect(() => Z().toUtcIso("yarın", "Europe/Istanbul")).toThrow(RangeError);
  });
});
