import { expect, test } from "@playwright/test";

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

for (const route of [
  "/",
  "/contributing",
  ...tools.map(([slug]) => `/tools/${slug}`),
]) {
  test(`${route} has no tool-initial labels in either theme or layout`, async ({
    page,
  }) => {
    await page.goto(route);
    await page.emulateMedia({ reducedMotion: "reduce" });

    for (const theme of ["light", "dark"]) {
      if ((await page.locator("html").getAttribute("data-theme")) !== theme) {
        await page
          .getByRole("button", { name: `Switch to ${theme} theme` })
          .click();
      }
      for (const width of [320, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        await expect(page.locator(".tool-monogram")).toHaveCount(0);
        await expect(page.getByText(/^(?:tb|vc|mr|dc)$/i)).toHaveCount(0);
        await expect(page.locator(".brand")).toHaveAccessibleName(
          "Moorkyz Tools",
        );
      }
    }

    if (route === "/") {
      for (const [, title, identifier] of tools) {
        const card = page.locator(".tool-card").filter({
          has: page.getByRole("heading", { name: title, exact: true }),
        });
        await expect(card.locator(".tool-id")).toHaveText(identifier);
      }
      await page
        .getByRole("button", { name: "Search documentation and navigation" })
        .click();
      await expect(page.getByText(/^(?:tb|vc|mr|dc)$/i)).toHaveCount(0);
      await expect(page.locator(".tool-monogram")).toHaveCount(0);
    }

    const tool = tools.find(([slug]) => route === `/tools/${slug}`);
    if (tool) {
      const [, title, identifier] = tool;
      await expect(page.getByRole("heading", { level: 1 })).toHaveText(title);
      await expect(page.locator(".tool-detail-heading .tool-id")).toHaveText(
        identifier,
      );
      const allTools = page.getByRole("link", { name: "All tools" });
      await allTools.focus();
      await expect(allTools).toBeFocused();
      await page.keyboard.press("Enter");
      await expect(page).toHaveURL(/\/#tools$/);
    }
  });
}
