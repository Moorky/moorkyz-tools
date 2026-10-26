import videoCrunchFiles from "../../../../tools/videocrunch.files.json" with { type: "json" };
import cleanerFiles from "../../../../tools/directory-cleaner.files.json" with { type: "json" };

export interface ToolDefinition {
  readonly slug: string;
  readonly id: string;
  readonly title: string;
  readonly category: string;
  readonly monogram: string;
  readonly version: string;
  readonly releaseDate: string | null;
  readonly updatedOn: string;
  readonly summary: string;
  readonly description: string;
  readonly useCases: readonly string[];
  readonly setup: readonly {
    readonly title: string;
    readonly text: string;
    readonly command?: string;
  }[];
  readonly workspace?: "toolbar" | "videocrunch" | "directory-cleaner";
  readonly source?: {
    readonly bundle: string;
    readonly files: readonly string[];
  };
}

export const TOOLS: readonly ToolDefinition[] = [
  {
    slug: "toolbar",
    id: "moorkyz-tools-toolbar-builder",
    title: "Moorkyz Toolbar Builder",
    category: "SMALL WEB TOOL",
    monogram: "tb",
    version: "1.0.0 local preview",
    releaseDate: null,
    updatedOn: "2026-10-23",
    summary:
      "Design, preview and export a tiny toolbar. A few links, a little style, one HTML file.",
    description:
      "A compact browser-only builder for up to eight navigation links. Pick light or dark appearance, an accent border and top or bottom placement, then copy or download one self-contained HTML file. No package, framework, account or service required.",
    useCases: [
      "Make a small navigation bar for a landing page or local utility.",
      "Try link labels and appearance without setting up a development project.",
      "Take portable, readable HTML/CSS with no JavaScript or runtime dependencies.",
    ],
    setup: [
      {
        title: "Edit the links",
        text: "Use the small builder above. Set labels and #section, /local-path or HTTP(S) destinations.",
      },
      {
        title: "Copy or download",
        text: "Open the HTML directly, or copy its nav and CSS into your own page. Generated output is at most 8 KiB; no installation is needed.",
      },
    ],
    workspace: "toolbar",
  },
  {
    slug: "videocrunch",
    id: "moorkyz-tools-video-crunch",
    title: "Moorkyz Video Crunch",
    category: "VIDEO WORKSPACE",
    monogram: "vc",
    version: "1.0.0 local preview",
    releaseDate: null,
    updatedOn: "2026-10-23",
    summary:
      "Save authorized videos, keep the original and create a smaller MP4 for offline playback.",
    description:
      "A Windows-first local web service for downloading and compressing videos you own or have permission to save. The complete original app, PowerShell exporter, launchers, tests and guides live in this repository. Original bytes are preserved alongside optional MP4 derivatives, an offline player and an integrity manifest. Public hosting, accounts and multi-user processing are off.",
    useCases: [
      "Keep authorized source videos and watch them offline.",
      "Create smaller playback copies with Balanced, Crunch or Extreme compression.",
      "Download just the original, inspect live logs and revisit the last 50 local exports.",
    ],
    setup: [
      {
        title: "Prepare Windows prerequisites",
        text: "Requires Node.js 22+, Windows PowerShell 5.1, yt-dlp and FFmpeg. The original launcher checks prerequisites and uses winget to install missing tools when you explicitly run it; installation may require approval and internet access.",
        command: ".\\tools\\videocrunch\\Start-WebVideoCrunch.cmd",
      },
      {
        title: "Start the local service",
        text: "If prerequisites are already available, use this instead of the launcher. Keep its terminal open. The service binds only to 127.0.0.1:8765.",
        command: "pnpm tool:videocrunch",
      },
      {
        title: "Enable the portal connection",
        text: "In a second PowerShell terminal, opt in and start the portal on loopback. The connection is disabled by default and never starts or installs the service itself.",
        command:
          '$env:TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = "1"\npnpm --filter @moorkyz-tools/portal dev --hostname 127.0.0.1',
      },
      {
        title: "Open your workspace",
        text: "Use the button below after both services are ready. Exports and history stay on this PC. Do not expose either service through a public address or reverse proxy. No DRM, login or paywall bypass is provided.",
      },
    ],
    workspace: "videocrunch",
    source: { bundle: "videocrunch", files: videoCrunchFiles },
  },
  {
    slug: "directory-cleaner",
    id: "moorkyz-tools-directory-cleaner",
    title: "Moorkyz Directory Cleaner",
    category: "LOCAL FILE UTILITY",
    monogram: "dc",
    version: "1.0.0 local preview",
    releaseDate: null,
    updatedOn: "2026-10-23",
    summary:
      "Keep, move or archive files using patterns, creation dates and size limits. Preview every change first.",
    description:
      "A local file organizer available as both a web-service workspace and a downloadable script. Match filename or relative-path patterns, combine creation-before/after and size-smaller/larger conditions, then Keep, Move or Delete to archive. Keep rules win; otherwise the first match applies, and unmatched files remain untouched. Nothing is permanently deleted or overwritten.",
    useCases: [
      "Keep important documents while archiving old or tiny temporary files.",
      "Move matching photos or downloads into another folder without overwriting existing files.",
      "Find large files created before a chosen date, review the exact plan and archive only after confirmation.",
    ],
    setup: [
      {
        title: "Use the downloadable script",
        text: "Requires Node.js 22+. Download the complete script ZIP from Tool or Workspace, extract it and download your settings from the rule editor. Preview does not change files.",
        command:
          "node .\\clean.mjs --settings .\\directory-cleaner-settings.json --plan .\\preview.json",
      },
      {
        title: "Apply a reviewed script preview",
        text: "Read preview.json and use its actual ID. Delete always puts files into an archive. The exact source snapshot is revalidated before any changes.",
        command:
          'node .\\clean.mjs --apply .\\preview.json --confirm "APPLY <preview-id>"',
      },
      {
        title: "Or start the local web service",
        text: "From this repository, run the service in its own terminal. It binds only to 127.0.0.1:8766; no account or installation runs automatically.",
        command: "pnpm tool:directory-cleaner",
      },
      {
        title: "Enable the portal connection",
        text: "In another terminal, opt in and bind the portal to loopback. Connect in Workspace, preview the files and type the exact confirmation. Both local-service connections are independent and off by default.",
        command:
          '$env:TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED = "1"\npnpm --filter @moorkyz-tools/portal dev --hostname 127.0.0.1',
      },
    ],
    workspace: "directory-cleaner",
    source: { bundle: "directory-cleaner", files: cleanerFiles },
  },
];

export function findTool(slug: string): ToolDefinition | undefined {
  return TOOLS.find((tool) => tool.slug === slug);
}

export function formatToolDate(value: string): string {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "long",
    timeZone: "UTC",
  }).format(new Date(`${value}T00:00:00Z`));
}
