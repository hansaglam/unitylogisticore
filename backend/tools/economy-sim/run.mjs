import {readFile} from 'node:fs/promises';
import {comparisonReport} from './report.mjs';
import {simulate} from './model.mjs';
const args=process.argv.slice(2);
try {
 if(args.length===0)process.stdout.write(comparisonReport());
 else if(args.length===2 && args[0]==='--input'){
  const raw=JSON.parse(await readFile(args[1],'utf8'));const inputs=Array.isArray(raw)?raw:[raw];
  if(inputs.length<1 || inputs.length>32)throw new Error('Expected 1..32 scenarios');
  process.stdout.write(JSON.stringify(inputs.map(simulate),null,2)+'\n');
 }else throw new Error('Usage: node run.mjs [--input local-scenarios.json]');
}catch(error){process.stderr.write(`${error.message}\n`);process.exitCode=1;}
