import assert from "node:assert/strict";
import { test } from "node:test";
import { downloadJSON } from "../../packages/portal/src/components/download-json.ts";

test("JSON download revokes its owned URL on success and click failure", (t) => {
  const document = Object.getOwnPropertyDescriptor(globalThis, "document");
  const create = URL.createObjectURL,
    revoke = URL.revokeObjectURL;
  t.after(() => {
    URL.createObjectURL = create;
    URL.revokeObjectURL = revoke;
    if (document) Object.defineProperty(globalThis, "document", document);
    else Reflect.deleteProperty(globalThis, "document");
  });
  const revoked = [];
  const link = { href: "", download: "", click() {} };
  globalThis.document = { createElement: () => link };
  URL.createObjectURL = (blob) => {
    assert.equal(blob.type, "application/json");
    return "blob:owned";
  };
  URL.revokeObjectURL = (value) => revoked.push(value);
  downloadJSON('{"version":2}', "design.json");
  assert.equal(link.href, "blob:owned");
  assert.equal(link.download, "design.json");
  assert.deepEqual(revoked, ["blob:owned"]);
  link.click = () => {
    throw new Error("Synthetic blocked click");
  };
  assert.throws(() => downloadJSON("{}", "design.json"), /blocked click/);
  assert.deepEqual(revoked, ["blob:owned", "blob:owned"]);
});

test("unavailable download creation is explicit and owns no URL to leak", (t) => {
  const create = URL.createObjectURL;
  t.after(() => {
    URL.createObjectURL = create;
  });
  URL.createObjectURL = () => {
    throw new Error("Synthetic unavailable API");
  };
  assert.throws(() => downloadJSON("{}", "design.json"), /unavailable API/);
});
