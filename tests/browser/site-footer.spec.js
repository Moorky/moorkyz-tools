import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test("shared footer preserves credits and has one complete projects link on every route", async ({
  page,
}) => {
  for (const route of [
    "/",
    "/tools/toolbar",
    "/tools/videocrunch",
    "/tools/mediarecover",
    "/tools/directory-cleaner",
    "/contributing",
  ]) {
    await page.goto(route);
    const footer = page.getByRole("contentinfo");
    await expect(footer).toHaveCount(1);
    await expect(footer.locator(".footer-note")).toHaveText(
      `made /w© ${new Date().getUTCFullYear()}Moorkyz`,
    );
    const projects = footer.getByRole("link", {
      name: "More projects? Moorkyz",
      exact: true,
    });
    await expect(projects).toHaveAttribute(
      "href",
      "https://moorkyz.com/projects",
    );
    await expect(projects).toHaveText("More projects?");
    await expect(projects.locator("svg")).toHaveAttribute(
      "aria-hidden",
      "true",
    );
    await expect(projects.locator("svg")).toHaveAttribute("focusable", "false");
    await expect(
      footer.getByRole("link", { name: "Evidence workspace", exact: true }),
    ).toHaveAttribute("href", "https://evidence.moorkyz.com");
    await expect(
      footer.getByRole("link", { name: "Moorkyz", exact: true }),
    ).toHaveAttribute("href", "https://moorkyz.com");
  }
});

test("footer is 35 percent shorter, centered and bottom-right aligned in both themes", async ({
  page,
}, testInfo) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  const footer = page.getByRole("contentinfo");
  for (const theme of ["light", "dark"]) {
    if ((await page.locator("html").getAttribute("data-theme")) !== theme) {
      await page
        .getByRole("button", { name: `Switch to ${theme} theme` })
        .click();
    }
    for (const width of [320, 390, 700, 701, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await footer.scrollIntoViewIfNeeded();
      const metrics = await footer.evaluate((element) => {
        const projects = element.querySelector(".footer-projects");
        const credit = element.querySelector(".footer-note");
        const evidence = element.querySelector("nav");
        const logo = projects.querySelector("svg");
        const rect = (node) => node.getBoundingClientRect().toJSON();
        return {
          footer: rect(element),
          projects: rect(projects),
          credit: rect(credit),
          evidence: rect(evidence),
          logo: rect(logo),
          fontSize: parseFloat(getComputedStyle(projects).fontSize),
          position: getComputedStyle(element).position,
          background: getComputedStyle(element).backgroundImage,
          fits: document.documentElement.scrollWidth <= innerWidth,
        };
      });
      expect(1 - metrics.footer.height / 107.2).toBeCloseTo(0.35, 2);
      expect(metrics.position).toBe("relative");
      expect(metrics.background).toContain("rgb(3, 6, 14)");
      expect(metrics.fits).toBe(true);
      expect(metrics.credit.x + metrics.credit.width / 2).toBeCloseTo(
        width / 2,
        0,
      );
      expect(metrics.footer.right - metrics.projects.right).toBeCloseTo(16, 0);
      expect(metrics.footer.bottom - metrics.projects.bottom).toBeLessThan(10);
      expect(metrics.projects.height).toBeGreaterThanOrEqual(24);
      expect(metrics.projects.y).toBeGreaterThanOrEqual(
        width <= 700 ? metrics.credit.bottom : metrics.credit.y,
      );
      expect(metrics.evidence.right).toBeLessThanOrEqual(metrics.projects.x);
      if (width > 700) {
        expect(metrics.evidence.right).toBeLessThan(metrics.credit.x);
        expect(metrics.credit.right).toBeLessThan(metrics.projects.x);
      }
      expect(metrics.logo.width).toBeCloseTo(metrics.fontSize, 1);
      expect(metrics.logo.height).toBeCloseTo(metrics.fontSize, 1);
      await expect(footer.locator(".footer-projects")).toHaveCSS(
        "color",
        "rgb(255, 255, 255)",
      );
      await expect(footer.locator(".footer-projects svg")).toHaveCSS(
        "color",
        "rgb(255, 255, 255)",
      );
      await expect(footer.locator(".footer-projects")).toHaveCSS(
        "text-decoration-line",
        "none",
      );
      if (width === 320 || width === 1440) {
        await footer.screenshot({
          path: testInfo.outputPath(`footer-${theme}-${width}.png`),
        });
      }
    }
    expect(
      (await new AxeBuilder({ page }).include(".website-footer").analyze())
        .violations,
    ).toEqual([]);
  }
});

