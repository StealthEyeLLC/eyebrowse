globalThis.crossEyeFixture = { phase: 'fixed', result: 42 };
const status = document.querySelector('#cross-eye-status');
if (status) status.textContent = 'fixed:42';
console.log('Build002 cross-Eye source fixed');
