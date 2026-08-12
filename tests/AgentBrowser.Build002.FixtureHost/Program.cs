using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("EYEBROWSE_FIXTURE_URLS") ?? "http://0.0.0.0:18762");
var app = builder.Build();
var crossEyeSourcePath = Environment.GetEnvironmentVariable("EYEBROWSE_CROSS_EYE_SOURCE") ?? @"C:\AgentBrowser\runtime\build002-cross-eye\active.js";

app.MapGet("/health", () => Results.Json(new { ok = true, fixture = "eyebrowse-build002", pid = Environment.ProcessId }));

app.MapGet("/", () => Html("Build 002 Fixture", """
<h1>eyeBROWSE Build 002 fixture</h1>
<nav>
<a href="/identity">identity</a>
<a href="/spa">spa</a>
<a href="/bfcache/a">bfcache</a>
<a href="/prerender">prerender</a>
<a href="/oopif">oopif</a>
<a href="/forms">forms</a>
<a href="/runtime-tools">runtime tools</a>
<a href="/webmcp">webmcp</a>
<a href="/virtual">virtualized collection</a>
<a href="/slow">performance</a>
<a href="/cross-eye">cross-eye</a>
<a href="/memory">memory</a>
<a href="/a11y">accessibility</a>
<a href="/dialog">dialog</a>
<a href="/agent-readiness">agent readiness</a>
<a href="/horizontal/export">horizontal</a>
</nav>
"""));

app.MapGet("/identity", () => Html("Identity Fixture", """
<h1>Identity replacement fixture</h1>
<section id="stage">
  <button id="stable" data-testid="stable-action">Stable action</button>
  <button id="replace-one" data-testid="replace-action">Replace one</button>
  <button id="replace-two" data-testid="ambiguous-action">Replace two</button>
</section>
<script>
window.fixture={mode:'initial'};
document.querySelector('#replace-one').addEventListener('click',()=>{
  const old=document.querySelector('#replace-one');
  const next=document.createElement('button'); next.id='replace-one-v2'; next.dataset.testid='replace-action'; next.textContent='Replace one';
  old.replaceWith(next); window.fixture.mode='unique-replaced';
});
document.querySelector('#replace-two').addEventListener('click',()=>{
  const old=document.querySelector('#replace-two');
  const a=document.createElement('button'); a.id='replace-two-a'; a.dataset.testid='ambiguous-action'; a.textContent='Replace two';
  const b=document.createElement('button'); b.id='replace-two-b'; b.dataset.testid='ambiguous-action'; b.textContent='Replace two';
  old.replaceWith(a,b); window.fixture.mode='ambiguous-replaced';
});
</script>
"""));

app.MapGet("/spa", () => Html("SPA Fixture", """
<h1>Same-document navigation</h1>
<div id="route">home</div>
<button id="route-a" data-testid="route-a">Route A</button>
<button id="route-b" data-testid="route-b">Route B</button>
<script>
function go(name){history.pushState({name},'',`/spa#${name}`);document.querySelector('#route').textContent=name;}
document.querySelector('#route-a').onclick=()=>go('a');
document.querySelector('#route-b').onclick=()=>go('b');
addEventListener('popstate',e=>document.querySelector('#route').textContent=e.state?.name||'home');
</script>
"""));

app.MapGet("/bfcache/a", () => Html("BFCache A", """
<h1>BFCache A</h1><p id="nonce"></p><a id="to-b" href="/bfcache/b">Go B</a>
<script>globalThis.heapNonce=globalThis.heapNonce||crypto.randomUUID();document.querySelector('#nonce').textContent=globalThis.heapNonce;addEventListener('pageshow',e=>document.body.dataset.pageshowPersisted=String(e.persisted));addEventListener('pagehide',e=>document.body.dataset.pagehidePersisted=String(e.persisted));</script>
"""));
app.MapGet("/bfcache/b", () => Html("BFCache B", """
<h1>BFCache B</h1><a id="back-a" href="javascript:history.back()">Back A</a>
"""));

