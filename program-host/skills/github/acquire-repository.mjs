import { resolveGithubContext } from './lib.mjs';

export default async function acquireRepository(browser, args = {}) {
  const context = await resolveGithubContext(browser, args);
  if (!context.ok) return context;
  if (typeof args.destination !== 'string' || !args.destination) throw new TypeError('destination is required');
  const ref = typeof args.ref === 'string' && args.ref ? args.ref : context.defaultBranch;
  return {
    ok: true,
    resolved: { repository: context.repository, owner: context.owner, repo: context.repo, ref, sourceTarget: context.target, sourceUrl: context.canonicalUrl },
    destination: args.destination,
    handoff: {
      eye: 'CODEeye',
      operation: 'acquire-repository',
      semantics: 'git-working-copy',
      repository: context.repository,
      ref,
      destination: args.destination,
      cloneUrl: `https://github.com/${context.repository}.git`
    },
    note: 'True Git acquisition is intentionally delegated to CODEeye; eyeBROWSE did not implement a private Git engine.'
  };
}
