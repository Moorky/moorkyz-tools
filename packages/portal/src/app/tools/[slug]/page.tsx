import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { headers } from "next/headers";
import { SiteHeader } from "../../../components/site-header";
import { SiteFooter } from "../../../components/site-footer";
import { LocalToolWorkspace } from "../../../components/local-tool-workspace";
import { ToolSourceBrowser } from "../../../components/tool-source-browser";
import { CompactToolbarBuilder } from "../../../components/compact-toolbar-builder";
import { DirectoryCleaner } from "../../../components/directory-cleaner";
import { findTool, formatToolDate } from "../../../data/tools";

type Props = { readonly params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const tool = findTool((await params).slug);
  if (!tool) notFound();
  return {
    title: tool.title,
    description: tool.summary,
    alternates: { canonical: `/tools/${tool.slug}` },
  };
}

export default async function ToolPage({ params }: Props) {
  const tool = findTool((await params).slug);
  if (!tool) notFound();
  const nonce = (await headers()).get("x-nonce") ?? "";
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <SiteHeader />
      <main
        id="main-content"
        className="tool-detail section-shell"
        tabIndex={-1}
      >
        <a className="text-link" href="/#tools">
          ← All tools
        </a>
        <header className="tool-detail-heading">
          <span className="tool-monogram" aria-hidden="true">
            {tool.monogram}
          </span>
          <p className="eyebrow">{tool.category}</p>
          <h1>{tool.title}</h1>
          <small className="tool-id">{tool.id}</small>
          <p className="hero-lede">{tool.summary}</p>
          <dl className="tool-metadata">
            <div>
              <dt>Release Date</dt>
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
              <dt>Updated On</dt>
              <dd>
                <time dateTime={tool.updatedOn}>
                  {formatToolDate(tool.updatedOn)}
                </time>
              </dd>
            </div>
            <div>
              <dt>Version</dt>
              <dd>{tool.version}</dd>
            </div>
          </dl>
        </header>
        <section
          id="description"
          aria-labelledby="description-heading"
          tabIndex={-1}
        >
          <h2 id="description-heading">Description</h2>
          <p>{tool.description}</p>
        </section>
        {(tool.source || tool.workspace === "toolbar") && (
          <section id="tool" aria-labelledby="tool-heading" tabIndex={-1}>
            <h2 id="tool-heading">Tool</h2>
            {tool.workspace === "toolbar" && (
              <CompactToolbarBuilder nonce={nonce} />
            )}
            {tool.source && (
              <ToolSourceBrowser
                key={tool.source.bundle}
                bundle={tool.source.bundle}
                files={tool.source.files}
              />
            )}
          </section>
        )}
        <section
          id="use-cases"
          aria-labelledby="use-cases-heading"
          tabIndex={-1}
        >
          <h2 id="use-cases-heading">Use Cases</h2>
          <ul>
            {tool.useCases.map((useCase) => (
              <li key={useCase}>{useCase}</li>
            ))}
          </ul>
        </section>
        <section id="setup" aria-labelledby="setup-heading" tabIndex={-1}>
          <h2 id="setup-heading">Setup</h2>
          <ol className="tool-setup">
            {tool.setup.map((step) => (
              <li key={step.title}>
                <h3>{step.title}</h3>
                <p>{step.text}</p>
                {step.command && (
                  <pre>
                    <code>{step.command}</code>
                  </pre>
                )}
              </li>
            ))}
          </ol>
        </section>
        {tool.workspace && tool.workspace !== "toolbar" && (
          <section
            id="workspace"
            aria-labelledby="workspace-heading"
            tabIndex={-1}
          >
            <h2 id="workspace-heading">Workspace</h2>
            {tool.workspace === "directory-cleaner" ? (
              <DirectoryCleaner
                enabled={
                  process.env.TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED ===
                  "1"
                }
              />
            ) : (
              <LocalToolWorkspace
                enabled={
                  process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED === "1"
                }
              />
            )}
          </section>
        )}
      </main>
      <SiteFooter />
    </>
  );
}
