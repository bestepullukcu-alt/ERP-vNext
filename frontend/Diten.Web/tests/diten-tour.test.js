const { loadScript } = require("./load-script");

/*
 * WP-UX-COMMON-01 (BL-495) — the tour layer (window.DitenTour). Pages call this file, never the library.
 *
 *   · no library on the page → "no tour" everywhere, never an exception (the state until the vendored Driver.js
 *     is approved and added);
 *   · text handed to the library is ESCAPED — the library writes it with innerHTML;
 *   · "seen" is per tour version, in localStorage, and storage that throws is not an error;
 *   · only at md width and up; Arabic gets the RTL popover class; keyboard control is on.
 */

const STEPS = [
  { target: "list", titleKey: "Tour.T.Step1.Title", textKey: "Tour.T.Step1.Text" },
  { target: "missing", titleKey: "Tour.T.Step2.Title", textKey: "Tour.T.Step2.Text" }
];

let calls;
const fakeLibrary = () => {
  calls = [];
  window.driver = { js: { driver: (config) => { calls.push(config); return { drive: () => calls.push("drive") }; } } };
};

const boot = ({ width = 1280, strings = {} } = {}) => {
  delete window.DitenTour;
  delete window.driver;
  document.documentElement.removeAttribute("dir");
  document.body.innerHTML = `<script id="diten-tour-l10n" type="application/json">${JSON.stringify(strings)}</script>
    <div data-tour="list"></div>`;
  Object.defineProperty(window, "innerWidth", { value: width, configurable: true, writable: true });
  loadScript("wwwroot/assets/js/shared/diten-tour.js");
};

// The test environment's own localStorage depends on the jsdom URL; a plain in-memory one makes the measurement
// independent of it. `blocked` is a browser whose storage throws on every access (private mode, blocked site data).
const installStorage = ({ blocked = false } = {}) => {
  const data = new Map();
  const memory = {
    getItem: (key) => (data.has(key) ? data.get(key) : null),
    setItem: (key, value) => { data.set(key, String(value)); },
    removeItem: (key) => { data.delete(key); },
    clear: () => data.clear()
  };
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    get: () => { if (blocked) { throw new Error("SecurityError"); } return memory; }
  });
  return memory;
};

beforeEach(() => { installStorage(); });

afterEach(() => {
  delete window.driver;
  vi.restoreAllMocks();
});

describe("DitenTour without the library", () => {
  it("answers 'no tour' and never throws or logs", () => {
    boot();
    const errors = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(window.DitenTour.register("T", { version: 1, steps: STEPS })).toBe(true);
    expect(window.DitenTour.isAvailable("T")).toBe(false);
    expect(window.DitenTour.start("T")).toBe(false);
    expect(window.DitenTour.isCompleted("T")).toBe(false);
    expect(errors).not.toHaveBeenCalled();
  });
});

describe("DitenTour with a library", () => {
  it("passes only the steps whose target is on the page, with ESCAPED text — never markup", () => {
    boot({ strings: { "Tour.T.Step1.Title": "<img src=x onerror=alert(1)>", "Tour.T.Step1.Text": "a & b", "Tour.Next": "Next" } });
    fakeLibrary();
    window.DitenTour.register("T", { version: 1, steps: STEPS });

    expect(window.DitenTour.isAvailable("T")).toBe(true);
    expect(window.DitenTour.start("T")).toBe(true);

    const config = calls[0];
    expect(config.steps).toHaveLength(1);
    expect(config.steps[0].element).toBe(document.querySelector('[data-tour="list"]'));
    expect(config.steps[0].popover.title).toBe("&lt;img src=x onerror=alert(1)&gt;");
    expect(config.steps[0].popover.description).toBe("a &amp; b");
    expect(config.nextBtnText).toBe("Next");
    expect(config.allowKeyboardControl).toBe(true);
    expect(calls[1]).toBe("drive");
  });

  it("a missing string shows its key, not an empty bubble", () => {
    boot();
    fakeLibrary();
    window.DitenTour.register("T", { version: 1, steps: STEPS });
    window.DitenTour.start("T");
    expect(calls[0].steps[0].popover.title).toBe("Tour.T.Step1.Title");
  });

  it("Arabic (dir=rtl) gets the RTL popover class", () => {
    boot();
    document.documentElement.setAttribute("dir", "rtl");
    fakeLibrary();
    window.DitenTour.register("T", { version: 1, steps: STEPS });
    window.DitenTour.start("T");
    expect(calls[0].popoverClass).toContain("diten-tour-rtl");
  });

  it("below md width there is no tour", () => {
    boot({ width: 600 });
    fakeLibrary();
    window.DitenTour.register("T", { version: 1, steps: STEPS });
    expect(window.DitenTour.isAvailable("T")).toBe(false);
    expect(window.DitenTour.start("T")).toBe(false);
  });

  it("finishing marks THIS version seen; a new version is offered again", () => {
    boot();
    fakeLibrary();
    window.DitenTour.register("T", { version: 1, steps: STEPS });
    window.DitenTour.start("T");
    calls[0].onDestroyed();
    expect(window.DitenTour.isCompleted("T")).toBe(true);
    expect(window.localStorage.getItem("diten.tour.seen.T.v1")).toBe("1");

    window.DitenTour.register("T", { version: 2, steps: STEPS });
    expect(window.DitenTour.isCompleted("T")).toBe(false);
  });

  it("storage that throws is not an error: the tour is simply offered again", () => {
    boot();
    fakeLibrary();
    installStorage({ blocked: true });
    window.DitenTour.register("T", { version: 1, steps: STEPS });
    window.DitenTour.start("T");
    expect(() => calls[0].onDestroyed()).not.toThrow();
    expect(window.DitenTour.isCompleted("T")).toBe(false);
  });
});
