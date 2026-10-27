import assert from "node:assert/strict";
import { test } from "node:test";
import { getFileInfo } from "prettier";

test("the relocated formatter ignores generated output without hiding maintained source", async () => {
  const options = { ignorePath: "src/.prettierignore" };
  for (const file of [
    "src/packages/portal/.next/package.json",
    "src/packages/portal/public/tool-sources/visory-media-downloader.json",
    "src/test-results/browser-results.json",
    "src/pnpm-lock.yaml",
    "src/node_modules/prettier/package.json",
  ]) {
    assert.equal((await getFileInfo(file, options)).ignored, true, file);
  }
  for (const file of [
    "src/packages/portal/src/app/layout.tsx",
    "src/.template/cli/moorkyz.cjs",
    "src/.moorkyz/launchers/tools.moorkyz.com.cjs",
  ]) {
    assert.equal((await getFileInfo(file, options)).ignored, false, file);
  }
});
