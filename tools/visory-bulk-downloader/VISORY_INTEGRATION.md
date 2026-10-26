# Visory integration

Visory does not publish a recordings API or a bulk-export feature. This app therefore
does **not** call invented Visory endpoints. It hosts the official web app
(`https://app.visory.cam/`) in Microsoft Edge WebView2 and passively reads the metadata the
app itself loads while you browse your recordings.

## Verified facts (public, checked 2026-10-26)

From the public bootstrap of `https://app.visory.cam/` (no sign-in):

- The web app is a Flutter web application using the Firebase JS SDK (`firebase-app-compat`,
  `firebase-auth-compat` 10.12.2) and initializes the default Firebase app globally.
- Firebase project `visory-54ab5`; Realtime Database
  `visory-54ab5-default-rtdb.europe-west1.firebasedatabase.app`; Storage bucket
  `visory-54ab5.appspot.com` (`firebase-config.js`).
- The page loads `normalize-webp-video-file.js`, which handles WebM (EBML) and fragmented
  MP4 recordings produced in the browser, so recordings may be `.webm` or `.mp4`.
- The page also loads third-party scripts (PostHog analytics, unpkg, cdnjs). Their traffic
  is never inspected by this app.

The embedded sign-in window was opened on the development machine and rendered the
official Visory onboarding/login screen. **No account was signed in.**

## Unverified (requires an authorized test session)

- Where recording metadata lives (Realtime Database, Firestore, Storage listing or a
  Visory host), its exact shape, and how the app paginates.
- Whether recordings are stored in Firebase Storage and whether their URLs carry download
  tokens, signed parameters or require an `Authorization: Firebase <ID token>` header.
- Whether `firebase.auth().currentUser` exposes the session used by the Flutter plugins.
- Byte-range and HEAD behavior of real media URLs (BITS eligibility, resume).
- Whether scheduled unattended rescans can reproduce the interactive navigation.

Until these are confirmed with your own account, treat real-account behavior as a
development preview and check the inventory, warnings and reports of each run.

## Observation contract

`DiscoverySession` (Core, pure, unit-tested with captured-shape fixtures):

| Source | Accepted when | Extracted |
|---|---|---|
| HTTP JSON responses | origin matches the metadata allowlist below, `application/json`-like type, ≤ 8 MB | recordings via the recognizers below |
| Realtime Database WebSocket frames (DevTools `Network.webSocketFrameReceived`) | socket URL matches the allowlist; multi-frame messages reassembled | data pushes (`a: d/m`) walked with their database path |
| Media responses the app plays | media origin allowlisted and `video/*`/`audio/*` or media extension | URL, size (200 only), object path; **body never read** |

Metadata allowlist: `*.visory.cam`, `*.visory.app`, the project Realtime Database host,
`firebasestorage.googleapis.com/v0/b/visory-54ab5.appspot.com/…`,
`storage.googleapis.com` paths of that bucket, `firestore.googleapis.com/v1/projects/visory-54ab5/…`,
`visory-54ab5.firebaseapp.com`, `visory-54ab5.web.app`. Media allowlist: Visory hosts and
the project bucket on Firebase/Google Cloud Storage. Everything else, including other
buckets, IP hosts, non-HTTPS and non-standard ports, is rejected.

Recognizers:

1. **Firebase Storage list** (`items[]`, `nextPageToken`): media object names become
   recordings; announced page tokens must be followed by a request with `pageToken=`.
2. **Firebase Storage object metadata** (`bucket`, `name`, `contentType`/`size`/
   `downloadTokens`): size, `timeCreated` (labeled *upload time*), `md5Hash` and a
   tokenized download URL (`…/o/<name>?alt=media&token=…`, the documented format).
3. **Firestore REST documents**: typed values flattened, document ID used as recording ID.
4. **Generic recording objects** anywhere in JSON: an object with a media URL
   (media extension or `alt=media`) or a media storage path. IDs come from ID fields, the
   database key, the object path or, as a last resort, a metadata fingerprint. Camera,
   timestamp (epoch s/ms/µs or ISO), size, name and type come from common field names or
   path segments after `cameras/`/`devices/`. Thumbnails/preview fields are ignored.
5. **Filename timestamps** only when no remote time exists, labeled with their
   provenance (`filename (time zone assumed local)` or epoch milliseconds). Local file
   modification times are never used.

Recordings seen through multiple sources merge by object path.

## Completeness

- **Incomplete**: announced pages never loaded, metadata requests failed, the scan was
  canceled, or no metadata was observed.
- **Uncertain** (shown as "Not confirmable"): all observed pages loaded, but Visory exposes
  no total count. The hub shows **Discovered recordings** and a warning; runs end as
  *Completed – the Visory inventory could not be confirmed complete*, never as a full
  confirmed backup.
- **Complete**: only providers that can prove completeness (demo/mock) report it.

## Authorization and refresh

- Tokenized/signed URLs are used directly and stored DPAPI-encrypted.
- For storage objects without an embedded token the provider uses the user's own Firebase
  ID token, kept **in memory only**, captured from (a) `firebase.auth().currentUser.getIdToken()`
  evaluated in the page via DevTools, (b) `Authorization: Firebase/Bearer` headers the
  app sends to the project's Storage/Firestore, or (c) the Realtime Database auth frame.
- On HTTP 401/403 the provider requests the object's metadata with the ID token to obtain
  a fresh download token. If no valid token exists, a hidden WebView2 window reloads the
  app with the saved session to refresh it; failing that, the engine pauses with
  **Sign-in required**.
- Links without an object path cannot be renewed; the file fails with a message to scan
  again.

## Diagnostics

With **Settings → Privacy → Provider diagnostics** enabled, the scan window shows a
redacted summary: observed response count, pages, errors, ignored origins and the first
recordings with redacted URLs. It never shows tokens, cookies or query strings.

## Validating with your account

1. Build or download the EXE and run **Sign in & scan**.
2. Open each camera's cloud recordings and scroll (or **Auto-scroll list**) until the
   counter stops increasing; enable diagnostics if nothing is found.
3. Compare the discovered count with what the Visory app shows per camera.
4. Back up a small date range first (Settings → Recording filters) and open a few files.
5. Report shapes that are not recognized using the redacted diagnostics only.
