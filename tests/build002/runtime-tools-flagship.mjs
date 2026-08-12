import {EyeBrowse} from '../../program-host/sdk/eyebrowse.mjs';
const b=await EyeBrowse.connect();
const out={};
try {
  const opened=await b.open('http://127.0.0.1:18762/runtime-tools');
  const target=opened.target.id;
  await b.activate(target);
  const surface=await b.observe(target);
  const runtimeNode=(surface.elements||[]).find(e=>e.name==='Runtime node');
  out.target=target;
  out.document=surface.document;
  out.runtimeNode=runtimeNode;
  out.groups=await b.runtimeTools(target);
  out.incrementInspect=await b.runtimeToolInspect(target,'increment','fixture');
  out.nodeInspect=await b.runtimeToolInspect(target,'get-node','fixture');
  out.increment=await b.runtimeToolExecute(target,'increment',{by:7},'fixture');
  out.counter=await b.jsValue(target,'Number(document.querySelector("#counter")?.textContent||0)');
  out.nodeResult=await b.runtimeToolExecute(target,'get-node',{},'fixture');
  out.mappedExisting = !!runtimeNode && out.nodeResult?.element === runtimeNode.id;
  await b.navigate(target,'http://127.0.0.1:18762/runtime-tools-empty');
  await b.wait(target,'location.pathname==="/runtime-tools-empty" && document.readyState==="complete"',10000,50);
  const after=await b.observe(target);
  out.afterDocument=after.document;
  out.afterGroups=await b.runtimeTools(target);
  out.documentChanged=out.afterDocument!==out.document;
  try { out.oldToolExecution=await b.runtimeToolExecute(target,'increment',{by:1},'fixture'); }
  catch(error){ out.oldToolExecutionError={message:error.message,type:error.type}; }
  console.log(JSON.stringify(out,null,2));
} finally { b.close(); }
