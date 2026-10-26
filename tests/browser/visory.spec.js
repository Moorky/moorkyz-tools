import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { readFile } from "node:fs/promises";

test("Visory Bulk Downloader explains the unofficial desktop workflow, honest limits and unreleased downloads without network requests", async ({
  page,
}) => {
  const requests = [];
  page.on("request", (request) => requests.push(request.url()));
  await page.goto("/");
  const portalName = await page.locator(".brand strong").innerText();
  const card = page.locator(".tool-card").filter({
    has: page.getByRole("heading", {
      name: "Moorkyz Visory Bulk Downloader",
      exact: true,
    }),
  });
  await expect(card).toContainText("moorkyz-tools-visory-bulk-downloader");
  await card
    .getByRole("link", { name: "Moorkyz Visory Bulk Downloader", exact: true })
    .click();
  await expect(page).toHaveURL(/\/tools\/visory-bulk-downloader$/);
  await expect(page).toHaveTitle(
    `Moorkyz Visory Bulk Downloader — ${portalName}`,
  );
  expect(await page.locator("main > section > h2").allTextContents()).toEqual([
    "Description",
    "Tool",
    "Use Cases",
    "Setup",
    "Downloads",
  ]);
  const description = page.locator("#description");
  await expect(description).toContainText("unofficial");
  await expect(description).toContainText(
    "cannot sign in to Visory or download recordings",
  );
  await expect(description).toContainText("not affiliated with Visory");
  await expect(description).toContainText("were not tested with an account");
  await expect(description).toContainText("Application Control");
  expect(await description.locator("ol h4").allTextContents()).toEqual([
    "Sign in",
    "Scan",
    "Review",
    "Back up",
    "Verify",
    "Archive and report",
  ]);
  await expect(page.locator("#setup")).toContainText(".\\scripts\\publish.ps1");
  await expect(page.locator("#downloads")).toContainText("Not yet released");
  await expect(page.locator("#downloads a, #downloads button")).toHaveCount(0);
  await expect(
    page.locator("#workspace, iframe, input[type=file]"),
  ).toHaveCount(0);
  await expect(page.locator("#tool")).toContainText(
    "Canonical source maintained in tools/visory-bulk-downloader in Moorky/moorkyz-tools",
  );
  expect(
    requests.filter(
      (url) =>
        url.includes("visory.cam") ||
        url.includes("/api/local-tools/") ||
        url.includes("/tool-sources/visory-bulk-downloader."),
    ),
  ).toEqual([]);
  expect(
    requests.filter(
      (url) => new URL(url).origin !== new URL(page.url()).origin,
    ),
  ).toEqual([]);
});

test("Visory desktop source is readable, downloadable and never executed", async ({
  page,
}) => {
  await page.goto("/tools/visory-bulk-downloader");
  const provenance = page
    .getByRole("navigation", { name: "Tool files" })
    .getByRole("button", { name: "SOURCE-PROVENANCE.json", exact: true });
  await provenance.focus();
  await page.keyboard.press("Enter");
  await expect(page.locator(".source-console h3")).toBeFocused();
  await expect(page.locator(".source-console pre")).toContainText(
    '"sourceDirectory": "tools/visory-bulk-downloader"',
  );
  const downloaded = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download file" }).click();
  const download = await downloaded;
  expect(download.suggestedFilename()).toBe("SOURCE-PROVENANCE.json");
  expect(await readFile(await download.path(), "utf8")).toBe(
    await readFile(
      new URL(
        "../../tools/visory-bulk-downloader/SOURCE-PROVENANCE.json",
        import.meta.url,
      ),
      "utf8",
    ),
  );
  const archiveEvent = page.waitForEvent("download");
  await page.getByRole("link", { name: "Download all (.zip)" }).click();
  expect((await archiveEvent).suggestedFilename()).toBe(
    "visory-bulk-downloader.zip",
  );
  await expect(page.locator("iframe")).toHaveCount(0);
});

test("Visory Bulk Downloader is searchable and accessible in both themes at narrow widths", async ({
  page,
}, testInfo) => {
  await page.goto("/");
  await page
    .getByRole("button", { name: "Search documentation and navigation" })
    .click();
  const dialog = page.getByRole("dialog", { name: "Find your next step." });
  await dialog.getByRole("searchbox").fill("visory");
  await dialog
    .getByRole("link", { name: /Moorkyz Visory Bulk Downloader/ })
    .click();
  await expect(page).toHaveURL(/\/tools\/visory-bulk-downloader$/);
  await page.emulateMedia({ reducedMotion: "reduce" });
  for (const width of [320, 390, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  }
  for (const theme of ["light", "dark"]) {
    if ((await page.locator("html").getAttribute("data-theme")) !== theme)
      await page
        .getByRole("button", { name: `Switch to ${theme} theme` })
        .click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
    for (const locator of [
      page.locator("body"),
      page.locator("h1"),
      page.locator("#description h4").first(),
    ])
      await expect(locator).toHaveCSS(
        "color",
        theme === "dark" ? "rgb(247, 244, 252)" : "rgb(32, 27, 43)",
      );
    await page.evaluate(
      () =>
        new Promise((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(resolve)),
        ),
    );
    await page.screenshot({
      path: testInfo.outputPath(`visory-tool-page-${theme}.png`),
      fullPage: true,
      animations: "disabled",
    });
    expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  }
});
