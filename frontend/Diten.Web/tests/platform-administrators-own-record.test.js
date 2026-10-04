const fs = require("fs");
const path = require("path");

// BL-529 FIX2 — "Send setup link" to an existing account resets its password (AuthService ends it and every session), so the
// Platform Administrators screen never offers it on the signed-in administrator's own record. The signed-in id is the
// AuthService account's and the row id is Platform's record id — they never matched — so the e-mail is the shared key.
const source = fs.readFileSync(path.resolve(__dirname, "..", "wwwroot", "assets", "js", "Platform", "Administrators", "index.js"), "utf8");
const slice = (from, to) => {
  const start = source.indexOf(from);
  const end = source.indexOf(to, start);
  if (start < 0 || end < 0) throw new Error(`slice not found: ${from}`);
  return source.slice(start, end);
};

const load = (currentUser) => {
  // eslint-disable-next-line no-new-func
  return new Function("window", slice("const isOwnRecord", "const updateBulkDeleteButtonVisibility") + "; return { isOwnRecord, isRowProtected };")(
    { CurrentUser: currentUser });
};

describe("BL-529 — your own administrator record", () => {
  test("is recognised by e-mail although the ids differ", () => {
    const { isOwnRecord, isRowProtected } = load({ id: "auth-account-id", email: "Me@Platform.test" });
    expect(isOwnRecord({ id: "platform-record-id", email: "me@platform.test" })).toBe(true);
    expect(isRowProtected({ id: "platform-record-id", email: "me@platform.test" })).toBe(true);
    expect(isOwnRecord({ id: "platform-record-id", email: "other@platform.test" })).toBe(false);
  });

  test("the row menu offers Send setup link only on a record that is not protected", () => {
    const render = slice("const isProtected = isRowProtected(row);\n\n                        const actions", "if (isProtected) {\n                            actions.push({\n                                key: 'delete'");
    expect(render).toMatch(/if \(!isProtected\) \{\s*actions\.push\(\{ key: 'resendInvite'/);
  });

  test("the edit panel hides the setup-link button on one's own record", () => {
    expect(source).toMatch(/wrapperResend\?\.classList\.toggle\('d-none', isOwnRecord\(data\)\)/);
  });
});
