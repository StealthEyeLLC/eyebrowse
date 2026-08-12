import { targetFromArgs, optionalInt } from '../common/lib.mjs';

export default async function investigateConsoleError(browser,args={}){
  const target=await targetFromArgs(browser,args); const limit=optionalInt(args,'limit',100,1,500);
  await browser.runtimeDebugEnable(target).catch(()=>null);
  const [exceptions,consoleEntries,scripts,network]=await Promise.all([
    browser.exceptions(target,limit), browser.console(target,limit), browser.runtimeScripts(target,args.contains,1000).catch(()=>[]), browser.network({target,limit:200})
  ]);
  const errors=[...exceptions.map(x=>({kind:'exception',value:x})),...consoleEntries.filter(x=>/error|warning/i.test(String(x.level||x.source||''))).map(x=>({kind:'console',value:x}))];
  const urls=new Set();
  for(const error of errors){const text=JSON.stringify(error.value); for(const script of scripts){if(script.url && text.includes(script.url)) urls.add(script.url)}}
  const relatedScripts=scripts.filter(x=>urls.has(x.url)).slice(0,50);
  const relatedNetwork=network.filter(x=>[...urls].some(url=>x.url===url||x.url?.includes(url))).slice(0,50);
  return {target,errorCount:errors.length,errors:errors.slice(-50),relatedScripts,relatedNetwork,scriptCount:scripts.length};
}
