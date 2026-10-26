import { expect, test } from "@playwright/test";
import { readFile, mkdtemp, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";
import AxeBuilder from "@axe-core/playwright";

test("Moorky Toolbar Builder previews, copies and downloads independent small HTML", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/tools/toolbar");
  await expect(
    page.getByRole("heading", {
      name: "Moorky Toolbar Builder",
      exact: true,
    }),
  ).toBeVisible();
  await expect(page.locator(".compact-builder")).toBeVisible();
  const bounds = await page.locator(".compact-builder").boundingBox();
  expect(bounds.height).toBeLessThan(540);
  expect(bounds.width).toBeLessThanOrEqual(704);
  await expect(page.locator(".mzk-toolbar-host")).toHaveCount(0);
  await expect(
    page.getByRole("link", { name: /Studio|Playground/ }),
  ).toHaveCount(0);
  await page.getByLabel("Label 1", { exact: true }).fill("My home");
  await expect(
    page.getByRole("navigation", { name: "Custom toolbar preview" }),
  ).toContainText("My home");
  await page.getByLabel("Link 1", { exact: true }).fill("javascript:alert(1)");
  await expect(
    page.locator(".compact-builder").getByRole("alert"),
  ).toContainText("Only HTTP");
  await expect(
    page.getByRole("button", { name: "Download HTML" }),
  ).toBeDisabled();
  await page.getByLabel("Link 1", { exact: true }).fill("#my-home");
  await page
    .getByRole("combobox", { name: "Appearance", exact: true })
    .selectOption("dark");
  await expect(
    page.getByRole("navigation", { name: "Custom toolbar preview" }),
  ).toHaveCSS("color", "rgb(247, 244, 252)");
  await page
    .getByRole("combobox", { name: "Position", exact: true })
    .selectOption("top");
  await page
    .getByRole("navigation", { name: "Custom toolbar preview" })
    .getByRole("link", { name: "My home" })
    .click();
  expect(page.url()).not.toContain("#my-home");
  await page.evaluate(() =>
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: {
        writeText: async (text) => {
          window.toolbarHTML = text;
        },
      },
    }),
  );
  await page.getByRole("button", { name: "Copy HTML" }).click();
  expect(await page.evaluate(() => window.toolbarHTML)).toContain(
    'href="#my-home"',
  );
  const event = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download HTML" }).click();
  const download = await event;
  expect(download.suggestedFilename()).toBe("toolbar.html");
  const html = await readFile(await download.path(), "utf8");
  expect(Buffer.byteLength(html)).toBeLessThan(3072);
  expect(html).not.toMatch(/@tools\.moorkyz\.com|<script|<iframe/);
  const folder = await mkdtemp(path.join(tmpdir(), "tools.moorkyz.com-html-"));
  const popup = await page.context().newPage();
  try {
    const file = path.join(folder, "toolbar.html");
    await writeFile(file, html);
    await popup.goto(pathToFileURL(file).href);
    await expect(
      popup.getByRole("navigation", { name: "Custom toolbar" }),
    ).toBeVisible();
    await popup.getByRole("link", { name: "My home" }).focus();
    await expect(popup.getByRole("link", { name: "My home" })).toBeFocused();
    expect(
      (await new AxeBuilder({ page: popup }).analyze()).violations,
    ).toEqual([]);
  } finally {
    await popup.close();
    await rm(folder, { recursive: true, force: true });
  }
  expect(errors).toEqual([]);
});

test("Moorky Directory Cleaner settings and script downloads work with service off and zero native requests", async ({
  page,
}) => {
  const calls = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/local-tools/directory-cleaner/"))
      calls.push(request.url());
  });
  await page.goto("/tools/directory-cleaner");
  const workspace = page.locator(".directory-cleaner");
  await expect(
    workspace.getByRole("status").filter({ hasText: "connection is off" }),
  ).toBeVisible();
  await expect(
    workspace.getByRole("button", { name: "Connect local service" }),
  ).toHaveCount(0);
  await workspace
    .getByLabel("Source folder", { exact: true })
    .fill("C:\\MyFiles");
  const archiveRule = workspace.getByRole("group", {
    name: "Rule 2",
    exact: true,
  });
  await archiveRule.locator("summary").click();
  await archiveRule
    .getByLabel("Created before (UTC)", { exact: true })
    .fill("2026-01-01");
  await archiveRule.getByLabel("Larger than", { exact: true }).fill("0.5");
  const downloaded = page.waitForEvent("download");
  await workspace.getByRole("button", { name: "Download settings" }).click();
  const download = await downloaded;
  const settings = JSON.parse(await readFile(await download.path(), "utf8"));
  expect(settings.rules[1]).toMatchObject({
    action: "archive",
    smallerThanBytes: 1048576,
    largerThanBytes: 524288,
    createdBefore: "2026-01-01",
  });
  expect(settings.recursive).toBe(false);
  expect(calls).toEqual([]);
  await expect(
    workspace.getByRole("link", { name: "Download script (.zip)" }),
  ).toHaveAttribute("href", "/tool-sources/directory-cleaner.zip");
});

test("native header/search navigation remains independent and old SDK routes are gone", async ({
  page,
}) => {
  await page.goto("/");
  await page
    .getByRole("button", { name: "Search documentation and navigation" })
    .click();
  const dialog = page.getByRole("dialog", { name: "Find your next step." });
  await dialog.getByRole("searchbox").fill("directory");
  await dialog.getByRole("link", { name: /Moorky Directory Cleaner/ }).click();
  await expect(page).toHaveURL(/\/tools\/directory-cleaner$/);
  await expect(page.locator(".mzk-toolbar-host")).toHaveCount(0);
  for (const route of ["/studio", "/tools/toolbar/playground"]) {
    const response = await page.goto(route);
    expect(response.status()).toBe(404);
  }
});
