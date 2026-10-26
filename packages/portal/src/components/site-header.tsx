"use client";

import { ThemeToggle } from "./portal-shell";
import { PortalSearch } from "./portal-search";

export function SiteHeader() {
  return (
    <header className="site-header">
      <a className="brand" href="/#home">
        <img
          className="header-logo"
          src="/tools.moorkyz.com-mark.png"
          width={112}
          height={56}
          alt=""
        />
        <span>
          <strong>tools.moorkyz.com</strong>
          <small
            className="brand-id"
            id="moorkyz-tools-name"
            aria-hidden="true"
          >
            moorkyz-tools-name
          </small>
        </span>
      </a>
      <nav className="header-links" aria-label="Main navigation">
        <a href="/#tools">Tools</a>
        <a className="header-aux-link" href="/contributing">
          Contributing
        </a>
        <a
          href="https://github.com/Moorky/moorkyz-tools"
          className="header-aux-link"
          target="_blank"
          rel="noreferrer"
        >
          GitHub
        </a>
        <PortalSearch />
        <ThemeToggle />
      </nav>
    </header>
  );
}
