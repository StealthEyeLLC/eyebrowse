import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';
const cmd=process.argv[2];
const browser=await EyeBrowse.connect();
try{
  if(cmd==='open'){
    const x=await browser.open(process.argv[3]);
    await browser.activate(x.target.id);
    await browser.wait(x.target.id,"document.readyState === 'complete'",10000,50).catch(()=>null);
    const surface=await browser.observe(x.target.id);
    console.log(JSON.stringify({target:x.target.id,document:surface.document,url:await browser.jsValue(x.target.id,'location.href'),title:await browser.jsValue(x.target.id,'document.title')}));
  } else if(cmd==='form-ids'){
    const target=process.argv[3];
    await browser.activate(target); await browser.observe(target);
    const pick=async (role,name)=>{const x=await browser.query({target,role,name,limit:10});return x[0]??null};
    console.log(JSON.stringify({
      name:await pick('textbox','Name'),
      role:await pick('combobox','Role'),
      enabled:await pick('checkbox','Enabled'),
      notes:await pick('textbox','Notes'),
      submit:await pick('button','Submit')
    },null,2));
  } else if(cmd==='value'){
    console.log(JSON.stringify(await browser.jsValue(process.argv[3],process.argv[4]),null,2));
  } else if(cmd==='current'){
    console.log(JSON.stringify(await browser.current(),null,2));
  } else if(cmd==='activate'){
    await browser.activate(process.argv[3]); console.log(JSON.stringify(await browser.current(),null,2));
  } else throw new Error(`unknown command ${cmd}`);
} finally { browser.close(); }