app.MapGet("/prerender", () => Html("Prerender Source", """
<h1>Prerender source</h1><a id="activate-prerender" href="/prerender-target">Activate target</a>
<script type="speculationrules">{"prerender":[{"source":"list","urls":["/prerender-target"]}]}</script>
"""));
app.MapGet("/prerender-target", () => Html("Prerender Target", """
<h1>Prerender target</h1><p id="state"></p><script>globalThis.prerenderNonce=globalThis.prerenderNonce||crypto.randomUUID();document.querySelector('#state').textContent=`prerendering=${document.prerendering}; nonce=${globalThis.prerenderNonce}`;</script>
"""));

app.MapGet("/oopif", () => Html("OOPIF Parent", """
<h1>OOPIF parent</h1><iframe id="cross" title="Cross-origin child" src="http://localhost:18762/oopif-child"></iframe>
"""));
app.MapGet("/oopif-child", () => Html("OOPIF Child", """
<h2>Cross-origin frame</h2><button id="frame-button" data-testid="frame-action">Frame action</button>
"""));

app.MapGet("/forms", () => Html("Forms Fixture", """
<h1>Forms</h1>
<form id="profile">
<label>Name <input id="name" name="name" value=""></label>
<label>Role <select id="role" name="role"><option>Engineer</option><option>Researcher</option><option>Operator</option></select></label>
<label><input id="enabled" type="checkbox" name="enabled"> Enabled</label>
<label>Notes <div id="notes" role="textbox" contenteditable="true" aria-label="Notes"></div></label>
<label>File <input id="upload" type="file" name="upload"></label>
<button id="submit" type="submit">Submit</button>
</form>
<pre id="result"></pre>
<a id="download" href="/download/test.txt" download="fixture.txt">Download fixture</a>
<script>
document.querySelector('#profile').addEventListener('submit',e=>{e.preventDefault();const f=new FormData(e.currentTarget);document.querySelector('#result').textContent=JSON.stringify({name:f.get('name'),role:f.get('role'),enabled:f.has('enabled'),notes:document.querySelector('#notes').innerText,file:f.get('upload')?.name||null});});
</script>
"""));

app.MapGet("/download/test.txt", (HttpContext context) => {
    var bytes = Encoding.UTF8.GetBytes("eyeBROWSE Build 002 deterministic fixture download\n");
    context.Response.Headers.ContentDisposition = "attachment; filename=fixture.txt";
    context.Response.ContentType = "text/plain; charset=utf-8";
    return Results.Bytes(bytes, "text/plain");
});

app.MapGet("/runtime-tools", () => Html("Runtime Tools", """
<h1>Runtime tool fixture</h1><div id="counter">0</div><button id="runtime-node" data-testid="runtime-node">Runtime node</button>
<script>
window.addEventListener('devtoolstooldiscovery',event=>{
  event.respondWith?.({name:'fixture',description:'Build 002 runtime tools',tools:[
    {name:'increment',description:'Increment the fixture counter',inputSchema:{type:'object',properties:{by:{type:'number'}},required:['by']},execute:({by})=>{const e=document.querySelector('#counter');e.textContent=String(Number(e.textContent)+Number(by));return {value:Number(e.textContent)};}},
    {name:'get-node',description:'Return the fixture DOM node',inputSchema:{type:'object'},execute:()=>document.querySelector('#runtime-node')}
  ]});
});
</script>
"""));

app.MapGet("/runtime-tools-empty", () => Html("Runtime Tools Empty", """<h1>No runtime tools here</h1>"""));

app.MapGet("/webmcp", () => Html("WebMCP Fixture", """
<h1>WebMCP fixture</h1>
<input id="query" aria-label="Query"><button id="submit-item">Submit</button><ul id="items"><li>alpha</li><li>beta</li><li>gamma</li></ul><pre id="webmcp-status"></pre>
<script>
const status=document.querySelector('#webmcp-status');
const ctx=document.modelContext;
if(!ctx){status.textContent='webmcp-unavailable';}
else{
  const register=tool=>ctx.registerTool(tool);
  register({name:'search_items',description:'Search fixture items',inputSchema:{type:'object',properties:{query:{type:'string'}},required:['query']},execute:({query})=>Array.from(document.querySelectorAll('#items li')).map(x=>x.textContent).filter(x=>x.includes(query))});
  register({name:'filter_items',description:'Filter visible fixture items',inputSchema:{type:'object',properties:{prefix:{type:'string'}},required:['prefix']},execute:({prefix})=>{for(const li of document.querySelectorAll('#items li'))li.hidden=!li.textContent.startsWith(prefix);return {visible:Array.from(document.querySelectorAll('#items li:not([hidden])')).map(x=>x.textContent)};}});
  register({name:'add_item',description:'Add an item',inputSchema:{type:'object',properties:{value:{type:'string'}},required:['value']},execute:({value})=>{const li=document.createElement('li');li.textContent=value;document.querySelector('#items').appendChild(li);return {added:value,count:document.querySelectorAll('#items li').length};}});
  register({name:'submit',description:'Submit current query',inputSchema:{type:'object'},execute:()=>({submitted:document.querySelector('#query').value})});
  status.textContent='webmcp-registered';
}
</script>
"""));

