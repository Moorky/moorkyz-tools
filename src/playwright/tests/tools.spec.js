import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { readFile } from "node:fs/promises";

const origin = `http://127.0.0.1:${process.env.TOOLS_MOORKYZ_COM_PORTAL_PORT ?? process.env.PORTAL_TEST_PORT ?? "4174"}`;

test("every card displays the Moorkyz name and its subtle monospace identifier", async ({
  page,
}) => {
  await page.goto(origin);
  const tools = [
    ["toolbar", "Moorkyz Toolbar Builder", "moorkyz-tools-toolbar-builder"],
    ["videocrunch", "Moorkyz Video Crunch", "moorkyz-tools-video-crunch"],
    ["mediarecover", "Moorkyz Media Recover", "moorkyz-tools-mediarecover"],
    [
      "directory-cleaner",
      "Moorkyz Directory Cleaner",
      "moorkyz-tools-directory-cleaner",
    ],
  ];
  for (const [slug, title, id] of tools) {
    const card = page.locator(".tool-card").filter({
      has: page.getByRole("heading", { name: title, exact: true }),
    });
    const identifier = card.locator(".tool-id");
    await expect(identifier).toBeVisible();
    await expect(identifier).toHaveText(id);
    await expect(identifier).toHaveCSS("font-family", /DM Mono/);
    expect(
      await identifier.evaluate((element) =>
        Number.parseFloat(getComputedStyle(element).fontSize),
      ),
    ).toBeCloseTo(10.4, 1);
    await expect(identifier).toHaveCSS("color", "rgb(104, 104, 104)");
    await card.getByRole("link", { name: title, exact: true }).click();
    await expect(page).toHaveURL(`${origin}/tools/${slug}`);
    await expect(page.locator(".tool-detail-heading .tool-id")).toHaveText(id);
    await page.goto(origin);
  }
  await page.getByRole("button", { name: /Switch to dark theme/ }).click();
  await expect(page.locator(".tool-id").first()).toHaveCSS(
    "color",
    "rgb(184, 184, 184)",
  );
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await expect(page.locator(".brand .tool-id")).toHaveCount(0);
});

test("project identity is consistent across branding, metadata, assets and preferences", async ({
  page,
  request,
}) => {
  await page.goto(origin);
  await expect(page.locator(".brand strong")).toHaveText("Moorkyz Tools");
  await expect(page).toHaveTitle("A little more useful. — Moorkyz Tools");
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
    "href",
    "https://tools.moorkyz.com",
  );
  await expect(page.locator('meta[property="og:url"]')).toHaveAttribute(
    "content",
    "https://tools.moorkyz.com",
  );
  await expect(page.locator('meta[property="og:site_name"]')).toHaveAttribute(
    "content",
    "Moorkyz Tools",
  );
  await expect(page.locator('meta[property="og:image"]')).toHaveAttribute(
    "content",
    "https://tools.moorkyz.com/tools.moorkyz.com-mark.png",
  );
  await expect(page.locator('meta[name="twitter:title"]')).toHaveAttribute(
    "content",
    "Moorkyz Tools — A little more useful.",
  );
  await expect(
    page.getByRole("link", { name: "GitHub", exact: true }),
  ).toHaveAttribute("href", "https://github.com/Moorky/moorkyz-tools");
  const contact = page
    .locator(".website-footer")
    .getByRole("link", { name: "Stay in Touch?", exact: true });
  await expect(contact).toHaveAttribute("href", "https://moorkyz.com/contact");
  const manifest = await request.get(`${origin}/site.webmanifest`);
  expect(manifest.ok()).toBe(true);
  expect(await manifest.json()).toMatchObject({
    name: "Moorkyz Tools",
    short_name: "Moorkyz Tools",
  });
  const mark = await request.get(`${origin}/tools.moorkyz.com-mark.png`);
  expect(mark.ok()).toBe(true);
  expect(mark.headers()["content-type"]).toBe("image/png");
  await page.getByRole("button", { name: /Switch to dark theme/ }).click();
  expect(
    await page.evaluate(() => localStorage.getItem("tools.moorkyz.com-theme")),
  ).toBe("dark");
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.goto(`${origin}/tools/directory-cleaner`);
  await expect(page).toHaveTitle("Moorkyz Directory Cleaner — Moorkyz Tools");
});

