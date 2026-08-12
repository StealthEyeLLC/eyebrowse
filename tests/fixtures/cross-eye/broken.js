globalThis.crossEyeFixture = { phase: 'broken', result: null };
console.error('Build002 cross-Eye controlled source failure');
globalThis.crossEyeFixture.result = missingCrossEyeDependency.value;