app.MapGet("/virtual", () => Html("Virtualized Collection", """
<h1>Virtualized collection</h1><input id="search" aria-label="Search records"><div id="viewport"></div><div id="summary"></div>
<script>
window.__records=Array.from({length:5000},(_,i)=>({id:i+1,name:`record-${String(i+1).padStart(4,'0')}`,group:(i%17),value:(i*13)%997}));
function render(records=window.__records){const shown=records.slice(0,40);document.querySelector('#viewport').innerHTML=shown.map(r=>`<div role="row" data-id="${r.id}">${r.name} group=${r.group} value=${r.value}</div>`).join('');document.querySelector('#summary').textContent=`showing ${shown.length} of ${records.length}`;}
render();
document.querySelector('#search').addEventListener('input',e=>render(window.__records.filter(r=>r.name.includes(e.target.value))));
</script>
"""));

app.MapGet("/many", (int? i) => Html($"Many Target {i ?? 0}", $"<h1>Many target {i ?? 0}</h1><button data-testid=\"many-{i ?? 0}\">Target {i ?? 0}</button>"));

app.MapGet("/auth-resource", (HttpContext context) => {
    if (!context.Request.Cookies.TryGetValue("fixture_auth", out var value) || value != "yes") return Results.Unauthorized();
    return Results.Text("authenticated fixture resource", "text/plain");
});
app.MapGet("/set-auth", (HttpContext context) => {
    context.Response.Cookies.Append("fixture_auth", "yes", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
    return Results.Redirect("/forms");
});

app.MapGet("/api/slow", async (int? ms, CancellationToken cancellationToken) =>
{
    var delay = Math.Clamp(ms ?? 250, 0, 5000);
    await Task.Delay(delay, cancellationToken);
    return Results.Json(new { ok = true, delayedMs = delay, serverUtc = DateTimeOffset.UtcNow });
});

app.MapGet("/slow", () => Html("Performance Fixture", """
<h1>Performance debugging fixture</h1>
<p>This page exposes deterministic main-thread and network latency.</p>
<button id="block" data-testid="block-main">Block main thread</button>
<button id="fetch" data-testid="fetch-slow">Fetch slow API</button>
<pre id="perf-result"></pre>
<script>
window.slowFixture={
  block(ms=180){const start=performance.now();while(performance.now()-start<ms){};document.querySelector('#perf-result').textContent=`blocked ${Math.round(performance.now()-start)}ms`;return performance.now()-start;},
  async fetchDelay(ms=300){const start=performance.now();const response=await fetch(`/api/slow?ms=${ms}`);const json=await response.json();document.querySelector('#perf-result').textContent=JSON.stringify(json);return {elapsed:performance.now()-start,json};},
  async mixed(){const block=this.block(160);const fetch=await this.fetchDelay(250);return {block,fetch};}
};
document.querySelector('#block').onclick=()=>window.slowFixture.block(180);
document.querySelector('#fetch').onclick=()=>window.slowFixture.fetchDelay(300);
</script>
"""));

app.MapGet("/cross-eye", () => Html("Cross-Eye Fixture", """
<h1>Developer cross-Eye fixture</h1>
<p>This page loads a file-backed JavaScript source used only for the Build 002 browser-evidence/source-engineering handoff.</p>
<pre id="cross-eye-status">loading</pre>
<script src="/cross-eye.js"></script>
"""));

app.MapGet("/cross-eye.js", (HttpContext context) =>
{
    if (!File.Exists(crossEyeSourcePath))
        return Results.NotFound($"Cross-Eye source is not prepared: {crossEyeSourcePath}");
    var bytes = File.ReadAllBytes(crossEyeSourcePath);
    var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-EyeBrowse-Source-Sha256"] = hash;
    return Results.Bytes(bytes, "text/javascript; charset=utf-8");
});
app.MapGet("/horizontal/export", () => Html("Horizontal Export Fixture", """
<main>
<h1>Horizontal export fixture</h1>
<p>This fixture proves generic page export and table extraction without GitHub-specific logic.</p>
<h2>Inventory</h2>
<table id="inventory">
<caption>Fixture Inventory</caption>
<thead><tr><th>Name</th><th>Count</th><th>Status</th></tr></thead>
<tbody>
<tr><td>alpha</td><td>3</td><td>ready</td></tr>
<tr><td>beta</td><td>5</td><td>queued</td></tr>
<tr><td>gamma</td><td>8</td><td>ready</td></tr>
</tbody>
</table>
<p><a href="/horizontal/downloads">Download resources</a></p>
</main>
"""));

app.MapGet("/horizontal/downloads", () => Html("Horizontal Download Fixture", """
<main>
<h1>Horizontal download fixture</h1>
<ul>
<li><a href="/horizontal/download/text.txt" download="fixture-note.txt">Text attachment</a></li>
<li><a href="/horizontal/download/data.csv" download="fixture-data.csv">CSV attachment</a></li>
<li><a href="/horizontal/download/report.pdf" download="fixture-report.pdf">PDF attachment</a></li>
</ul>
</main>
"""));

app.MapGet("/horizontal/download/text.txt", (HttpContext context) =>
{
    var bytes = Encoding.UTF8.GetBytes("horizontal fixture text resource\nline-two\n");
    context.Response.Headers.ContentDisposition = "attachment; filename=fixture-note.txt";
    return Results.Bytes(bytes, "text/plain; charset=utf-8");
});

app.MapGet("/horizontal/download/data.csv", (HttpContext context) =>
{
    var bytes = Encoding.UTF8.GetBytes("name,count\nalpha,3\nbeta,5\ngamma,8\n");
    context.Response.Headers.ContentDisposition = "attachment; filename=fixture-data.csv";
    return Results.Bytes(bytes, "text/csv; charset=utf-8");
});

app.MapGet("/horizontal/download/report.pdf", (HttpContext context) =>
{
    var pdf = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 300 144]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj\n4 0 obj<</Length 67>>stream\nBT /F1 12 Tf 36 90 Td (eyeBROWSE horizontal PDF fixture) Tj ET\nendstream\nendobj\n5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\nxref\n0 6\n0000000000 65535 f \ntrailer<</Root 1 0 R/Size 6>>\nstartxref\n0\n%%EOF\n";
    var bytes = Encoding.ASCII.GetBytes(pdf);
    context.Response.Headers.ContentDisposition = "attachment; filename=fixture-report.pdf";
    return Results.Bytes(bytes, "application/pdf");
});