test("catalog cards open complete tool pages, source tree and dormant local service", async ({
  page,
}) => {
  const requests = [];
  page.on("request", (request) => requests.push(request.url()));
  await page.goto(origin);
  await expect(
    page.getByRole("heading", { name: "A little more useful." }),
  ).toBeVisible();
  await expect(page.locator(".tool-card")).toHaveCount(5);
  await expect(page.locator(".tool-card").first()).toContainText(
    "Design, preview and export",
  );
  await page
    .getByRole("link", { name: "Moorkyz Video Crunch", exact: true })
    .click();
  await expect(page).toHaveURL(`${origin}/tools/videocrunch`);
  await expect(
    page.getByRole("heading", { name: "Moorkyz Video Crunch", exact: true }),
  ).toBeVisible();
  await expect(page.locator("dt", { hasText: "Release Date" })).toBeVisible();
  await expect(page.locator("dt", { hasText: "Updated On" })).toBeVisible();
  expect(await page.locator("main > section > h2").allTextContents()).toEqual([
    "Description",
    "Tool",
    "Use Cases",
    "Setup",
    "Workspace",
  ]);
  await expect(
    page.getByRole("navigation", { name: "Tool files" }),
  ).toContainText("public/");
  expect(
    requests.filter(
      (url) =>
        url.includes("/api/local-tools/") || url.endsWith("/videocrunch.json"),
    ),
  ).toEqual([]);
  await expect(page.locator("iframe")).toHaveCount(0);
  await page
    .getByRole("navigation", { name: "Tool files" })
    .getByRole("button", { name: "Get-WebVideoCrunch.ps1", exact: true })
    .click();
  await expect(page.locator(".source-console pre")).toContainText("#requires");
  await expect(page.getByRole("button", { name: "Copy file" })).toBeVisible();
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download file" }).click();
  const file = await download;
  expect(file.suggestedFilename()).toBe("Get-WebVideoCrunch.ps1");
  expect(await readFile(await file.path(), "utf8")).toBe(
    await readFile(
      new URL(
        "../../../tools/videocrunch/Get-WebVideoCrunch.ps1",
        import.meta.url,
      ),
      "utf8",
    ),
  );
  await expect(page.locator(".source-console pre")).toHaveAttribute(
    "tabindex",
    "0",
  );
  const archive = page.waitForEvent("download");
  await page.getByRole("link", { name: "Download all (.zip)" }).click();
  expect((await archive).suggestedFilename()).toBe("videocrunch.zip");
  await expect(page.locator(".website-footer")).toContainText("made /w");
  await expect(page.locator(".website-footer")).toContainText("Moorkyz");
  expect(requests.filter((url) => !url.startsWith(`${origin}/`))).toEqual([]);
});

test("file source errors can be retried and copy failures retain selectable source", async ({
  page,
}) => {
  await page.route("**/tool-sources/videocrunch.json", (route) =>
    route.fulfill({ status: 503, body: "Unavailable" }),
  );
  await page.goto(`${origin}/tools/videocrunch`);
  await page
    .getByRole("navigation", { name: "Tool files" })
    .getByRole("button", { name: "server.js", exact: true })
    .click();
  await expect(
    page.locator(".source-console").getByRole("alert"),
  ).toContainText("Source download failed (503)");
  await page.unroute("**/tool-sources/videocrunch.json");
  await page.getByRole("button", { name: "Retry source download" }).click();
  await expect(page.locator(".source-console pre")).toContainText(
    "createApplication",
  );
  await page.evaluate(() =>
    Object.defineProperty(navigator, "clipboard", {
      value: {
        writeText: async () => {
          throw new Error("denied");
        },
      },
      configurable: true,
    }),
  );
  await page.getByRole("button", { name: "Copy file" }).click();
  await expect(
    page.getByRole("status").filter({ hasText: "Could not copy" }),
  ).toBeVisible();
  await page.evaluate(() =>
    Object.defineProperty(navigator, "clipboard", {
      value: {
        writeText: async (text) => {
          window.copiedSource = text;
        },
      },
      configurable: true,
    }),
  );
  await page.getByRole("button", { name: "Copy file" }).click();
  expect(await page.evaluate(() => window.copiedSource)).toContain(
    "createApplication",
  );
  await expect(
    page.getByRole("status").filter({ hasText: "Copied to clipboard" }),
  ).toBeVisible();
  await page
    .getByRole("navigation", { name: "Tool files" })
    .getByRole("button", { name: "Start-WebVideoCrunch.cmd", exact: true })
    .click();
  await expect(page.locator(".source-console pre")).toContainText("powershell");
});

test("tool catalog and detail pages remain accessible and fit mobile in both themes", async ({
  page,
}, testInfo) => {
  test.slow();
  for (const route of [
    "/",
    "/tools/toolbar",
    "/tools/videocrunch",
    "/tools/directory-cleaner",
    "/tools/mediarecover",
  ]) {
    await page.goto(`${origin}${route}`);
    await page.emulateMedia({ reducedMotion: "reduce" });
    for (const width of [320, 390, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expect
        .poll(() =>
          page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
          ),
        )
        .toBe(true);
    }
    if (route.endsWith("videocrunch")) {
      const script = page
        .getByRole("navigation", { name: "Tool files" })
        .getByRole("button", { name: "server.js", exact: true });
      await script.focus();
      await page.keyboard.press("Enter");
      await expect(page.locator(".source-console h3")).toBeFocused();
      await expect(page.locator(".source-console pre")).toContainText(
        "createApplication",
      );
    }
    for (const theme of ["light", "dark"]) {
      await expect(page.locator(".theme-toggle")).toBeEnabled();
      if ((await page.locator("html").getAttribute("data-theme")) !== theme)
        await page
          .getByRole("button", { name: `Switch to ${theme} theme` })
          .click();
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
      await expect(page.locator("body")).toHaveCSS(
        "color",
        theme === "dark" ? "rgb(247, 244, 252)" : "rgb(32, 27, 43)",
      );
      await expect(page.locator("h1")).toHaveCSS(
        "color",
        theme === "dark" ? "rgb(247, 244, 252)" : "rgb(32, 27, 43)",
      );
      await page.evaluate(
        () =>
          new Promise((resolve) =>
            requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
          ),
      );
      expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
    }
    await page.screenshot({
      path: testInfo.outputPath(
        `${route === "/" ? "catalog" : route.split("/").at(-1)}.png`,
      ),
      fullPage: true,
      animations: "disabled",
    });
  }
  const response = await page.goto(`${origin}/tools/not-a-tool`);
  expect(response.status()).toBe(404);
});
