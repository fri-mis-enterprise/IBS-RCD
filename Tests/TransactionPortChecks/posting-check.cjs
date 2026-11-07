const fs=require('fs'),crypto=require('crypto'),assert=require('assert/strict');
function methods(s){s=s.replace(/\r\n/g,'\n');const found=[];for(const m of s.matchAll(/(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?[\w?<>,.\[\] ]+\s+(\w+)\s*\([^)]*\)\s*(?:where[^\{]+)?\{/g)){let start=m.index,d=1,i=m.index+m[0].length;for(;i<s.length&&d;i++){if(s[i]==='{')d++;if(s[i]==='}')d--;}found.push({name:m[1],body:s.slice(start,i)});}return found;}
function hash(s){return crypto.createHash('sha256').update(s).digest('hex');}
const baseline=JSON.parse(fs.readFileSync(__dirname+'/posting-baseline.json','utf8'));
for(const item of baseline){const candidates=methods(fs.readFileSync(item.file,'utf8')).filter(m=>m.name===item.name);assert(candidates.some(m=>hash(m.body)===item.sha256),item.file+' '+item.name+' changed from the RCD baseline');}
console.log('PASS: '+baseline.length+' existing posting, void, cancellation and ledger methods are unchanged.');
