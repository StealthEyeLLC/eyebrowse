import { resolveGithubContext } from './lib.mjs';
export default async function resolveContext(browser, args = {}) {
  return await resolveGithubContext(browser, args);
}
