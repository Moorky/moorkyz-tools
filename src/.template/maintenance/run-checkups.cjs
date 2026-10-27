#!/usr/bin/env node
"use strict";

const path = require("node:path");
const { execFileSync } = require("node:child_process");
const { checkSyntax } = require("./helpers/check-syntax.cjs");

function main() {
  const args = process.argv.slice(2);
  if (args.length === 1 && args[0] === "--dry-run") {
    console.log(
      "PREVIEW ONLY: discover repository JavaScript and run node --check. No checks ran.",
    );
    return;
  }
  if (args.length === 1 && args[0] === "--help") {
    console.log(
      "Usage: node src/.template/maintenance/run-checkups.cjs [--dry-run|--help]",
    );
    return;
  }
  if (args.length) throw new Error("Unexpected arguments. Use --help.");
  const root = path.resolve(__dirname, "..", "..", "..");
  const files = execFileSync(
    "git",
    ["ls-files", "--cached", "--others", "--exclude-standard", "-z"],
    { cwd: root, encoding: "utf8", timeout: 10_000, windowsHide: true },
  )
    .split("\0")
    .filter(Boolean);
  const { checked, failures } = checkSyntax(root, files);
  if (failures.length) throw new Error(failures.join("\n"));
  if (!checked) throw new Error("No runnable JavaScript sources were checked.");
  console.log(
    `PASS: syntax checked ${checked} JavaScript files without executing them.`,
  );
}

try {
  main();
} catch (error) {
  console.error(`FAIL: ${error.message}`);
  process.exitCode = 1;
}
