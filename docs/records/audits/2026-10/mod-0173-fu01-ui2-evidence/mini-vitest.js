// WP-0173-UI2 E1 (copy of the WP-0173-LOC2 runner) — a minimal vitest-compatible runner (describe / it / test / test.each / expect with the matchers the
// repo-wide guard tests use). vitest itself cannot run here (no node_modules). The test FILES are executed unchanged.
// Usage: node mini-vitest.js <test file> [...]
const path = require('path');
const util = require('util');

const results = [];
const stack = [];
const hooks = { beforeEach: [], afterEach: [] };

const deepEqual = (a, b) => util.isDeepStrictEqual(a, b);
const fmt = (v) => util.inspect(v, { depth: 4, breakLength: 160 }).slice(0, 1500);

function makeExpect(actual, message) {
    const fail = (text) => { throw new Error((message ? message + ' — ' : '') + text); };
    const matchers = (negate) => {
        const check = (pass, text) => { if (negate ? pass : !pass) fail((negate ? 'NOT ' : '') + text); };
        return {
            toBe: (e) => check(Object.is(actual, e), `expected ${fmt(actual)} to be ${fmt(e)}`),
            toEqual: (e) => check(deepEqual(actual, e), `expected ${fmt(actual)} to equal ${fmt(e)}`),
            toStrictEqual: (e) => check(deepEqual(actual, e), `expected ${fmt(actual)} to strictly equal ${fmt(e)}`),
            toBeNull: () => check(actual === null, `expected ${fmt(actual)} to be null`),
            toBeUndefined: () => check(actual === undefined, `expected ${fmt(actual)} to be undefined`),
            toBeDefined: () => check(actual !== undefined, 'expected value to be defined'),
            toBeTruthy: () => check(!!actual, `expected ${fmt(actual)} to be truthy`),
            toBeFalsy: () => check(!actual, `expected ${fmt(actual)} to be falsy`),
            toBeGreaterThan: (e) => check(actual > e, `expected ${fmt(actual)} > ${e}`),
            toBeGreaterThanOrEqual: (e) => check(actual >= e, `expected ${fmt(actual)} >= ${e}`),
            toBeLessThan: (e) => check(actual < e, `expected ${fmt(actual)} < ${e}`),
            toContain: (e) => check(actual != null && actual.includes(e), `expected ${fmt(actual)} to contain ${fmt(e)}`),
            toHaveLength: (e) => check(actual != null && actual.length === e, `expected length ${actual?.length} to be ${e}`),
            toMatch: (e) => check(typeof e === 'string' ? String(actual).includes(e) : e.test(String(actual)), `expected ${fmt(actual)} to match ${e}`),
            toThrow: () => { let threw = false; try { actual(); } catch (err) { threw = true; } check(threw, 'expected function to throw'); }
        };
    };
    const m = matchers(false);
    m.not = matchers(true);
    return m;
}

global.expect = makeExpect;
global.describe = (name, fn) => { stack.push(name); try { fn(); } finally { stack.pop(); } };
global.describe.each = (rows) => (name, fn) => rows.forEach((row) => global.describe(util.format(name, ...(Array.isArray(row) ? row : [row])), () => fn(...(Array.isArray(row) ? row : [row]))));
const register = (name, fn) => results.push({ name: [...stack, name].join(' › '), fn, before: [...hooks.beforeEach], after: [...hooks.afterEach] });
global.it = register;
global.test = register;
global.it.each = global.test.each = (rows) => (name, fn) => rows.forEach((row) => register(util.format(name, ...(Array.isArray(row) ? row : [row])), () => fn(...(Array.isArray(row) ? row : [row]))));
global.it.skip = global.test.skip = () => {};
global.beforeEach = (fn) => hooks.beforeEach.push(fn);
global.afterEach = (fn) => hooks.afterEach.push(fn);
global.beforeAll = (fn) => fn();
global.afterAll = () => {};

(async () => {
    let failed = 0;
    for (const file of process.argv.slice(2)) {
        results.length = 0;
        hooks.beforeEach.length = 0;
        hooks.afterEach.length = 0;
        require(path.resolve(file));
        console.log(`## ${path.basename(file)} — ${results.length} tests`);
        for (const r of results) {
            try {
                for (const h of r.before) await h();
                await r.fn();
                for (const h of r.after) await h();
                console.log(`  PASS ${r.name}`);
            } catch (error) {
                failed++;
                console.log(`  FAIL ${r.name}\n       ${String(error.message).split('\n').join('\n       ')}`);
            }
        }
    }
    console.log(failed ? `FAILED: ${failed}` : 'ALL PASSED');
    process.exitCode = failed ? 1 : 0;
})();
