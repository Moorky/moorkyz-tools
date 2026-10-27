import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

if (
  process.argv.length > 3 ||
  (process.argv[2] && process.argv[2] !== "--check")
)
  throw new Error(
    "Usage: node src\\scripts\\development\\readme.mjs [--check]",
  );

const root = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
);
const source = await readFile(path.join(root, "README.md"), "utf8");
const css = (
  await readFile(
    path.join(root, "src", "utils", "documents", "document.css"),
    "utf8",
  )
)
  .replace(/\r\n/g, "\n")
  .replace(
    "__DM_MONO__",
    (
      await readFile(
        path.join(
          root,
          "src",
          "utils",
          "documents",
          "fonts",
          "DMMono-Regular.ttf.b64",
        ),
        "utf8",
      )
    ).trim(),
  );
const escape = (text) =>
  text
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
const inline = (text) =>
  escape(text)
    .replace(/`([^`]+)`/g, "<code>$1</code>")
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, label, url) => {
      if (/^(?:[a-z]+:|\/\/)/i.test(url) && !url.startsWith("https://"))
        throw new Error("Unsafe README link.");
      return `<a href="${url}">${label}</a>`;
    });
const headings = [];
const html = [];
let paragraph = [],
  code = null,
  list = false;
const flush = () => {
  if (paragraph.length) {
    html.push(`<p>${inline(paragraph.join(" "))}</p>`);
    paragraph = [];
  }
  if (list) {
    html.push("</ul>");
    list = false;
  }
};
for (const line of source.split(/\r?\n/)) {
  if (line.startsWith("```")) {
    if (code !== null) {
      html.push(`<pre><code>${escape(code.join("\n"))}</code></pre>`);
      code = null;
    } else {
      flush();
      code = [];
    }
  } else if (code !== null) code.push(line);
  else if (/^#{1,3} /.test(line)) {
    flush();
    const level = line.indexOf(" ");
    const text = line.slice(level + 1);
    const id = text
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-|-$/g, "");
    if (level === 2) headings.push({ text, id });
    html.push(`<h${level} id="${id}">${inline(text)}</h${level}>`);
  } else if (line.startsWith("- ")) {
    if (paragraph.length) {
      html.push(`<p>${inline(paragraph.join(" "))}</p>`);
      paragraph = [];
    }
    if (!list) {
      html.push("<ul>");
      list = true;
    }
    html.push(`<li>${inline(line.slice(2))}</li>`);
  } else if (!line.trim()) flush();
  else paragraph.push(line.trim());
}
if (code !== null) throw new Error("Unclosed README code block.");
flush();
const hash = createHash("sha256").update(css).digest("base64");
const output = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; font-src data:; style-src 'sha256-${hash}'; base-uri 'none'; form-action 'none'">
<title>Moorksz Visory Media Downloader</title><style>${css}</style></head>
<body><a class="skip-link" href="#main">Skip to content</a><main class="document" id="main">
<nav aria-label="Document navigation"><a href="README.md">Markdown source</a>${headings.map(({ text, id }) => `<a href="#${id}">${escape(text)}</a>`).join("")}</nav>
${html.join("\n")}
<footer>Local documentation. No server, scripts, fonts or external assets are loaded. Visual direction inspired by the main moorkyz-template README.</footer>
</main></body></html>
`;
const target = path.join(root, "README.html");
if (process.argv.includes("--check")) {
  if ((await readFile(target, "utf8")) !== output)
    throw new Error(
      "README.html is stale. Run node src\\scripts\\development\\readme.mjs.",
    );
  console.log(
    "PASS README.html matches README.md and the shared document stylesheet.",
  );
} else {
  await writeFile(target, output);
  console.log("Generated local README.html; no website created.");
}
