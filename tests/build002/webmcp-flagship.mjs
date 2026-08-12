import {EyeBrowse} from '../../program-host/sdk/eyebrowse.mjs';
const b=await EyeBrowse.connect();
try {
  const t=(await b.targets()).find(x=>x.url?.endsWith('/webmcp')) || (await b.open('http://127.0.0.1:18762/webmcp')).target;
  await b.observe(t.id);
  const tools=await b.webmcp(t.id);
  const byName=Object.fromEntries(tools.map(x=>[x.name,x]));
  const inspected={}; for(const name of ['search_items','filter_items','add_item','submit']) inspected[name]=await b.webmcpInspect(t.id,name,byName[name]?.frameId);
  const add=await b.webmcpExecute(t.id,'add_item',{value:'delta'},byName.add_item.frameId);
  const search=await b.webmcpExecute(t.id,'search_items',{query:'ta'},byName.search_items.frameId);
  const filter=await b.webmcpExecute(t.id,'filter_items',{prefix:'d'},byName.filter_items.frameId);
  const submit=await b.webmcpExecute(t.id,'submit',{},byName.submit.frameId);
  const state=await b.jsValue(t.id,'({items:window.__webmcpState?.items,query:window.__webmcpState?.query,filtered:window.__webmcpState?.filtered,submitted:window.__webmcpState?.submitted})');
  console.log(JSON.stringify({target:t.id,toolCount:tools.length,inspected,add,search,filter,submit,state},null,2));
} finally { b.close(); }
