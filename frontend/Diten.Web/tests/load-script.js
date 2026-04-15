const fs = require("fs");
const path = require("path");
const vm = require("vm");

function loadScript(relativePath) {
  const filePath = path.resolve(__dirname, "..", relativePath);
  const source = fs.readFileSync(filePath, "utf8");
  vm.runInThisContext(source, { filename: filePath });
}

module.exports = { loadScript };
