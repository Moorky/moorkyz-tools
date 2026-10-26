"use client";

import { ThemeToggle } from "./portal-shell";
import { PortalSearch } from "./portal-search";

export function SiteHeader() {
  return (
    <header className="site-header">
      <a className="brand" href="/#home">
        <img
          className="header-logo"
          src="/moorkyz-tools.svg"
          width={64}
          height={64}
          alt=""
        />
        <span>
          <strong>Moorkyz Tools</strong>
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