test("footer matches the live fade and glint and disables motion when requested", async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await page.goto("/");
  const motion = await page.getByRole("contentinfo").evaluate((element) => {
    const footer = getComputedStyle(element);
    const glint = getComputedStyle(element, "::after");
    const animation = element
      .getAnimations()
      .find((animation) => animation.animationName === "footer-fade-in");
    const glintAnimation = element
      .getAnimations({ subtree: true })
      .find((animation) => animation.animationName === "footer-glint");
    return {
      fade: [
        footer.animationName,
        footer.animationDuration,
        footer.animationDelay,
      ],
      glint: [
        glint.animationName,
        glint.animationDuration,
        glint.animationDelay,
        glint.animationIterationCount,
        glint.pointerEvents,
      ],
      fadeFrames: animation.effect.getKeyframes().map((frame) => frame.opacity),
      glintFrames: glintAnimation.effect
        .getKeyframes()
        .map((frame) => frame.backgroundPositionX),
    };
  });
  expect(motion.fade).toEqual(["footer-fade-in", "0.9s", "0.4s"]);
  expect(motion.glint).toEqual(["footer-glint", "2.4s", "1.5s", "1", "none"]);
  expect(motion.fadeFrames).toEqual(["0", "1"]);
  expect(motion.glintFrames).toEqual(["-30%", "130%"]);
  await page.emulateMedia({ reducedMotion: "reduce" });
  expect(
    await page
      .getByRole("contentinfo")
      .evaluate((element) => [
        getComputedStyle(element).animationName,
        getComputedStyle(element, "::after").animationName,
        getComputedStyle(element.querySelector(".footer-projects"))
          .transitionProperty,
      ]),
  ).toEqual(["none", "none", "none"]);
  await expect(page.getByRole("contentinfo")).toHaveCSS("opacity", "1");
});

test("projects text and SVG glow on hover or keyboard focus and activate the same link", async ({
  page,
  browserName,
}) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  const projects = page.getByRole("link", { name: "More projects? Moorkyz" });
  await projects.scrollIntoViewIfNeeded();
  await page.mouse.move(0, 0);
  await expect(projects).toHaveCSS("text-shadow", "none");
  await expect(projects).toHaveCSS("filter", "none");
  await projects.hover();
  await expect(projects).not.toHaveCSS("text-shadow", "none");
  await expect(projects).not.toHaveCSS("filter", "none");
  await page.mouse.move(0, 0);
  await expect(projects).toHaveCSS("text-shadow", "none");
  const creditLink = page
    .getByRole("contentinfo")
    .getByRole("link", { name: "Moorkyz", exact: true });
  await creditLink.focus();
  await page.keyboard.press("Tab");
  // WebKit's default platform preference skips links during Tab navigation.
  if (browserName === "webkit") await projects.focus();
  await expect(projects).toBeFocused();
  await expect(projects).not.toHaveCSS("text-shadow", "none");
  await expect(projects).toHaveCSS("outline-style", "solid");
  await expect(projects).toHaveCSS("outline-color", "rgb(255, 255, 255)");

  await page.route("https://moorkyz.com/projects", (route) =>
    route.fulfill({
      contentType: "text/html",
      body: "<title>Projects destination</title>",
    }),
  );
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL("https://moorkyz.com/projects");
  await page.goto("/");
  await page.locator(".footer-projects svg").click();
  await expect(page).toHaveURL("https://moorkyz.com/projects");
});
