import { targetFromArgs } from '../common/lib.mjs';
import lighthouseAudit from './lighthouse-audit.mjs';

export default async function auditAgentReadiness(browser,args={}){
  const target=await targetFromArgs(browser,args);
  const [surface,a11y,webmcp,runtimeTools,context]=await Promise.all([
    browser.observe(target), browser.accessibilityAudit(target), browser.webmcp(target).catch(()=>[]), browser.runtimeTools(target).catch(()=>[]), browser.current()
  ]);
  const formRoles=new Set(['textbox','searchbox','combobox','checkbox','radio','switch','slider','spinbutton']);
  const formObjects=(surface.elements||surface.Elements||[]).filter(x=>formRoles.has(x.role||x.Role));
  const unnamed=formObjects.filter(x=>!String(x.name||x.Name||'').trim()).map(x=>x.id||x.Id);
  const recommendations=[];
  if((a11y.unnamedInteractables??a11y.UnnamedInteractables)>0) recommendations.push('Add stable accessible names/labels to actionable controls.');
  if(formObjects.length&&unnamed.length) recommendations.push('Ensure form controls have deterministic labels and native/ARIA semantics.');
  if(!webmcp.length) recommendations.push('Consider WebMCP for high-value structured actions where UI inference is otherwise brittle.');
  if(runtimeTools.length) recommendations.push('Document page-native runtime tools for developer/debug workflows; keep them page-scoped.');
  let lighthouse=null;
  if(args.lighthouse!==false){
    try { lighthouse=await lighthouseAudit(browser,{target,device:args.device||'desktop',categories:args.categories||['accessibility','best-practices','agentic-browsing'],maxFindings:Number(args.maxFindings||60)}); }
    catch(error){ lighthouse={supported:false,error:error.message}; }
  }
  if(lighthouse?.agenticBrowsing?.some?.(x=>x.score!==1)) recommendations.push('Address failing Lighthouse Agentic Browsing audits to improve deterministic machine operation.');
  return {target,context:{url:context.url,title:context.title,document:context.document},semanticObjects:(surface.elements||surface.Elements||[]).length,formObjects:formObjects.length,unnamedFormObjects:unnamed,a11y,webmcpTools:webmcp.map(x=>({name:x.name,description:x.description,frameId:x.frameId})),runtimeToolGroups:runtimeTools.map(x=>({name:x.name,description:x.description,tools:x.tools?.map(t=>t.name)})),lighthouse,recommendations};
}
