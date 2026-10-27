FROM node:26-alpine AS base

ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"

RUN npm install --global pnpm@11.8.0

FROM base AS dependencies

WORKDIR /app/src

COPY src/package.json src/pnpm-lock.yaml src/pnpm-workspace.yaml ./
COPY src/packages/portal/package.json packages/portal/package.json

RUN pnpm install --frozen-lockfile

FROM dependencies AS builder

WORKDIR /app/src

COPY . /app

ENV NEXT_TELEMETRY_DISABLED=1

RUN pnpm build

FROM node:26-alpine AS runner

WORKDIR /app

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"

COPY --from=builder --chown=node:node /app/src/packages/portal/.next/standalone ./

USER node

EXPOSE 3000

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:3000/').then((response) => process.exit(response.ok ? 0 : 1)).catch(() => process.exit(1))"

CMD ["node", "src/packages/portal/server.js"]
