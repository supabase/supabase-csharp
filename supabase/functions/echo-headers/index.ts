Deno.serve((req: Request) =>
  new Response(
    JSON.stringify({
      traceparent: req.headers.get("traceparent"),
      clientInfo: req.headers.get("x-client-info"),
    }),
    { headers: { "Content-Type": "application/json" } },
  )
)
