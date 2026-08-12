import { targetFromArgs, optionalInt } from '../common/lib.mjs';
import { compareHeaps, retainingPath } from './analysis.mjs';

export default async function analyzeMemoryLeak(browser,args={}){
  const target=await targetFromArgs(browser,args); const iterations=optionalInt(args,'iterations',20,1,1000);
  await browser.cdp('HeapProfiler.collectGarbage',{},target).catch(()=>null);
  const beforeCurrent=await browser.memoryCurrent(target); const before=await browser.heapSnapshot(target);
  if(typeof args.exerciseExpression!=='string'||!args.exerciseExpression.trim()) throw new TypeError('exerciseExpression is required for developer.analyze-memory-leak');
  for(let i=0;i<iterations;i++) await browser.jsValue(target,args.exerciseExpression);
  await browser.cdp('HeapProfiler.collectGarbage',{},target).catch(()=>null);
  const afterCurrent=await browser.memoryCurrent(target); const after=await browser.heapSnapshot(target);
  const comparison=await compareHeaps(before.artifact.path,after.artifact.path,{top:50});
  let path=null; if(args.retainMatcher) path=await retainingPath(after.artifact.path,args.retainMatcher,{maxDepth:40});
  return {target,iterations,beforeCurrent,afterCurrent,before:before.artifact,after:after.artifact,comparison,retainingPath:path};
}
