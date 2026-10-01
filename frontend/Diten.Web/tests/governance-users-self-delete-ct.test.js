const fs = require("fs");
const path = require("path");

/*
 * OWN ROW, NO DELETE — owner finding 2026-09-24 (control round, item 6), CT guard.
 *
 * The kebab offered "Sil" on the signed-in administrator's own row; the server refused it (USER_DELETE_SELF) and the
 * toast said only "an error occurred". A menu item that can only fail is not an action: the row of the signed-in user
 * draws no Delete. The server rule stays (it is the backstop, and the last-steward rule can only be judged there).
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const source = fs.readFileSync(path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "Governance", "Users", "index.js"), "utf8");

describe("the signed-in user's own row offers no Delete and no Disable (CT)", () => {
  it("the Disable action is pushed only for other people's rows", () => {
    expect(source, "Disable is pushed without the own-row guard")
      .toMatch(/if \(canUpdate\(\) && full\.isActive && !isCurrentUser\(full\)\) actions\.push\(adminAction\('disable'/);
  });

  it("the Delete action is pushed only for other people's rows", () => {
    expect(source, "Delete is pushed without the own-row guard").toMatch(/if \(canDelete\(\) && !isCurrentUser\(full\)\) actions\.push\(\{ key: 'delete'/);
    expect((source.match(/actions\.push\(\{ key: 'delete'/g) || []).length, "a second, unguarded Delete push").toBe(1);
  });

  it("the row is compared with the shell's CurrentUser id, and a missing id never hides Delete for everybody", () => {
    const start = source.indexOf("const isCurrentUser = ");
    const end = source.indexOf("\n", start); // one-line declaration; the next line is `const canDelete`
    expect(start).toBeGreaterThan(-1);
    // eslint-disable-next-line no-new-func
    const factory = new Function("window", source.slice(start, end) + "; return isCurrentUser;");
    const me = { id: "11111111-1111-1111-1111-111111111111" };
    const isCurrentUser = factory({ CurrentUser: me });
    expect(isCurrentUser({ id: me.id })).toBe(true);
    expect(isCurrentUser({ id: "22222222-2222-2222-2222-222222222222" })).toBe(false);
    const noShell = factory({});
    expect(noShell({ id: me.id }), "with no CurrentUser the guard must not hide Delete").toBe(false);
  });
});
