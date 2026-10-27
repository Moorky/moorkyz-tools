import { proxyVideoCrunch } from "../../../../../server/videocrunch-proxy";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type Context = { readonly params: Promise<{ path?: string[] }> };

async function handle(request: Request, context: Context): Promise<Response> {
  return proxyVideoCrunch(request, (await context.params).path ?? []);
}

export { handle as GET, handle as HEAD, handle as POST };
