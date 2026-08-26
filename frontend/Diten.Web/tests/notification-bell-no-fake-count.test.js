const fs = require("fs");
const path = require("path");

/*
 * WC-4 — the notification bell tells no lies.
 *
 * THE DEFECT. Both shared layouts carried the Sneat theme's sample notification menu verbatim: a hard-coded count
 * of 8, four invented records ("Congratulation Lettie 🎉", "You have new order 🛒"), stock avatars, dead
 * javascript:void(0) links, and no JavaScript behind any of it.
 *
 * It was harmless while nothing produced notifications. It stopped being harmless the moment real task
 * notifications started flowing: a user assigns a task, the bell still says 8, that 8 has nothing to do with
 * anything, and they file "notifications are not arriving" against a system that is working. A badge that lies is
 * worse than no badge.
 *
 * Wiring the bell to real data is BL-025 (platform slice). This ticket only removes the lie.
 */
const LAYOUTS = [
  path.resolve(__dirname, "..", "Views", "Shared", "_LayoutTenantShell.cshtml"),
  path.resolve(__dirname, "..", "Views", "Shared", "_Layout.cshtml")
];

const read = (file) => fs.readFileSync(file, "utf8");

/** The bell's own markup — from its <li> to the closing one, so an assertion cannot stray into the user menu. */
const bellBlock = (content) => {
  const start = content.indexOf('<li class="nav-item dropdown-notifications');
  expect(start).toBeGreaterThan(-1);
  const end = content.indexOf("<!--/ Notification -->", start);
  expect(end).toBeGreaterThan(start);
  return content.slice(start, end);
};

describe("WC-4: the notification bell carries no invented data", () => {
  LAYOUTS.forEach((layout) => {
    const name = path.basename(layout);

    describe(name, () => {
      it("shows no count at all", () => {
        // Not "shows zero" — a count implies something counted it, and nothing does yet.
        const bell = bellBlock(read(layout));

        expect(bell).not.toContain("badge-notifications");
        expect(bell).not.toMatch(/NewNotifications/);
        expect(bell).not.toMatch(/>\s*8\s*</);
        expect(bell).not.toContain("8 New");
      });

      it("carries none of the theme's sample records", () => {
        const bell = bellBlock(read(layout));

        ["Congratulation Lettie", "You have new order", "New Message", "ACME Inc.", "Natalie"]
          .forEach((sample) => expect(bell).not.toContain(sample));
      });

      it("shows no stock avatars", () => {
        // A stranger's face beside a notification that does not exist.
        expect(bellBlock(read(layout))).not.toMatch(/avatars\/\d+\.png/);
      });

      it("offers no dead controls", () => {
        /*
         * "Mark all as read", per-row read/archive buttons and "View all notifications" were all
         * href="javascript:void(0)" with no handler. A control that does nothing when pressed is a defect the
         * moment the surface around it becomes real.
         */
        const bell = bellBlock(read(layout));

        expect(bell).not.toContain("dropdown-notifications-all");
        expect(bell).not.toContain("dropdown-notifications-read");
        expect(bell).not.toContain("dropdown-notifications-archive");
        expect(bell).not.toMatch(/View all notifications|ViewAllNotifications/);
      });

      it("still renders a bell, and says plainly that it is empty", () => {
        /*
         * Non-vacuity for every assertion above: deleting the whole menu would satisfy them all while removing a
         * navigation affordance the shell is expected to have. The bell stays; it just tells the truth.
         */
        const bell = bellBlock(read(layout));

        expect(bell).toContain("bx-bell");
        expect(bell).toMatch(/NoNotifications|No notifications/);
      });

      it("uses classes only, never an inline style (FG-003)", () => {
        expect(bellBlock(read(layout))).not.toMatch(/\sstyle\s*=\s*"/);
      });
    });
  });
});

describe("WC-4: the empty-state string exists in all seven languages", () => {
  const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const RESX = path.resolve(__dirname, "..", "Resources");

  const value = (locale, key) => {
    const xml = read(path.join(RESX, `SharedResource.${locale}.resx`));
    const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
    return match ? match[1].trim() : null;
  };

  it("is present everywhere", () => {
    // The tenant shell is a tenant surface: a missing key renders the KEY, in the navbar, on every page.
    LOCALES.forEach((locale) => expect(value(locale, "NoNotifications")).toBeTruthy());
  });

  it("is translated rather than left in English", () => {
    const english = value("en", "NoNotifications");

    ["tr", "fr", "es", "zh", "ar", "ru"].forEach((locale) => {
      expect(value(locale, "NoNotifications")).not.toBe(english);
    });
  });
});
