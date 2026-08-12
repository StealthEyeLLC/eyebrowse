import { targetFromArgs, optionalInt } from '../common/lib.mjs';
import { analyzeTrace } from './analysis.mjs';

export default async function capturePerformanceProfile(browser,args={}){
  const target=await targetFromArgs(browser,args);
  const eventTypes=Array.isArray(args.eventTypes)&&args.eventTypes.length?args.eventTypes:['largest-contentful-paint','layout-shift','longtask','navigation'];
  await browser.performanceTimelineEnable(target,eventTypes).catch(()=>null);
  await browser.traceStart(target,args.categories);
  let exerciseResult=null;
  try{
    if(typeof args.exerciseExpression==='string'&&args.exerciseExpression.trim()) exerciseResult=await browser.jsValue(target,args.exerciseExpression);
    if(typeof args.waitExpression==='string'&&args.waitExpression.trim()) await browser.wait(target,args.waitExpression,optionalInt(args,'timeoutMs',30000,1,300000));
  } finally {}
  const trace=await browser.traceStop(target,optionalInt(args,'traceTimeoutMs',60000,1000,300000));
  const [timeline,metrics,analysis]=await Promise.all([
    browser.performanceTimeline(target,undefined,500).catch(()=>[]), browser.performance(target), analyzeTrace(trace.artifact.path,{top:30,minDurationUs:Number(args.minDurationUs||50000)})
  ]);
  return {target,exerciseResult,trace,metrics,timeline,analysis};
}
