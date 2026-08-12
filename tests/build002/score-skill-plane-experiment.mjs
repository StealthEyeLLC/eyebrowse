import { readFile } from 'node:fs/promises';

function fail(message) { throw new Error(message); }
async function parseJsonFile(path) { return JSON.parse((await readFile(path, 'utf8')).replace(/^\\uFEFF/, '')); }
function median(values) {
  if (!values.length) return null;
  const v = [...values].sort((a,b)=>a-b);
  const m = Math.floor(v.length / 2);
  return v.length % 2 ? v[m] : (v[m-1] + v[m]) / 2;
}
function asInt(value, label) {
  if (!Number.isInteger(value) || value < 0) fail(`${label} must be a non-negative integer`);
  return value;
}
function validateManifest(manifest) {
  if (manifest?.schemaVersion !== 1) fail('manifest schemaVersion must be 1');
  const tasks = manifest?.tasks;
  if (!Array.isArray(tasks) || tasks.length !== 12) fail('manifest must contain exactly 12 tasks');
  const ids = new Set();
  for (const task of tasks) {
    if (!/^T\d{2}$/.test(task?.id || '')) fail(`invalid task id ${task?.id}`);
    if (ids.has(task.id)) fail(`duplicate task id ${task.id}`); ids.add(task.id);
    for (const field of ['category','startUrl','startState','prompt','expectedTruth','successCriteria']) if (typeof task[field] !== 'string' || !task[field]) fail(`${task.id}.${field} is required`);
    if (!Object.hasOwn(task, 'mutationFixtureId')) fail(`${task.id}.mutationFixtureId must be explicit (null allowed)`);
    if (!Array.isArray(task.requiredRelevantSkills) || !Array.isArray(task.allowedBackgroundSkills)) fail(`${task.id} skill expectation arrays are required`);
    if (/\bskill\b/i.test(task.prompt)) fail(`${task.id} prompt names Skills explicitly`);
  }
  const counts = tasks.reduce((m,t)=>(m[t.category]=(m[t.category]||0)+1,m),{});
  const required = {'github-context':4,'github-authority-split':2,'horizontal-forms':1,'horizontal-multi-tab':1,'debugging':1,'webmcp':1,'second-site':1,'negative-selection':1};
  for (const [category,count] of Object.entries(required)) if ((counts[category]||0) < count) fail(`category ${category} requires at least ${count} task(s)`);
  if (!tasks.some(t=>t.multiSkill && t.requiredRelevantSkills.length >= 2)) fail('at least one multi-Skill task is required');
  const s = manifest?.scoring || {};
  for (const field of ['treatmentMinimumSuccesses','minimumSuccessImprovementOverControl','medianRoundTripReductionMinimumFraction','treatmentWrongObjectActionsMaximum','relevantSkillActivationRecallMinimum','perSkillFalsePositiveTaskRateMaximum','minimumUsefulSkillsOnMultiSkillTask','minimumMeaningfulOperationsInOneReasoningInvocation']) if (typeof s[field] !== 'number') fail(`scoring.${field} is required`);
  return {tasks, ids};
}
function validateResultSet(manifest, result, condition) {
  if (result?.schemaVersion !== 1) fail(`${condition}: schemaVersion must be 1`);
  if (result?.condition !== condition) fail(`${condition}: condition field mismatch`);
  if (typeof result?.candidateCommit !== 'string' || !/^[0-9a-f]{40}$/i.test(result.candidateCommit)) fail(`${condition}: candidateCommit must be exact 40-hex commit`);
  if (!Array.isArray(result.subjects) || result.subjects.length !== 12) fail(`${condition}: exactly 12 subjects required`);
  const byTask = new Map(); const subjectIds = new Set();
  for (const row of result.subjects) {
    if (!manifest.ids.has(row.taskId)) fail(`${condition}: unknown task ${row.taskId}`);
    if (byTask.has(row.taskId)) fail(`${condition}: duplicate task ${row.taskId}`);
    if (typeof row.subjectId !== 'string' || !row.subjectId) fail(`${condition}/${row.taskId}: subjectId required`);
    if (subjectIds.has(row.subjectId)) fail(`${condition}: duplicate subjectId ${row.subjectId}`); subjectIds.add(row.subjectId);
    if (typeof row.success !== 'boolean') fail(`${condition}/${row.taskId}: success must be boolean`);
    asInt(row.wrongObjectActions, `${condition}/${row.taskId}.wrongObjectActions`);
    asInt(row.modelBrowserRoundTrips, `${condition}/${row.taskId}.modelBrowserRoundTrips`);
    asInt(row.maxMeaningfulOperationsSingleReasoning, `${condition}/${row.taskId}.maxMeaningfulOperationsSingleReasoning`);
    if (!Array.isArray(row.skillActivations) || row.skillActivations.some(x=>typeof x !== 'string')) fail(`${condition}/${row.taskId}: skillActivations must be string[]`);
    if (!Array.isArray(row.evidence)) fail(`${condition}/${row.taskId}: evidence must be an array`);
    byTask.set(row.taskId,row);
  }
  for (const id of manifest.ids) if (!byTask.has(id)) fail(`${condition}: missing ${id}`);
  return {byTask, candidateCommit: result.candidateCommit};
}

