import videoCrunchFiles from "../../../../tools/videocrunch.files.json" with { type: "json" };
import cleanerFiles from "../../../../tools/directory-cleaner.files.json" with { type: "json" };
import mediaRecoverFiles from "../../../../tools/mediarecover.files.json" with { type: "json" };
import visoryFiles from "../../../../tools/visory-bulk-downloader.files.json" with { type: "json" };

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
        text: "No public executable release is available yet. Build the canonical source in tools/mediarecover within this repository or use a locally verified portable package supplied by its maintainer. Extract the entire package before starting MoorkyzMediaRecover.exe; do not run source text in this viewer.",
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
        "Canonical source maintained in tools/mediarecover in Moorky/moorkyz-tools. Edit it in this repository, then refresh SOURCE-PROVENANCE.json to record the allowlisted files' SHA-256 and source tree hash. The ZIP contains buildable source, not a public executable release.",
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
  {
    slug: "visory-bulk-downloader",
    id: "moorkyz-tools-visory-bulk-downloader",
    title: "Moorkyz Visory Bulk Downloader",
    category: "CLOUD RECORDING BACKUP",
    monogram: "vb",
    version: "0.1.0 development preview",
    releaseDate: null,
    updatedOn: "2026-10-26",
    summary:
      "Back up the cloud recordings your own Visory account can access to a local folder, with resumable background transfers, exact reports and a verified ZIP.",
    description:
      "An unofficial Windows desktop hub for backing up cloud recordings accessible from your own Visory account. You sign in through the official Visory web app inside an embedded Microsoft Edge WebView2 window; the hub reads only the recording metadata that app loads, shows the discovered count and scan completeness before downloading, then transfers with Windows BITS or resumable streaming downloads, verifies every file and writes exact logs, CSV reports and an optional ZIP. This page explains the desktop tool; it cannot sign in to Visory or download recordings.",
    howItWorks: [
      {
        title: "Sign in",
        text: "Open the official Visory web app inside the hub. Your password goes only to Visory; the session stays in your Windows profile, protected by Windows encryption.",
      },
      {
        title: "Scan",
        text: "Open each camera's cloud recordings, scroll or use Auto-scroll, then finish the scan. Only metadata from Visory and its own Firebase project is read; media bodies are never inspected.",
      },
      {
        title: "Review",
        text: "Check discovered recordings, cameras, already verified, missing and corrupt files, estimated size and free space. Without proof of completeness, the hub says discovered recordings and warns instead of claiming everything was found.",
      },
      {
        title: "Back up",
        text: "Start the backup and minimize or close the window; it continues in the notification area with pause, resume and safe exit.",
      },
      {
        title: "Verify",
        text: "Each file is size-checked and hashed with SHA-256 before an atomic rename. A changed file is preserved and downloaded again in a later run, never overwritten.",
      },
      {
        title: "Archive and report",
        text: "Read the run summary, text log, CSV report and failed report, and optionally keep one verified ZIP64 archive with a manifest that lists anything not included.",
      },
    ],
    features: [
      {
        title: "Honest discovery",
        text: "Passive observation of Firebase Storage listings and metadata, Realtime Database frames, Firestore documents and media the app plays. Unknown dates stay eligible, with their provenance labeled.",
      },
      {
        title: "Reliable transfer",
        text: "Windows BITS for compatible links, otherwise streaming downloads with validated Range resume, Retry-After, jittered retries, authorization refresh, network waits and a disk-space pause.",
      },
      {
        title: "Integrity & state",
        text: "SQLite state shipped with Windows, one final result per file per run, restart reconciliation, collision-free names and server MD5 checks when available.",
      },
      {
        title: "Reports & archives",
        text: "The exact SUCCESS/FAIL log format, Excel-safe UTF-8 CSV reports with redacted URLs, a JSON summary and streaming ZIP64 archives verified by re-reading.",
      },
      {
        title: "Privacy & background",
        text: "Local-first: no telemetry, uploads or plaintext passwords. Tokens stay in memory, stored links are encrypted with Windows DPAPI, and sign-out deletes the embedded browser profile.",
      },
    ],
    limitations: [
      "Unofficial and not affiliated with Visory. Visory publishes no recordings API, so discovery depends on what its web app loads; completeness is reported as not confirmable at best.",
      "Real Visory sign-in, discovery, downloads, link renewal, scheduled scans and BITS continuation after exit were not tested with an account. Automated tests use a loopback server, a mock provider and a clearly labeled demo mode.",
      "Unreleased development preview without a signed EXE or installer. Unsigned builds may be blocked by SmartScreen, Smart App Control or WDAC; on the development machine Application Control blocked several local builds.",
      "Requires Windows 10/11 x64 and the Microsoft Edge WebView2 Runtime for sign-in, which is not bundled. Direct downloads stop when the app exits and resume on the next start; BITS is used only for compatible links without sign-in headers.",
    ],
    useCases: [
      "Keep an independent local copy of the cloud recordings your account can access.",
      "Back up only new or missing recordings on a schedule after the first full run.",
      "Resume a large backup after a network outage, sleep or restart.",
      "Prove which recordings were saved, skipped or failed with exact reports.",
      "Hand off one verified ZIP archive with a manifest of every recording in scope.",
    ],
    setup: [
      {
        title: "Check Windows prerequisites",
        text: "Windows 10/11 x64 with the Microsoft Edge WebView2 Runtime, which is preinstalled on Windows 11. The portable EXE bundles .NET; building from source requires the .NET 10 SDK. Nothing is installed by visiting this page.",
      },
      {
        title: "Build the portable EXE",
        text: "No public executable release exists yet. From tools/visory-bulk-downloader in this repository, build one self-contained VisoryBulkDownloader.exe with notices and documentation.",
        command: ".\\scripts\\publish.ps1",
      },
      {
        title: "Try demo mode first",
        text: "Demo mode generates clearly labeled synthetic recordings in a separate folder to show scanning, backup, reports and archives without a Visory account.",
      },
      {
        title: "Sign in, scan and review",
        text: "Choose Sign in & scan, open your cloud recordings in Visory and finish the scan. Review the inventory and warnings, then start the backup. Back up a small date range first to confirm results with your account.",
      },
      {
        title: "Find files and reports",
        text: "Recordings, archives and per-run reports live in the chosen backup folder (default Videos\\Visory Backup). Settings stay beside the EXE when its folder is writable; the sign-in session always stays in your Windows profile.",
      },
    ],
    downloadNotice:
      "Not yet released. There is no public EXE, installer or signed build. Downloads in the Tool section are text source, not an executable release; a local build is not a published release. Nothing is installed, launched or connected by opening this page.",
    source: {
      bundle: "visory-bulk-downloader",
      files: visoryFiles,
      notice:
        "Canonical source maintained in tools/visory-bulk-downloader in Moorky/moorkyz-tools. Edit it in this repository, then refresh SOURCE-PROVENANCE.json to record the allowlisted files' SHA-256 and source tree hash. The ZIP contains buildable source, not a public executable release.",
    },
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
