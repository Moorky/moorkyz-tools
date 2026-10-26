import type { Metadata } from "next";
import { SiteHeader } from "../components/site-header";
import { SiteFooter } from "../components/site-footer";
import { TOOLS } from "../data/tools";

export const metadata: Metadata = {
  title: "A little more useful. — Moorkyz Tools",
  alternates: { canonical: "/" },
  description:
    "Independent tools for building interfaces and working with video. Explore what each tool does, how to use it and how to get started.",
};

export default function HomePage() {
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
            Thoughtful tools. Less friction.
            <br />
            Find something that makes your next idea easier.
          </p>
          <a className="text-link" href="#tools">
            Explore the tools <span aria-hidden="true">↓</span>
          </a>
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
              <h2 id="tools-heading">Small tools. Real possibilities.</h2>
            </div>
            <p>{TOOLS.length} tools, one workbench.</p>
          </div>
          <div className="tool-grid">
            {TOOLS.map((tool) => (
              <article className="tool-card" key={tool.slug}>
                <div className="tool-card-top">
                  <span className="tool-monogram" aria-hidden="true">
                    {tool.monogram}
                  </span>
                  <span className="tool-category">{tool.category}</span>
                </div>
                <h3>
                  <a href={`/tools/${tool.slug}`}>
                    {tool.title}
                    <span className="tool-card-arrow" aria-hidden="true">
                      ↗
                    </span>
                  </a>
                </h3>
                <small className="tool-id">{tool.id}</small>
                <p>{tool.summary}</p>
                <div className="tool-card-bottom">
                  <span>{tool.version}</span>
                  <span>
                    Explore tool <span aria-hidden="true">→</span>
                  </span>
                </div>
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
