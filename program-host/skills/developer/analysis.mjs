import { readFile } from 'node:fs/promises';

export async function analyzeTrace(path, options = {}) {
  const raw = await readFile(path, 'utf8');
  const parsed = JSON.parse(raw);
  const events = Array.isArray(parsed) ? parsed : (parsed.traceEvents || []);
  const minDurationUs = Number(options.minDurationUs || 50000);
  const durationEvents = events
    .filter(e => typeof e?.dur === 'number' && e.dur >= minDurationUs)
    .map(e => ({ name: e.name || '', category: e.cat || '', durationMs: e.dur / 1000, ts: e.ts, pid: e.pid, tid: e.tid, args: compactArgs(e.args) }))
    .sort((a,b) => b.durationMs - a.durationMs)
    .slice(0, Number(options.top || 30));
  const categoryCounts = new Map();
  for (const event of events) {
    for (const cat of String(event?.cat || '').split(',').filter(Boolean)) categoryCounts.set(cat, (categoryCounts.get(cat) || 0) + 1);
  }
  const categories = [...categoryCounts.entries()].sort((a,b)=>b[1]-a[1]).slice(0,30).map(([name,count])=>({name,count}));
  const navigation = events.filter(e => /navigation|firstContentfulPaint|largestContentfulPaint|LayoutShift|RunTask|EvaluateScript/i.test(String(e?.name || '')))
    .slice(-500).map(e => ({ name:e.name, ts:e.ts, durationMs: typeof e.dur === 'number' ? e.dur/1000 : undefined, args:compactArgs(e.args) }));
  return { path, bytes: Buffer.byteLength(raw), eventCount: events.length, longEventThresholdMs: minDurationUs/1000, topDurationEvents: durationEvents, categories, navigationSignals: navigation.slice(0,100) };
}

export async function loadHeap(path) {
  const raw = await readFile(path, 'utf8');
  const heap = JSON.parse(raw);
  const meta = heap?.snapshot?.meta;
  if (!meta || !Array.isArray(heap.nodes) || !Array.isArray(heap.edges) || !Array.isArray(heap.strings)) throw new Error('Not a V8 heap snapshot');
  return { heap, meta, bytes: Buffer.byteLength(raw) };
}

export async function summarizeHeap(path, options = {}) {
  const { heap, meta, bytes } = await loadHeap(path);
  const nf = meta.node_fields;
  const nt = meta.node_types?.[0] || [];
  const nodeWidth = nf.length;
  const typeIx = nf.indexOf('type');
  const nameIx = nf.indexOf('name');
  const idIx = nf.indexOf('id');
  const selfIx = nf.indexOf('self_size');
  const edgeCountIx = nf.indexOf('edge_count');
  const detachedIx = nf.indexOf('detachedness');
  const classes = new Map();
  let detachedNodes = 0;
  let totalSelfSize = 0;
  let nodeCount = 0;
  for (let off=0; off<heap.nodes.length; off+=nodeWidth) {
    nodeCount++;
    const type = nt[heap.nodes[off+typeIx]] ?? String(heap.nodes[off+typeIx]);
    const name = heap.strings[heap.nodes[off+nameIx]] ?? '';
    const size = Number(heap.nodes[off+selfIx] || 0);
    const detached = detachedIx >= 0 ? Number(heap.nodes[off+detachedIx] || 0) : 0;
    if (detached) detachedNodes++;
    totalSelfSize += size;
    const key = `${type}:${name}`;
    const current = classes.get(key) || { type, name, count:0, selfSize:0, detached:0 };
    current.count++; current.selfSize += size; if (detached) current.detached++;
    classes.set(key,current);
  }
  const top = [...classes.values()].sort((a,b)=>b.selfSize-a.selfSize).slice(0,Number(options.top || 40));
  const topCount = [...classes.values()].sort((a,b)=>b.count-a.count).slice(0,Number(options.top || 40));
  return { path, bytes, nodeCount, totalSelfSize, detachedNodes, topBySelfSize:top, topByCount:topCount, meta:{nodeFields:nf, edgeFields:meta.edge_fields}, _heap: options.includeHeap ? heap : undefined };
}

