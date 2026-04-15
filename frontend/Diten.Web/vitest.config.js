/** @type {import('vitest').UserConfig} */
module.exports = {
  test: {
    environment: "jsdom",
    include: ["tests/**/*.test.js"],
    globals: true,
    restoreMocks: true,
    clearMocks: true,
  },
};
