Deno.serve((req: Request) =>
  new Response(
    JSON.stringify({ traceparent: req.headers.get("traceparent") }),
    { headers: { "Content-Type": "application/json" } },
  )
)