const args = process.argv.slice(2);
if (args.length < 1) {
  console.error('usage: node score-skill-plane-experiment.mjs <task-set.json> [--validate-manifest | <control.json> <treatment.json>]');
  process.exit(2);
}
const manifest = await parseJsonFile(args[0]);
const mv = validateManifest(manifest);
if (args[1] === '--validate-manifest') {
  console.log(JSON.stringify({ok:true, tasks:mv.tasks.length, categories:Object.fromEntries([...new Set(mv.tasks.map(t=>t.category))].map(c=>[c,mv.tasks.filter(t=>t.category===c).length])), multiSkillTasks:mv.tasks.filter(t=>t.multiSkill).map(t=>t.id)},null,2));
  process.exit(0);
}
if (args.length !== 3) fail('control and treatment result files are both required');
const controlRaw = await parseJsonFile(args[1]);
const treatmentRaw = await parseJsonFile(args[2]);
const control = validateResultSet(mv, controlRaw, 'control');
const treatment = validateResultSet(mv, treatmentRaw, 'treatment');
if (control.candidateCommit !== treatment.candidateCommit) fail('control/treatment candidateCommit mismatch');

const tasks = mv.tasks;
const cRows = tasks.map(t=>control.byTask.get(t.id));
const tRows = tasks.map(t=>treatment.byTask.get(t.id));
const controlSuccesses = cRows.filter(x=>x.success).length;
const treatmentSuccesses = tRows.filter(x=>x.success).length;
const paired = tasks.filter(t=>control.byTask.get(t.id).success && treatment.byTask.get(t.id).success);
const cMedian = median(paired.map(t=>control.byTask.get(t.id).modelBrowserRoundTrips));
const tMedian = median(paired.map(t=>treatment.byTask.get(t.id).modelBrowserRoundTrips));
const roundTripReduction = cMedian && cMedian > 0 ? (cMedian - tMedian) / cMedian : null;
const treatmentWrongObjectActions = tRows.reduce((n,x)=>n+x.wrongObjectActions,0);
const controlWrongObjectActions = cRows.reduce((n,x)=>n+x.wrongObjectActions,0);

let relevantExpected=0,relevantObserved=0;
for (const task of tasks) {
  const activated = new Set(treatment.byTask.get(task.id).skillActivations);
  for (const skill of task.requiredRelevantSkills) { relevantExpected++; if (activated.has(skill)) relevantObserved++; }
}
const activationRecall = relevantExpected ? relevantObserved/relevantExpected : 1;
const specialized = (manifest.installedTreatmentSkills||[]).filter(s=>s!=='eyebrowse-operator');
const falsePositiveBySkill = {};
for (const skill of specialized) {
  const irrelevantTasks = tasks.filter(t=>!t.requiredRelevantSkills.includes(skill) && !t.allowedBackgroundSkills.includes(skill));
  const activatedWrongly = irrelevantTasks.filter(t=>treatment.byTask.get(t.id).skillActivations.includes(skill));
  falsePositiveBySkill[skill] = {numerator:activatedWrongly.length, denominator:irrelevantTasks.length, rate:irrelevantTasks.length ? activatedWrongly.length/irrelevantTasks.length : 0, tasks:activatedWrongly.map(t=>t.id)};
}
const maxFalsePositiveRate = Math.max(0,...Object.values(falsePositiveBySkill).map(x=>x.rate));
const multiSkillTasks = tasks.filter(t=>t.multiSkill);
const multiSkillPass = multiSkillTasks.every(t=>{
  const a=new Set(treatment.byTask.get(t.id).skillActivations); return t.requiredRelevantSkills.filter(s=>a.has(s)).length >= manifest.scoring.minimumUsefulSkillsOnMultiSkillTask;
});
const maxMeaningfulOperationsSingleReasoning = Math.max(...tRows.map(x=>x.maxMeaningfulOperationsSingleReasoning));
const s=manifest.scoring;
const checks = {
  treatmentSuccesses: treatmentSuccesses >= s.treatmentMinimumSuccesses,
  successImprovement: treatmentSuccesses-controlSuccesses >= s.minimumSuccessImprovementOverControl,
  medianRoundTripReduction: roundTripReduction !== null && roundTripReduction >= s.medianRoundTripReductionMinimumFraction,
  treatmentWrongObjectActions: treatmentWrongObjectActions <= s.treatmentWrongObjectActionsMaximum,
  activationRecall: activationRecall >= s.relevantSkillActivationRecallMinimum,
  falsePositiveSkillActivation: maxFalsePositiveRate <= s.perSkillFalsePositiveTaskRateMaximum,
  multiSkill: multiSkillPass,
  localExecutionCompression: maxMeaningfulOperationsSingleReasoning >= s.minimumMeaningfulOperationsInOneReasoningInvocation
};
const summary = {
  ok:Object.values(checks).every(Boolean), candidateCommit:control.candidateCommit,
  control:{successes:controlSuccesses,wrongObjectActions:controlWrongObjectActions},
  treatment:{successes:treatmentSuccesses,wrongObjectActions:treatmentWrongObjectActions},
  improvement:treatmentSuccesses-controlSuccesses,
  pairedSuccesses:paired.map(t=>t.id), roundTrips:{controlMedian:cMedian,treatmentMedian:tMedian,reductionFraction:roundTripReduction},
  skillSelection:{relevantExpected,relevantObserved,recall:activationRecall,maxFalsePositiveRate,falsePositiveBySkill,multiSkillTasks:multiSkillTasks.map(t=>t.id),multiSkillPass},
  localExecutionCompression:{maxMeaningfulOperationsSingleReasoning}, checks
};
console.log(JSON.stringify(summary,null,2));
process.exit(summary.ok?0:1);