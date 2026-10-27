import type { Metadata } from "next";
import { SiteHeader } from "../../components/site-header";
import { SiteFooter } from "../../components/site-footer";

export const metadata: Metadata = {
  title: "Contributing",
  alternates: { canonical: "/contributing" },
};

export default function ContributingPage() {
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <SiteHeader />
      <main
        id="main-content"
        className="section-shell website-section"
        tabIndex={-1}
      >
        <div className="compact-section-heading">
          <p className="eyebrow">OPEN SOURCE · APACHE-2.0</p>
          <h1 className="contribution-title">Build something thoughtful.</h1>
          <p>
            tools.moorkyz.com welcomes focused, tested improvements to
            independent tools and the shared portal. Publishing and deployment
            require separate authorization.
          </p>
        </div>
        <h2>Work locally</h2>
        <p>Use Node.js 22.18+ and the pnpm version pinned in the repository.</p>
        <pre
          className="contribution-commands"
          tabIndex={0}
          aria-label="Contributor verification commands"
        >
          <code>
            {
              "pnpm --dir src install --frozen-lockfile\npnpm --dir src --filter @moorkyz-tools/portal dev\npnpm --dir src check\npnpm --dir src check:source"
            }
          </code>
        </pre>
        <h2>Keep changes focused</h2>
        <ol>
          <li>
            Read the repository&apos;s agent contract, decision register and
            relevant tool guides before changing behavior.
          </li>
          <li>
            Preserve mandatory accessibility, local-first data handling,
            archive-only cleanup and disabled-service inactivity.
          </li>
          <li>
            Add observable tests; run the relevant gates and report real
            evidence, failures and limitations.
          </li>
          <li>
            Include a task-scoped audit. Do not mix unrelated changes or
            silently overwrite developer-owned source.
          </li>
          <li>
            Propose a focused pull request. Do not publish credentials or
            exploitable security details in public issues.
          </li>
        </ol>
        <p>
          DM Sans and DM Mono are separately licensed under SIL OFL 1.1; retain
          the included font notices. Automated tests do not replace independent
          security review or manual accessibility/device acceptance.
        </p>
        <p>
          <a href="https://github.com/Moorky/moorkyz-tools">
            Open the repository ↗
          </a>{" "}
          · <a href="/#tools">Explore the tools</a>
        </p>
      </main>
      <SiteFooter />
    </>
  );
}
