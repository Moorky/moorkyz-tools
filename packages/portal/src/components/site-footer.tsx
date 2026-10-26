function FooterLogo() {
  return (
    <svg
      viewBox="0 0 512 512"
      className="footer-logo"
      aria-hidden="true"
      focusable="false"
    >
      <path
        fill="currentColor"
        d="M40 50c7 5 22 21 29 28l42 40 24 24 64 62c9 9 23 20 30 30l-24 25-19 18-24-23-34-32-22-21v120l1 8 9 8 20 13 41 28 60 41c-3 7-19 19-26 25-3 2-8 7-11 7l-10-5-22-13-77-45-36-21c-12-7-15-6-15-17V50Zm431 0c2 5 1 22 1 28v272c0 11-3 10-15 17l-36 21-77 45-22 13-10 5c-3 0-8-5-11-7-7-6-23-18-26-25l60-41 41-28 20-13 9-8 1-8V201c-6 4-13 12-19 18l-11 10-16 16-35 33-17 17-12 11-28 27c-2 2-9 10-12 10s-11-8-13-10l-31-31 35-35 31-29 24-24 9-8 24-24 10-9 23-23L471 50Z"
      />
    </svg>
  );
}

export function SiteFooter() {
  return (
    <footer className="website-footer">
      <nav aria-label="Related projects">
        <a
          className="footer-link"
          href="https://evidence.moorkyz.com"
          target="_blank"
          rel="noreferrer"
        >
          Evidence workspace
        </a>
      </nav>
      <p className="footer-note">
        <span>made /w</span>
        <FooterLogo />
        <span>© {new Date().getUTCFullYear()}</span>
        <a href="https://moorkyz.com">Moorkyz</a>
      </p>
      <a
        className="footer-link footer-projects"
        href="https://moorkyz.com/projects"
        aria-label="More projects? Moorkyz"
      >
        <span>More projects?</span>
        <FooterLogo />
      </a>
    </footer>
  );
}
