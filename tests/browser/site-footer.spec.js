import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test("shared footer preserves credits and has text-only contact and projects links on every route", async ({
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
      name: "More projects?",
      exact: true,
    });
    await expect(projects).toHaveAttribute(
      "href",
      "https://moorkyz.com/projects",
    );
    await expect(projects).toHaveText("More projects?");
    await expect(projects.locator("svg, img")).toHaveCount(0);
    await expect(footer.locator("nav svg, nav img")).toHaveCount(0);
    await expect(footer.locator(".footer-note svg")).toHaveCount(1);
    await expect(
      footer.getByRole("link", { name: "Stay in Touch?", exact: true }),
    ).toHaveAttribute("href", "https://moorkyz.com/contact");
    await expect(
      footer.getByRole("link", { name: "Evidence workspace", exact: true }),
    ).toHaveCount(0);
    await expect(
      footer.getByRole("link", { name: "Moorkyz", exact: true }),
    ).toHaveAttribute("href", "https://moorkyz.com");
  }
});

test("footer keeps its compact height with all three content groups raised 18 percent", async ({
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
        const contact = element.querySelector("nav a");
        const rect = (node) => node.getBoundingClientRect().toJSON();
        return {
          footer: rect(element),
          projects: rect(projects),
          credit: rect(credit),
          contact: rect(contact),
          projectsFont: getComputedStyle(projects).fontSize,
          contactFont: getComputedStyle(contact).fontSize,
          upwardRatios: [...element.children].map(
            (child) =>
              -new DOMMatrixReadOnly(getComputedStyle(child).transform).m42 /
              child.getBoundingClientRect().height,
          ),
          position: getComputedStyle(element).position,
          background: getComputedStyle(element).backgroundImage,
          fits: document.documentElement.scrollWidth <= innerWidth,
        };
      });
      expect(1 - metrics.footer.height / 69.6).toBeCloseTo(0.2, 2);
      expect(metrics.position).toBe("relative");
      expect(metrics.background).toContain("rgb(3, 6, 14)");
      expect(metrics.fits).toBe(true);
      expect(metrics.credit.x + metrics.credit.width / 2).toBeCloseTo(
        width / 2,
        0,
      );
      expect(metrics.footer.right - metrics.projects.right).toBeCloseTo(16, 0);
      expect(metrics.footer.bottom - metrics.projects.bottom).toBeCloseTo(
        6.32,
        1,
      );
      expect(metrics.upwardRatios).toHaveLength(3);
      for (const shift of metrics.upwardRatios) {
        expect(shift).toBeGreaterThanOrEqual(0.15);
        expect(shift).toBeLessThanOrEqual(0.2);
        expect(shift).toBeCloseTo(0.18, 2);
      }
      expect(metrics.credit.top).toBeGreaterThan(metrics.footer.top);
      expect(metrics.projects.height).toBeGreaterThanOrEqual(24);
      expect(metrics.projects.y).toBeGreaterThanOrEqual(
        width <= 700 ? metrics.credit.bottom : metrics.credit.y,
      );
      expect(metrics.contact.y).toBeCloseTo(metrics.projects.y, 1);
      expect(metrics.contact.height).toBeCloseTo(metrics.projects.height, 1);
      expect(metrics.contactFont).toBe(metrics.projectsFont);
      expect(metrics.contact.x - metrics.footer.x).toBeCloseTo(16, 0);
      expect(metrics.contact.right).toBeLessThanOrEqual(metrics.projects.x);
      expect(metrics.projects.bottom - metrics.credit.bottom).toBeGreaterThan(
        0,
      );
      if (width > 700) {
        expect(metrics.contact.right).toBeLessThan(metrics.credit.x);
        expect(metrics.credit.right).toBeLessThan(metrics.projects.x);
        expect(metrics.footer.bottom - metrics.credit.bottom).toBeCloseTo(
          12.32,
          1,
        );
      }
      for (const link of await footer.locator(".footer-link").all()) {
        await expect(link).toHaveCSS("color", "rgb(255, 255, 255)");
        await expect(link).toHaveCSS("text-decoration-line", "none");
      }
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
        ...[...element.querySelectorAll(".footer-link")].map(
          (link) => getComputedStyle(link).transitionProperty,
        ),
      ]),
  ).toEqual(["none", "none", "none", "none"]);
  await expect(page.getByRole("contentinfo")).toHaveCSS("opacity", "1");
});

test("both side links have matching hover and keyboard glow and navigate by text or Enter", async ({
  page,
  browserName,
}) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  const footer = page.getByRole("contentinfo");
  const projects = footer.getByRole("link", { name: "More projects?" });
  const contact = footer.getByRole("link", { name: "Stay in Touch?" });
  const glows = [];
  for (const link of [contact, projects]) {
    await link.scrollIntoViewIfNeeded();
    await page.mouse.move(0, 0);
    await expect(link).toHaveCSS("text-shadow", "none");
    await expect(link).toHaveCSS("filter", "none");
    await link.hover();
    await expect(link).not.toHaveCSS("text-shadow", "none");
    await expect(link).not.toHaveCSS("filter", "none");
    glows.push(
      await link.evaluate((element) => [
        getComputedStyle(element).textShadow,
        getComputedStyle(element).filter,
      ]),
    );
    await page.mouse.move(0, 0);
    await expect(link).toHaveCSS("text-shadow", "none");
    await page.keyboard.press("Tab");
    await link.focus();
    await expect(link).not.toHaveCSS("text-shadow", "none");
    await expect(link).toHaveCSS("outline-style", "solid");
    await expect(link).toHaveCSS("outline-color", "rgb(255, 255, 255)");
    await link.blur();
  }
  expect(glows[0]).toEqual(glows[1]);
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

  await page.route("https://moorkyz.com/{projects,contact}", (route) =>
    route.fulfill({
      contentType: "text/html",
      body: "<title>Projects destination</title>",
    }),
  );
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL("https://moorkyz.com/projects");
  await page.goto("/");
  await page.getByRole("link", { name: "More projects?" }).click();
  await expect(page).toHaveURL("https://moorkyz.com/projects");
  await page.goto("/");
  await page.getByRole("link", { name: "Stay in Touch?" }).click();
  await expect(page).toHaveURL("https://moorkyz.com/contact");
  await page.goto("/");
  await page.getByRole("link", { name: "Stay in Touch?" }).focus();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL("https://moorkyz.com/contact");
});
