namespace DocuRelay.Api;

/// <summary>
/// A small, dependency-free visual console used during portfolio demos. It reads the same
/// health and dossier endpoints as the MAUI client, so the displayed state is not mock data.
/// </summary>
public static class LocalDemoPage
{
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>DocuRelay Field | Processing Console</title>
          <style>
            :root { color-scheme: dark; --ink:#e9efff; --muted:#a9b8d5; --line:#2a3a62; --panel:#111b35; --card:#172445; --teal:#3cdbc7; --violet:#9c7dff; --amber:#ffca62; }
            * { box-sizing:border-box; } body { margin:0; min-height:100vh; font-family:Inter,ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif; color:var(--ink); background:radial-gradient(circle at 18% 8%,#263d76 0,transparent 29rem),radial-gradient(circle at 88% 16%,#283468 0,transparent 26rem),#091125; }
            main { max-width:1120px; margin:0 auto; padding:48px 28px 56px; } .eyebrow { color:var(--teal); font-size:.77rem; font-weight:750; letter-spacing:.13em; text-transform:uppercase; } h1 { margin:9px 0 8px; font-size:clamp(2rem,5vw,3.7rem); line-height:1.03; letter-spacing:-.045em; } .lead { max-width:720px; margin:0; color:var(--muted); font-size:1.08rem; line-height:1.6; }
            .pill { display:inline-flex; gap:9px; align-items:center; border:1px solid #355783; border-radius:999px; padding:7px 11px; margin-top:20px; color:#c9d8f4; font-size:.82rem; background:#0c1934aa; } .dot { width:9px; height:9px; border-radius:50%; background:var(--teal); box-shadow:0 0 18px var(--teal); }
            .grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:16px; margin:32px 0 20px; } .card { min-height:155px; padding:21px; border:1px solid var(--line); border-radius:18px; background:linear-gradient(140deg,#17264aee,#101a35ee); box-shadow:0 20px 60px #00000035; } .step { color:var(--teal); font-size:.78rem; font-weight:800; letter-spacing:.1em; } h2 { margin:12px 0 8px; font-size:1.14rem; } .card p { margin:0; color:var(--muted); line-height:1.48; font-size:.91rem; }
            .console { border:1px solid var(--line); border-radius:18px; overflow:hidden; background:#0b142b; box-shadow:0 24px 80px #00000045; } .console-head { display:flex; justify-content:space-between; align-items:center; padding:15px 19px; border-bottom:1px solid var(--line); background:#101d3d; } .console-title { font-weight:720; } .refresh { color:var(--teal); font:inherit; font-size:.86rem; border:1px solid #386878; background:#102d39; border-radius:9px; padding:7px 10px; cursor:pointer; } .rows { padding:6px 19px 15px; } .row { display:grid; grid-template-columns:160px 1fr auto; gap:15px; align-items:center; padding:16px 0; border-bottom:1px solid #243555; } .row:last-child { border:0; } .label { color:var(--muted); font-size:.9rem; } .value { font-weight:650; overflow-wrap:anywhere; } .status { border-radius:999px; padding:6px 10px; font-size:.76rem; font-weight:800; } .ok { color:#95ffed; background:#11463e; } .pending { color:#ffe2a5; background:#5d4216; } .complete { color:#b5a6ff; background:#332965; }
            .flow { display:flex; gap:8px; align-items:center; color:#d9e4fb; flex-wrap:wrap; margin:25px 0 0; } .node { border:1px solid #324d79; border-radius:10px; background:#132344; padding:10px 12px; font-weight:700; font-size:.86rem; } .arrow { color:var(--teal); font-size:1.25rem; } .foot { margin:26px 0 0; color:var(--muted); font-size:.87rem; } a { color:var(--teal); }
            @media (max-width:720px) { main{padding:30px 18px;} .grid{grid-template-columns:1fr;} .row{grid-template-columns:1fr;gap:6px;} .status{justify-self:start;} }
          </style>
        </head>
        <body>
          <main>
            <div class="eyebrow">No-cost local Azure-ready demonstration</div>
            <h1>Document processing, made visible.</h1>
            <p class="lead">This console reads the running DocuRelay API. Upload a document from the MAUI app or OpenAPI page; the transactional outbox dispatches it to the local worker and the resulting status appears here.</p>
            <div class="pill"><span class="dot"></span><span id="connection">Checking local API…</span></div>
            <section class="grid" aria-label="Architecture steps">
              <article class="card"><div class="step">01 · API</div><h2>ASP.NET Core</h2><p>Accepts a scoped multipart upload and stores document metadata.</p></article>
              <article class="card"><div class="step">02 · OUTBOX</div><h2>Reliable hand-off</h2><p>The document and processing message are committed together before dispatch.</p></article>
              <article class="card"><div class="step">03 · WORKER</div><h2>Idempotent processing</h2><p>The local worker mirrors the Azure Service Bus worker’s Completed-state guard.</p></article>
            </section>
            <section class="console" aria-label="Live processing state">
              <div class="console-head"><span class="console-title">Live processing state</span><button class="refresh" id="refresh" type="button">Refresh data</button></div>
              <div class="rows">
                <div class="row"><span class="label">API health</span><span class="value" id="health">Loading…</span><span class="status pending" id="health-status">CHECKING</span></div>
                <div class="row"><span class="label">Dossier</span><span class="value" id="dossier">Loading seeded dossier…</span><span class="status pending" id="dossier-status">WAITING</span></div>
                <div class="row"><span class="label">Latest document</span><span class="value" id="document">No document loaded yet</span><span class="status pending" id="document-status">READY</span></div>
              </div>
            </section>
            <div class="flow" aria-label="Processing flow"><span class="node">MAUI / OpenAPI</span><span class="arrow">→</span><span class="node">Document API</span><span class="arrow">→</span><span class="node">Outbox</span><span class="arrow">→</span><span class="node">Worker</span><span class="arrow">→</span><span class="node">Completed</span></div>
            <p class="foot">For the cloud version, the same API switches to Azure SQL and Azure Service Bus through configuration. <a href="/openapi">Open the API contract</a> · <a href="/health">View health JSON</a></p>
          </main>
          <script>
            const byId = id => document.getElementById(id);
            const setStatus = (id, text, kind) => { const item = byId(id); item.textContent = text; item.className = `status ${kind}`; };
            async function refresh() {
              try {
                const health = await fetch('/health').then(response => response.json());
                byId('health').textContent = `${health.service} is responding`;
                setStatus('health-status', 'HEALTHY', 'ok');
                byId('connection').textContent = 'Local API connected — no Azure resources required';
                const dossiers = await fetch('/api/dossiers').then(response => response.json());
                const dossier = dossiers[0];
                if (!dossier) return;
                byId('dossier').textContent = `${dossier.reference} · ${dossier.title}`;
                setStatus('dossier-status', `${dossier.documentCount} DOCUMENT${dossier.documentCount === 1 ? '' : 'S'}`, 'complete');
                const detail = await fetch(`/api/dossiers/${dossier.id}`).then(response => response.json());
                const document = detail.documents[0];
                if (document) {
                  byId('document').textContent = `${document.fileName} · ${new Date(document.uploadedAtUtc).toLocaleTimeString()}`;
                  const complete = document.processingStatus === 'Completed';
                  setStatus('document-status', document.processingStatus.toUpperCase(), complete ? 'complete' : 'pending');
                }
              } catch (error) {
                byId('connection').textContent = 'Start the API to connect this console';
                byId('health').textContent = 'API is not currently reachable';
                setStatus('health-status', 'OFFLINE', 'pending');
              }
            }
            byId('refresh').addEventListener('click', refresh);
            refresh();
          </script>
        </body>
        </html>
        """;
}
