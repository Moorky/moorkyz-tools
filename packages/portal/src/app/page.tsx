import type { Metadata } from "next";
import { stat } from "node:fs/promises";
import path from "node:path";
import { SiteHeader } from "../components/site-header";
import { SiteFooter } from "../components/site-footer";
import { TOOLS, formatToolDate } from "../data/tools";

export const metadata: Metadata = {
  title: "A little more useful. — Moorkyz Tools",
  alternates: { canonical: "/" },
  description:
    "A library of tools I never planned to build - until they became essential. Small, independent tools. Real lifesavers.",
};

export default async function HomePage() {
  const cards = await Promise.all(
    TOOLS.map(async (tool) => ({
      tool,
      sourceBytes: tool.source
        ? (
            await stat(
              path.join(
                process.cwd(),
                "public",
                "tool-sources",
                `${tool.source.bundle}.zip`,
              ),
            )
          ).size
        : null,
    })),
  );
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <SiteHeader />
      <main id="main-content" tabIndex={-1} className="tools-home">
        <section id="home" className="section-shell catalog-hero" tabIndex={-1}>
          <h1>A little more useful.</h1>
          <p className="hero-lede">
            A library of tools I never planned to build — until they became
            essential.
          </p>
        </section>
        <section
          id="tools"
          className="section-shell tool-catalog"
          aria-labelledby="tools-heading"
          tabIndex={-1}
        >
          <div className="catalog-heading">
            <div>
              <p className="eyebrow">PICK YOUR NEXT TOOL</p>
              <h2 id="tools-heading">Small tools. Real lifesavers.</h2>
            </div>
            <p>{TOOLS.length} tools, one workbench.</p>
          </div>
          <div className="tool-grid">
            {cards.map(({ tool, sourceBytes }) => (
              <article className="tool-card" key={tool.slug}>
                <div className="tool-card-top">
                  <span className="tool-category">{tool.category}</span>
                  <span className="tool-version">v{tool.version}</span>
                </div>
                <h3>
                  <a href={`/tools/${tool.slug}`}>{tool.title}</a>
                </h3>
                <small className="tool-id">{tool.id}</small>
                <p className="tool-card-description">
                  {tool.catalogDescription}
                </p>
                <dl className="tool-card-bottom">
                  <div>
                    <dt>Release date</dt>
                    <dd>
                      {tool.releaseDate ? (
                        <time dateTime={tool.releaseDate}>
                          {formatToolDate(tool.releaseDate)}
                        </time>
                      ) : (
                        "Not publicly released"
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt>Interested in more?</dt>
                    <dd>
                      <a
                        href="https://moorkyz.com/contact"
                        aria-label={`Get in touch about ${tool.title}`}
                      >
                        Get in touch
                      </a>
                    </dd>
                  </div>
                  <div>
                    <dt>File size</dt>
                    <dd
                      title={
                        sourceBytes !== null
                          ? `${sourceBytes.toLocaleString("en")} bytes`
                          : undefined
                      }
                    >
                      {sourceBytes !== null
                        ? `${(sourceBytes / 1024).toFixed(1)} KiB source ZIP`
                        : tool.workspace === "toolbar"
                          ? "Up to 8 KiB HTML"
                          : "No public download"}
                    </dd>
                  </div>
                  <div>
                    <dt>Usefulness (editorial)</dt>
                    <dd>{tool.usefulness}/10</dd>
                  </div>
                </dl>
              </article>
            ))}
          </div>
        </section>
        <section
          id="about-tools"
          className="section-shell catalog-note"
          tabIndex={-1}
        >
          <p className="eyebrow">ROOM FOR WHAT COMES NEXT</p>
          <h2>A growing collection. Not a crowded toolbox.</h2>
          <p>
            Every tool has its own purpose, workspace and setup. Choose what you
            need; the rest stays out of your way. No accounts, tracking or
            automatic connections to external services.
          </p>
        </section>
      </main>
      <SiteFooter />
    </>
  );
}
