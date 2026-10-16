const fs = require("fs");
const path = require("path");

/*
 * WP-EMAIL-LANG-01 FIX3 — My profile's script (assets/js/Profile/index.js), measured through the REAL file: a save
 * redraws the page in the saved language when this browser shows another one (the page's own <html lang>), the
 * redraw is location.replace at the page's own address, and the "saved" flag is spent the moment the page starts.
 * The file runs against a stand-in `root` (location, sessionStorage, fetch) over jsdom's document.
 */
const SOURCE = fs.readFileSync(path.resolve(__dirname, "..", "wwwroot/assets/js/Profile/index.js"), "utf8");
const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

function page({ stored = "fr", screen = "en", flag = null, saveAnswer } = {}) {
  document.documentElement.setAttribute("lang", screen);
  document.body.innerHTML = `
    <script id="profile-l10n" type="application/json">{"Saved":"Saved","Cleared":"Cleared","SaveFailed":"Failed","LoadFailed":"Load failed"}</script>
    <div id="profilePage">
      <div id="profileNotice" class="d-none"></div>
      <div id="profileSkeleton"></div>
      <div id="profileCard" class="d-none">
        <span id="profileName"></span><span id="profileEmail"></span>
        <select id="profileLanguage"><option value=""></option><option value="en">en</option><option value="fr">fr</option><option value="es">es</option></select>
        <button type="button" id="profileSave">Save</button>
      </div>
    </div>`;
  const store = new Map(flag === null ? [] : [["profile.languageSaved", flag]]);
  const answer = (status, body) => Promise.resolve({ ok: status < 300, status, text: () => Promise.resolve(JSON.stringify(body)) });
  const root = {
    document,
    location: { pathname: "/Profile", replace: vi.fn() },
    sessionStorage: {
      getItem: (key) => (store.has(key) ? store.get(key) : null),
      setItem: (key, value) => store.set(key, String(value)),
      removeItem: (key) => store.delete(key),
    },
    fetch: vi.fn((url, options) => (options.method === "GET"
      ? answer(200, { data: { firstName: "Jean", lastName: "Dupont", email: "jean@acme.test", preferredLanguage: stored } })
      : answer(200, saveAnswer || { data: { preferredLanguage: JSON.parse(options.body).preferredLanguage } }))),
  };
  new Function("window", SOURCE)(root);
  return { root, store };
}

async function save(language) {
  document.getElementById("profileLanguage").value = language;
  document.getElementById("profileSave").click();
  await tick(); await tick(); await tick();
}

describe("WP-EMAIL-LANG-01 FIX3: My profile's save, flag and redraw", () => {
  it("F4: saving the stored language again redraws the page when this browser shows another one (<html lang>)", async () => {
    const { root } = page({ stored: "fr", screen: "en" });
    await tick(); await tick();

    await save("fr");

    expect(root.location.replace).toHaveBeenCalledWith("/Profile");
  });

  it("F4: the same language as stored AND as the screen is a notice, no redraw", async () => {
    const { root } = page({ stored: "fr", screen: "fr-FR" });
    await tick(); await tick();

    await save("fr");

    expect(root.location.replace).not.toHaveBeenCalled();
    expect(document.getElementById("profileNotice").textContent).toBe("Saved");
  });

  it("F4: a redraw is location.replace at the page's own address and leaves the saved flag for the next start", async () => {
    const { root, store } = page({ stored: "fr", screen: "fr" });
    await tick(); await tick();

    await save("es");

    expect(root.location.replace).toHaveBeenCalledTimes(1);
    expect(root.location.replace).toHaveBeenCalledWith("/Profile");
    expect(store.get("profile.languageSaved")).toBe("1");
  });

  it("F4: the saved flag is spent the moment the page starts, and says Saved once", async () => {
    const { store } = page({ stored: "fr", screen: "fr", flag: "1" });

    expect(store.has("profile.languageSaved")).toBe(false);
    await tick(); await tick();
    expect(document.getElementById("profileNotice").textContent).toBe("Saved");
  });
});