export async function compareHeaps(beforePath, afterPath, options = {}) {
  const before = await summarizeHeap(beforePath,{top:1000});
  const after = await summarizeHeap(afterPath,{top:1000});
  const b = new Map([...before.topBySelfSize, ...before.topByCount].map(x=>[`${x.type}:${x.name}`,x]));
  const a = new Map([...after.topBySelfSize, ...after.topByCount].map(x=>[`${x.type}:${x.name}`,x]));
  const keys = new Set([...b.keys(),...a.keys()]);
  const deltas = [];
  for (const key of keys) {
    const x=b.get(key)||{type:key.split(':')[0],name:key.slice(key.indexOf(':')+1),count:0,selfSize:0,detached:0};
    const y=a.get(key)||{...x,count:0,selfSize:0,detached:0};
    const countDelta=y.count-x.count, selfSizeDelta=y.selfSize-x.selfSize, detachedDelta=(y.detached||0)-(x.detached||0);
    if (countDelta || selfSizeDelta || detachedDelta) deltas.push({type:y.type,name:y.name,countDelta,selfSizeDelta,detachedDelta,beforeCount:x.count,afterCount:y.count});
  }
  deltas.sort((x,y)=>Math.max(Math.abs(y.selfSizeDelta),Math.abs(y.countDelta)*1024)-Math.max(Math.abs(x.selfSizeDelta),Math.abs(x.countDelta)*1024));
  return { before:{path:before.path,nodeCount:before.nodeCount,totalSelfSize:before.totalSelfSize,detachedNodes:before.detachedNodes}, after:{path:after.path,nodeCount:after.nodeCount,totalSelfSize:after.totalSelfSize,detachedNodes:after.detachedNodes}, deltas:deltas.slice(0,Number(options.top || 50)) };
}

export async function retainingPath(path, matcher, options = {}) {
  const { heap, meta } = await loadHeap(path);
  const nf=meta.node_fields, ef=meta.edge_fields, nt=meta.node_types?.[0]||[], et=meta.edge_types?.[0]||[];
  const nw=nf.length, ew=ef.length, typeIx=nf.indexOf('type'), nameIx=nf.indexOf('name'), idIx=nf.indexOf('id'), edgeCountIx=nf.indexOf('edge_count');
  const edgeTypeIx=ef.indexOf('type'), edgeNameIx=ef.indexOf('name_or_index'), toIx=ef.indexOf('to_node');
  const nodeTotal=Math.floor(heap.nodes.length/nw);
  const parent=new Int32Array(nodeTotal); parent.fill(-1);
  const parentEdge=new Array(nodeTotal);
  let edgeOff=0;
  for(let from=0;from<nodeTotal;from++){
    const off=from*nw, edgeCount=Number(heap.nodes[off+edgeCountIx]||0);
    for(let j=0;j<edgeCount;j++,edgeOff+=ew){
      const toOffset=Number(heap.edges[edgeOff+toIx]); const to=Math.floor(toOffset/nw);
      if(to>=0 && to<nodeTotal && parent[to]===-1 && to!==0){
        parent[to]=from;
        const edgeType=et[heap.edges[edgeOff+edgeTypeIx]]??String(heap.edges[edgeOff+edgeTypeIx]);
        const rawName=heap.edges[edgeOff+edgeNameIx];
        parentEdge[to]={type:edgeType,name: edgeType==='element'||edgeType==='hidden' ? String(rawName) : (heap.strings[rawName]??String(rawName))};
      }
    }
  }
  const regex = matcher instanceof RegExp ? matcher : new RegExp(String(matcher),'i');
  let target=-1, targetScore=-1;
  const typePriority={object:100,native:90,closure:80,code:60,string:10,synthetic:0};
  for(let i=0;i<nodeTotal;i++){
    const off=i*nw, name=heap.strings[heap.nodes[off+nameIx]]??'', type=nt[heap.nodes[off+typeIx]]??'';
    regex.lastIndex=0;
    if(!regex.test(`${type}:${name}`)) continue;
    const score=(typePriority[type]??50) + (name===String(matcher)?20:0);
    if(score>targetScore){ target=i; targetScore=score; }
  }
  if(target<0) return {path,found:false,matcher:String(matcher)};
  const result=[]; const seen=new Set(); let cur=target; const maxDepth=Number(options.maxDepth||30);
  while(cur>=0 && !seen.has(cur) && result.length<maxDepth){
    seen.add(cur); const off=cur*nw; const type=nt[heap.nodes[off+typeIx]]??'', name=heap.strings[heap.nodes[off+nameIx]]??'', id=idIx>=0?heap.nodes[off+idIx]:null;
    result.push({node:cur,type,name,id,via:parentEdge[cur]||null}); cur=parent[cur];
  }
  return {path,found:true,matcher:String(matcher),retainingPath:result};
}

function compactArgs(args) {
  if (!args || typeof args !== 'object') return undefined;
  const text = JSON.stringify(args);
  return text.length <= 2000 ? args : { truncated: true, preview: text.slice(0,2000) };
}
