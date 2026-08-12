import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';
const target=process.argv[2]||'t_12';
const browser=await EyeBrowse.connect();
try{
  await browser.observe(target);
  const sub=await browser.subscribe(['Runtime.executionContextCreated'],target);
  await browser.cdp('Runtime.disable',{},target).catch(()=>null);
  await browser.cdp('Runtime.enable',{},target);
  await new Promise(r=>setTimeout(r,150));
  const events=await browser.next(sub.id,1000,256);
  const contexts=[];
  for(const event of events){
    const c=event?.params?.context;
    if(!c?.id) continue;
    let probe=null;
    try{
      probe=await browser.cdp('Runtime.evaluate',{expression:'({has:Number(globalThis.__eyebrowseIdentity?.version??0),state:globalThis.__eyebrowseIdentity?.exportBindings?.()??null})',contextId:c.id,returnByValue:true,awaitPromise:true},target);
    }catch(error){probe={error:String(error)}}
    contexts.push({id:c.id,name:c.name,origin:c.origin,uniqueId:c.uniqueId,auxData:c.auxData,probe});
  }
  await browser.unsubscribe(sub.id);
  console.log(JSON.stringify({target,eventCount:events.length,contexts},null,2));
}finally{browser.close();}