const { test } = require("node:test");
const assert = require("node:assert/strict");
const { configuration, deploy } = require("./deploy-coolify.cjs");
const env = {
  COOLIFY_BASE_URL: "https://deploy.example.com",
  COOLIFY_RESOURCE_UUID: "abcdefgh12345678",
  COOLIFY_TOKEN: "test-only",
  APP_HEALTH_URL: "https://example.com/health",
};
test("configuration rejects insecure origins and embedded credentials", () => {
  for (const origin of [
    "http://example.com",
    "https://user:secret@example.com",
    "https://example.com/path",
    "https://example.com/?query=x",
  ])
    assert.throws(() => configuration({ ...env, COOLIFY_BASE_URL: origin }));
  assert.throws(() => configuration({ ...env, COOLIFY_TOKEN: "" }));
});
test("queues once, waits, and probes health without forwarding credentials", async () => {
  const calls = [];
  const responses = [
    {
      deployments: [
        {
          resource_uuid: env.COOLIFY_RESOURCE_UUID,
          deployment_uuid: "deployment123",
        },
      ],
    },
    { status: "in_progress" },
    { status: "finished" },
    {},
  ];
  const result = await deploy(env, {
    fetch: async (url, options) => {
      calls.push({ url, options });
      return { ok: true, json: async () => responses.shift() };
    },
    delay: async () => {},
    report: () => {},
  });
  assert.equal(result.status, "finished");
  assert.equal(calls.filter((c) => c.options.method === "POST").length, 1);
  assert.equal(calls.at(-1).options.headers.Authorization, undefined);
  assert.ok(calls.every((c) => c.options.redirect === "error"));
});
test("failure, wrong commit, and unknown states fail closed", async () => {
  for (const state of [
    { status: "failed" },
    { status: "finished", commit: "b".repeat(40) },
    { status: "surprise" },
  ]) {
    let n = 0;
    await assert.rejects(() =>
      deploy(
        { ...env, DEPLOY_COMMIT_SHA: "a".repeat(40) },
        {
          fetch: async () => ({
            ok: true,
            json: async () =>
              n++
                ? state
                : {
                    deployments: [
                      {
                        resource_uuid: env.COOLIFY_RESOURCE_UUID,
                        deployment_uuid: "deployment123",
                      },
                    ],
                  },
          }),
          delay: async () => {},
          report: () => {},
        },
      ),
    );
  }
});

test("image deployment checks the served revision before success", async () => {
  const sha = "a".repeat(40);
  const responses = [
    {
      deployments: [
        {
          resource_uuid: env.COOLIFY_RESOURCE_UUID,
          deployment_uuid: "deployment123",
        },
      ],
    },
    { status: "finished" },
  ];
  const result = await deploy(
    { ...env, DEPLOY_IMAGE_SHA: sha },
    {
      fetch: async () => ({
        ok: true,
        json: async () => responses.shift(),
        text: async () => sha,
      }),
      delay: async () => {},
      report: () => {},
    },
  );
  assert.equal(result.status, "finished");
});

test("a healthy old image never verifies a new revision", async () => {
  const responses = [
    {
      deployments: [
        {
          resource_uuid: env.COOLIFY_RESOURCE_UUID,
          deployment_uuid: "deployment123",
        },
      ],
    },
    { status: "finished" },
  ];
  await assert.rejects(
    () =>
      deploy(
        { ...env, DEPLOY_IMAGE_SHA: "a".repeat(40) },
        {
          fetch: async () => ({
            ok: true,
            json: async () => responses.shift(),
            text: async () => "b".repeat(40),
          }),
          delay: async () => {},
          report: () => {},
        },
      ),
    /health check/,
  );
});
