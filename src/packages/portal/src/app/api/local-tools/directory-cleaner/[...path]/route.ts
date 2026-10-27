import { proxyLocalTool } from "../../../../../server/local-tool-proxy";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type Context = { readonly params: Promise<{ path: string[] }> };
async function handle(request: Request, context: Context): Promise<Response> {
  return proxyLocalTool(
    request,
    (await context.params).path,
    "directory-cleaner",
  );
}
export { handle as GET, handle as POST };
