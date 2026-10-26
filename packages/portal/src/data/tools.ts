import videoCrunchFiles from "../../../../tools/videocrunch.files.json" with { type: "json" };
import cleanerFiles from "../../../../tools/directory-cleaner.files.json" with { type: "json" };
import mediaRecoverFiles from "../../../../tools/mediarecover.files.json" with { type: "json" };

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
  readonly howItWorks?: readonly {
    readonly title: string;
    readonly text: string;
  }[];
  readonly features?: readonly {
    readonly title: string;
    readonly text: string;
  }[];
  readonly limitations?: readonly string[];
  readonly downloadNotice?: string;
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
    readonly notice?: string;
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
    slug: "mediarecover",
    id: "moorkyz-tools-mediarecover",
    title: "Moorkyz Media Recover",
    category: "LOCAL MEDIA RECOVERY",
    monogram: "mr",
    version: "0.1.0 development preview",
    releaseDate: null,
    updatedOn: "2026-10-26",
    summary:
      "Safely recover and preserve accessible video and audio from connected devices and local folders. Know exactly where every file was saved.",
    description:
      "An independent, offline-first Windows desktop application for recursively discovering accessible audio and video in local folders and authorized Android USB/MTP storage, including Samsung phones. Scan before copying, review date filters and duplicate candidates, then back up without changing originals. Interrupted files can be retried from the beginning, saved bytes checked with SHA-256, and results traced through detailed reports. This page explains the desktop tool; it cannot scan your computer or phone.",
    howItWorks: [
      {
        title: "Choose source",
        text: "Select local folders or an authorized, unlocked portable device, then choose an absolute save folder.",
      },
      {
        title: "Scan",
        text: "Discover media recursively before copying. Review inaccessible folders and partial scans instead of assuming every subtree was readable.",
      },
      {
        title: "Review",
        text: "Check eligible recordings, date exclusions, unknown dates and destination candidates. The default cutoff is October 1, 2026 inclusive; unknown or conflicting dates are included rather than silently lost.",
      },
      {
        title: "Recover",
        text: "Copy readable sources into staging without modifying originals or overwriting unrelated files. Interrupted MTP files restart from byte zero, not a claimed byte-range resume.",
      },
      {
        title: "Verify",
        text: "Check saved lengths and SHA-256, with verification confidence recorded in the manifest. A saved hash detects later byte changes; it does not prove a recording is playable.",
      },
      {
        title: "Open saved folder",
        text: "Read the exact Saved to path, open the saved files and reports, and review failures before considering the backup complete.",
      },
    ],
    features: [
      {
        title: "Discovery & filters",
        text: "Recursive video/audio discovery, documented date provenance, unknown-date inclusion and safe exclusions for backup output and filesystem links.",
      },
      {
        title: "Reliable transfer",
        text: "Read-only sources, collision-safe filenames, per-file staging and bounded retry. Device access depends on Windows, USB mode and phone authorization.",
      },
      {
        title: "Verification & repair",
        text: "Persistent hashes distinguish trusted saved bytes from legacy candidates. Suspect output is preserved, not silently overwritten; independent source comparison provides a different assurance level from local hashing.",
      },
      {
        title: "Reports & history",
        text: "Durable job records, per-file results, manifests and exact output locations make incomplete work and verification levels visible.",
      },
      {
        title: "Privacy & portability",
        text: "A separate Windows desktop runtime, without accounts, telemetry, uploads or a public processing service. The portal stays usable when the desktop app is not running.",
      },
    ],
    limitations: [
      "Recover means retrying incomplete transfers or reacquiring damaged backups while the source is still readable. It does not mean undeleting inaccessible phone data, bypassing encryption or extracting hidden/private app storage.",
      "Unlock the phone and authorize File Transfer / Android Auto. WPD access, reconnect behavior and metadata vary by device; real Samsung/Android hardware validation is not a release claim.",
      "Hashes establish byte consistency, not media decodability or authenticity. Background work requires the desktop process and accessible storage; it cannot continue through process exit or sleep.",
      "This is an unreleased development preview. Consult the included implementation status and testing documentation for verified behavior and deferred options before using important media.",
      "Runtime acceptance is blocked on the build machine: Windows Application Control rejected unsigned application DLLs (0x800711C7). A self-contained package was produced, but a successful GUI launch and final expanded test run could not be verified. No desktop screenshot or release-readiness claim is available.",
      "Automatic device reconnect/PUID remapping, embedded capture-date parsing, media decoding, cross-identity content deduplication, tray mode and complete technical-message translation are not implemented. Phone transfers and files larger than 4 GiB remain unverified.",
    ],
    useCases: [
      "Retry accessible phone recordings after an unstable USB transfer.",
      "Collect audio and video recursively from a deep local folder tree.",
      "Continue a partially completed backup without overwriting unrelated files.",
      "Check previously saved bytes for later corruption using recorded hashes.",
      "Export a traceable archive with per-file outcomes and saved locations.",
    ],
    setup: [
      {
        title: "Check Windows prerequisites",
        text: "Targets Windows 10/11 x64. A self-contained portable build includes its .NET runtime; building the source requires the .NET 10 SDK with Windows desktop targeting support. No prerequisite is installed by visiting this page.",
      },
      {
        title: "Prepare an Android source",
        text: "Use a reliable USB data cable, unlock the phone, select File Transfer / Android Auto and authorize this PC. Select accessible internal storage or SD storage; protected app data is not exposed by this tool.",
      },
      {
        title: "Start the desktop application",
        text: "No public executable release is available yet. Build the independently maintained moorkyz-tools-mediarecover source or use a locally verified portable package supplied by its maintainer. Extract the entire package before starting MoorkyzMediaRecover.exe; do not run source text in this viewer.",
      },
      {
        title: "Choose storage and review the scan",
        text: "Keep the source readable and choose a destination with enough space. Settings and the SQLite ledger live under %LOCALAPPDATA%\\Moorkyz\\MediaRecover. Staging, quarantine and per-run reports live under the destination's .mediarecover folder. Scan, review, then explicitly start recovery.",
      },
      {
        title: "Review the exact saved paths",
        text: "Use the desktop Saved to path and report actions. Keep quarantined files and original recordings until you have reviewed failures. Processing works offline, and no local browser service or portal opt-in is required.",
      },
    ],
    downloadNotice:
      "Not yet released. There is no public EXE or portable ZIP download. Downloads in the Tool section are text source, not an executable release; a local build is not a published release. Nothing is installed, launched or connected by opening this page.",
    source: {
      bundle: "mediarecover",
      files: mediaRecoverFiles,
      notice:
        "Pinned text source from the independent Moorky/moorkyz-tools-mediarecover project, not a second editable runtime. SOURCE-PROVENANCE.json records each original file's SHA-256 and the source tree hash. Refresh from that project rather than editing this snapshot. The ZIP contains buildable source, not a public executable release.",
    },
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
