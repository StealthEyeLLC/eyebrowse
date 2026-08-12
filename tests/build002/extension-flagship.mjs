import path from 'node:path';
import {EyeBrowse} from '../../program-host/sdk/eyebrowse.mjs';
const b=await EyeBrowse.connect();
const out={};
try {
  const page=(await b.targets()).find(t=>t.type==='page' && t.url?.startsWith('http://127.0.0.1:18762/')) || (await b.open('http://127.0.0.1:18762/')).target;
  out.page={id:page.id,url:page.url};
  const loaded=await b.extensionLoadUnpacked(path.resolve('tests/fixtures/extensions/broken-mv3'));
  out.loaded=loaded;
  const id=loaded.id;
  await new Promise(r=>setTimeout(r,200));
  const targets=await b.targets();
  out.extensionTargets=targets.filter(t=>(t.url||'').startsWith(`chrome-extension://${id}/`));
  const worker=out.extensionTargets.find(t=>t.type==='service_worker') || out.extensionTargets.find(t=>t.type==='background_page') || out.extensionTargets[0];
  if(!worker) throw new Error('No extension runtime target appeared for loaded fixture');
  out.debugAttach=await b.debugTarget(worker.id);
  out.debugEnable=await b.runtimeDebugEnable(worker.id);
  out.scripts=await b.runtimeScripts(worker.id,'service-worker',50);
  try { out.storage=await b.extensionStorage(id,'local'); } catch(e) { out.storageError={message:e.message,type:e.type}; }
  out.before={console:await b.console(worker.id,100),exceptions:await b.exceptions(worker.id,100)};
  await b.extensionTriggerAction(id,page.id);
  await new Promise(r=>setTimeout(r,250));
  out.after={console:await b.console(worker.id,100),exceptions:await b.exceptions(worker.id,100),paused:await b.runtimePaused(worker.id)};
  out.diagnosed = [...out.after.console,...out.after.exceptions].some(x=>JSON.stringify(x).includes('Build002 controlled extension action failure'));
  console.log(JSON.stringify(out,null,2));
} finally {
  try { if(out.loaded?.id) await b.extensionUninstall(out.loaded.id); } catch {}
  b.close();
}
