import { targetFromArgs, optionalInt } from '../common/lib.mjs';

export default async function collectDebugSummary(browser, args = {}) {
  const target = await targetFromArgs(browser,args);
  const limit = optionalInt(args,'limit',50,1,500);
  const [context,surface,consoleEntries,exceptions,network,performance,dialog,paused] = await Promise.all([
    browser.current(), browser.observe(target), browser.console(target,limit), browser.exceptions(target,limit),
    browser.network({target,limit}), browser.performance(target), browser.dialog(target), browser.runtimePaused(target).catch(()=>null)
  ]);
  return {
    target, context,
    surface:{document:surface.document,url:surface.url,title:surface.title,cursor:surface.cursor,semanticObjects:surface.elements?.length||0,providers:surface.providers},
    console:consoleEntries.slice(-limit), exceptions:exceptions.slice(-limit), network:network.slice(-limit), performance, dialog, paused,
    counts:{console:consoleEntries.length,exceptions:exceptions.length,network:network.length}
  };
}
