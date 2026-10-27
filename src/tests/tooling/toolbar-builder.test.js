import assert from "node:assert/strict";
import { test } from "node:test";
import { existsSync, readFileSync } from "node:fs";
import { buildToolbar } from "../../../tools/toolbar-builder/builder.mjs";

const settings = {
  items: [
    { label: "Home", href: "#home" },
    { label: "Docs", href: "/docs" },
  ],
  theme: "light",
  position: "bottom",
  accent: "#6c35ab",
};

test("tiny builder emits a real self-contained toolbar below 3 KiB, without SDK or script", () => {
  const result = buildToolbar(settings);
  assert.ok(Buffer.byteLength(result.html) < 3072);
  assert.match(
    result.html,
    /<nav class="tiny-toolbar" aria-label="Custom toolbar">/,
  );
  assert.match(result.html, /min-height:44px/);
  assert.doesNotMatch(
    result.html,
    /<script|@tools\.moorkyz\.com|stylesheet.*https:|<iframe/,
  );
  const dark = buildToolbar({ ...settings, theme: "dark", position: "top" });
  assert.match(dark.css, /top:1rem/);
  assert.match(dark.css, /#f7f4fc/);
});

test("builder escapes all labels/attributes and rejects dangerous URLs or invalid settings", () => {
  const html = buildToolbar({
    ...settings,
    items: [
      {
        label: '<img src=x onerror="bad">',
        href: 'https://example.com/?q="<x>"',
      },
    ],
  }).html;
  assert.match(html, /&lt;img/);
  assert.match(html, /&quot;&lt;x&gt;&quot;/);
  assert.doesNotMatch(html, /<img/);
  for (const href of [
    "javascript:alert(1)",
    "data:text/html,x",
    "//evil.example",
    "/\\evil.example",
    "https://user:pass@example.com",
    " file:///C:/x",
  ]) {
    assert.throws(() =>
      buildToolbar({ ...settings, items: [{ label: "Link", href }] }),
    );
  }
  assert.throws(() => buildToolbar({ ...settings, items: [] }));
  assert.throws(() =>
    buildToolbar({ ...settings, accent: "red;display:none" }),
  );
  assert.throws(() =>
    buildToolbar({
      ...settings,
      items: Array.from({ length: 9 }, () => settings.items[0]),
    }),
  );
});

test("active project no longer contains the legacy package, Studio, examples or SDK build gates", () => {
  const retiredPackage = ["tool", "bar"].join("");
  for (const file of [
    `src/packages/${retiredPackage}/package.json`,
    "src/packages/portal/src/app/studio/page.tsx",
    "src/packages/portal/src/components/functional-playground.tsx",
    "examples/react-consumer/package.json",
    "docs/contracts/sdk-and-schema.md",
  ]) {
    assert.equal(existsSync(file), false, file);
  }
  const portal = JSON.parse(
    readFileSync("src/packages/portal/package.json", "utf8"),
  );
  const root = JSON.parse(readFileSync("src/package.json", "utf8"));
  assert.equal(
    portal.dependencies[`@tools.moorkyz.com/${retiredPackage}`],
    undefined,
  );
  assert.equal(root.scripts["test:coverage"], undefined);
  assert.doesNotMatch(
    root.scripts.typecheck,
    new RegExp(`consumer|@tools\\.moorkyz\\.com/${retiredPackage}`),
  );
  assert.doesNotMatch(
    readFileSync("src/packages/portal/src/components/portal-shell.tsx", "utf8"),
    new RegExp(`@tools\\.moorkyz\\.com/${retiredPackage}|ToolbarMount`),
  );
});
