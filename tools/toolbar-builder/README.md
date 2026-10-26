# Moorky Toolbar Builder

A small independent HTML/CSS generator, not an SDK. `builder.mjs` validates up to
eight labeled links, light/dark appearance, accent border and top/bottom position.
The portal provides a compact live preview, Copy and one HTML download.

Output is one file, at most 8 KiB, with no JavaScript, framework, package import,
external font, runtime dependency or automatic network request. Labels/attributes
are escaped. Links allow `#section`, `/local-path` or credential-free HTTP(S);
JavaScript/data URLs and scheme-relative URLs are rejected.

Open the downloaded HTML directly or copy its nav/CSS into your page. Replace
sample anchors with real destinations. The preview does not navigate; exported
links use native browser navigation, keyboard focus and horizontal overflow.
For a host with strict CSP, move the CSS into a permitted stylesheet or add the
host's nonce; the generated standalone file does not weaken a host policy.

First-party code follows the repository's Apache-2.0 license. There are no
adapters, Studio, component labs, public package contracts or consumer CLI.