app.MapGet("/horizontal/page/{page:int}", (int page) =>
{
    page = Math.Clamp(page, 1, 3);
    var items = page switch
    {
        1 => "<li data-item=\"p1-a\">p1-a</li><li data-item=\"p1-b\">p1-b</li>",
        2 => "<li data-item=\"p2-a\">p2-a</li><li data-item=\"p2-b\">p2-b</li>",
        _ => "<li data-item=\"p3-a\">p3-a</li><li data-item=\"p3-b\">p3-b</li>"
    };
    var next = page < 3 ? $"<button aria-label=\"Next\" onclick=\"location.href='/horizontal/page/{page + 1}'\">Next</button>" : "";
    return Html($"Horizontal Page {page}", $"<main><h1>Horizontal page {page}</h1><ol>{items}</ol>{next}</main>");
});
app.MapGet("/second-mail/sign-in", () => Html("Second Mail Sign In", """
<main>
<h1>Second Mail sign in</h1>
<p>This deterministic fixture requires a persistent browser cookie.</p>
<a href="/second-mail/login">Sign in to Second Mail</a>
</main>
"""));

app.MapGet("/second-mail/login", (HttpContext context) =>
{
    context.Response.Cookies.Append("second_mail_auth", "yes", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
    return Results.Redirect("/second-mail");
});

app.MapGet("/second-mail", (HttpContext context) =>
{
    if (!context.Request.Cookies.TryGetValue("second_mail_auth", out var auth) || auth != "yes")
        return Results.Redirect("/second-mail/sign-in");
    return Html("Second Mail", """
<main>
<h1>Second Mail</h1>
<p id="auth-state">authenticated</p>
<label>Search mail <input id="mail-search" aria-label="Search mail"></label>
<button id="compose" aria-label="Compose">Compose</button>
<div id="mail-count"></div>
<section id="mail-list" role="list" aria-label="Inbox"></section>
<article id="message" aria-live="polite"></article>
<section id="composer" hidden>
  <h2>Compose message</h2>
  <label>To <input id="compose-to" aria-label="To"></label>
  <label>Subject <input id="compose-subject" aria-label="Subject"></label>
  <div id="compose-body" role="textbox" contenteditable="true" aria-label="Body"></div>
  <button id="send" aria-label="Send">Send</button>
</section>
<pre id="mail-status"></pre>
<script>
window.__secondMail = Array.from({length:240}, (_,i) => {
  const n=i+1;
  const invoice=n%37===0;
  return {id:n, sender:`sender${String(n).padStart(3,'0')}@example.test`, subject:invoice?`Invoice ${String(n).padStart(3,'0')}`:`Project update ${String(n).padStart(3,'0')}`, body:`Message body ${n}`, attachment:invoice};
});
const list=document.querySelector('#mail-list');
const count=document.querySelector('#mail-count');
const detail=document.querySelector('#message');
function render(records=window.__secondMail){
  const shown=records.slice(0,25);
  list.replaceChildren(...shown.map(m=>{
    const item=document.createElement('div'); item.setAttribute('role','listitem'); item.dataset.messageId=String(m.id);
    const row=document.createElement('button'); row.type='button'; row.setAttribute('aria-label',`Open ${m.subject} from ${m.sender}`); row.textContent=`${m.sender} - ${m.subject}`;
    row.onclick=()=>openMessage(m); item.appendChild(row); return item;
  }));
  count.textContent=`showing ${shown.length} of ${records.length}`;
}
function openMessage(m){
  const attachment=m.attachment?`<p><a aria-label="Download attachment" download="message-${m.id}.txt" href="/second-mail/attachment/${m.id}.txt">Download attachment</a></p>`:'';
  detail.innerHTML=`<h2>${m.subject}</h2><p class="sender">${m.sender}</p><p>${m.body}</p>${attachment}`;
  document.querySelector('#mail-status').textContent=`opened:${m.id}`;
}
render();
document.querySelector('#mail-search').addEventListener('input',e=>{const q=e.target.value.toLowerCase();render(window.__secondMail.filter(m=>m.subject.toLowerCase().includes(q)||m.sender.toLowerCase().includes(q)));});
document.querySelector('#compose').onclick=()=>{document.querySelector('#composer').hidden=false;document.querySelector('#compose-to').focus();document.querySelector('#mail-status').textContent='compose-open';};
document.querySelector('#send').onclick=()=>{const payload={to:document.querySelector('#compose-to').value,subject:document.querySelector('#compose-subject').value,body:document.querySelector('#compose-body').innerText};document.querySelector('#mail-status').textContent='sent:'+JSON.stringify(payload);document.querySelector('#composer').hidden=true;};
</script>
</main>
""");
});

app.MapGet("/second-mail/attachment/{id:int}.txt", (HttpContext context, int id) =>
{
    if (!context.Request.Cookies.TryGetValue("second_mail_auth", out var auth) || auth != "yes")
        return Results.Unauthorized();
    var bytes = Encoding.UTF8.GetBytes($"Second Mail attachment {id}\ninvoice-evidence-{id}\n");
    context.Response.Headers.ContentDisposition = $"attachment; filename=message-{id}.txt";
    return Results.Bytes(bytes, "text/plain; charset=utf-8");
});
app.MapGet("/memory", () => Html("Memory Leak Fixture", """
<h1>Memory leak fixture</h1>
<p>LeakyWidget instances are intentionally retained by a global array.</p>
<button id="leak" data-testid="create-leak">Create leak batch</button>
<button id="clear-leak" data-testid="clear-leak">Clear retained objects</button>
<output id="leak-count">0</output>
<div id="leak-host"></div>
<script>
class LeakyWidget {
  constructor(id){
    this.id=id;
    this.payload=('LEAK-'+id+'-').repeat(1500);
    this.node=document.createElement('div');
    this.node.className='leaky-widget';
    this.node.textContent='leaky '+id;
    this.listener=()=>this.payload.length;
    this.node.addEventListener('click',this.listener);
    document.querySelector('#leak-host').appendChild(this.node);
  }
  detach(){this.node.remove();}
}
window.__leaks=[];
window.leakFixture=(count=25)=>{
  for(let i=0;i<count;i++){const widget=new LeakyWidget(window.__leaks.length);widget.detach();window.__leaks.push(widget);}
  document.querySelector('#leak-count').value=String(window.__leaks.length);
  return window.__leaks.length;
};
window.clearLeakFixture=()=>{window.__leaks.length=0;document.querySelector('#leak-count').value='0';return 0;};
document.querySelector('#leak').onclick=()=>window.leakFixture(25);
document.querySelector('#clear-leak').onclick=()=>window.clearLeakFixture();
</script>
"""));

app.MapGet("/a11y", () => Html("Accessibility Fixture", """
<h1>Accessibility debugging fixture</h1>
<p id="instructions">The following controls intentionally contain accessibility defects.</p>
<button id="good" aria-label="Good action">Visible icon only ✓</button>
<button id="unlabeled"><span aria-hidden="true">★</span></button>
<input id="unlabeled-input" type="text" placeholder="Search without label">
<div role="checkbox" id="fake-check" tabindex="0">Custom checkbox with no aria-checked state</div>
<button id="broken-description" aria-describedby="missing-description">Broken relation</button>
<a href="#later" tabindex="5">Artificial tab-order jump</a>
<input id="labelled" aria-labelledby="instructions" value="ok">
<div id="later">Later content</div>
"""));

app.MapGet("/dialog", () => Html("Dialog Fixture", """
<h1>JavaScript dialog fixture</h1>
<button id="alert" data-testid="alert">Alert</button>
<button id="confirm" data-testid="confirm">Confirm</button>
<button id="prompt" data-testid="prompt">Prompt</button>
<pre id="dialog-result"></pre>
<script>
document.querySelector('#alert').onclick=()=>{alert('fixture-alert');document.querySelector('#dialog-result').textContent='alert-closed';};
document.querySelector('#confirm').onclick=()=>{document.querySelector('#dialog-result').textContent='confirm:'+confirm('fixture-confirm');};
document.querySelector('#prompt').onclick=()=>{document.querySelector('#dialog-result').textContent='prompt:'+prompt('fixture-prompt','default-value');};
</script>
"""));

app.MapGet("/agent-readiness", () => Html("Agent Readiness Fixture", """
<h1>Agent-readiness fixture</h1>
<p>The useful UI works for a human but intentionally lacks deterministic agent affordances.</p>
<div id="visual-submit" role="button" onclick="document.querySelector('#readiness-status').textContent='submitted'">SUBMIT</div>
<input id="mystery" placeholder="Type here">
<div id="changing-container"></div>
<pre id="readiness-status"></pre>
<script>
let generation=0;
function rerender(){generation++;const b=document.createElement('button');b.textContent='Dynamic action';b.className='generated-'+generation;b.onclick=()=>{document.querySelector('#readiness-status').textContent='dynamic-'+generation;rerender();};const c=document.querySelector('#changing-container');c.replaceChildren(b);}
rerender();
window.agentReadinessFixture={rerender,get generation(){return generation;}};
</script>
"""));
app.Run();

static IResult Html(string title, string body) => Results.Content($$"""
<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>{{title}}</title><style>body{font-family:system-ui,sans-serif;margin:24px}nav a{display:block;margin:8px 0}label{display:block;margin:10px 0}iframe{width:90%;height:220px;border:1px solid #999}#viewport{height:360px;overflow:auto;border:1px solid #aaa}button,input,select,[contenteditable]{margin:6px;padding:6px}</style></head><body>{{body}}</body></html>
""", "text/html; charset=utf-8");
