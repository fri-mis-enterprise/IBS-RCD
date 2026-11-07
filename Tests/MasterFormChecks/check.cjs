const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require(process.env.JSDOM_PATH || 'jsdom');
const output = process.argv[2];
async function load(module, action) {
 const html = fs.readFileSync(path.join(output, `${module}-${action}.html`), 'utf8');
 const dom = new JSDOM(html, { runScripts: 'outside-only' });
 const $ = require(process.env.JQUERY_PATH || 'jquery')(dom.window);
 dom.window.$ = dom.window.jQuery = $;
 dom.window.eval(fs.readFileSync(`wwwroot/js/${module.toLowerCase()}-form.js`, 'utf8'));
 dom.window.document.dispatchEvent(new dom.window.Event('DOMContentLoaded'));
 await new Promise(resolve => setTimeout(resolve, 20));
 return { dom, $, html };
}
(async () => {
 for (const action of ['Create', 'Edit']) {
  const { dom, $, html } = await load('Customer', action);
  assert.equal($('[name="Type"]').length, 0);
  assert.equal($('#station-div').css('display') === 'none', false);
  $('select[name="CustomerType"]').val('Industrial').trigger('change');
  assert.equal($('#station-div').css('display'), 'none');
  assert.equal($('#CwtPercent').val(), '0.02');
  $('#WithHoldingTax').prop('checked', false).trigger('change');
  assert.equal($('#CwtPercent').val(), '0');
  assert.equal($('#WithholdingTaxPercent').css('display'), 'none');
  $('#WithHoldingTax').prop('checked', true).trigger('change');
  assert.equal($('#CwtPercent').val(), '0.01');
  $('#WithHoldingVat').prop('checked', true).trigger('change');
  assert.equal($('#CwVatPercent').val(), '0.05');
  $('#WithHoldingVat').prop('checked', false).trigger('change');
  assert.equal($('#CwVatPercent').val(), '0');
  assert(html.indexOf('name="CreditLimit"') < html.indexOf('name="BusinessStyle"'));
  dom.window.close();
  const supplier = await load('Supplier', action), q = supplier.$;
  assert.equal(q('input[name="Category"]').length, 5);
  const ids = Array.from(supplier.dom.window.document.querySelectorAll('[id]')).map(x=>x.id);
  assert.equal(new Set(ids).size, ids.length, `Duplicate IDs on Supplier ${action}`);
  assert.equal(q('#EmployeeNumber').val(), 'E001');
  assert(q('#EmployeeNumber').prop('required'));
  assert.equal(q('#ValidityDate').val(), '2027-01-01');
  assert(!q('#ValidityDate').prop('disabled'));
  q('#ValidityPermanent').prop('checked',true).trigger('change');
  assert(q('#ValidityDate').prop('disabled'));
  q('#TaxType').val('Withholding Tax').trigger('change');
  assert(q('#ReasonOfExemption').prop('disabled'));
  assert(!q('#WithholdingTaxtitle').prop('disabled'));
  assert.deepEqual(q('#supplier-tax-rate option').map((_,e)=>e.value).get().filter(Boolean), ['0.5','1','2','5','10']);
  for(const category of ['Trade','Commissionee','Hauler','Non-Trade','Employee']) {
   q(`input[name="Category"][value="${category}"]`).prop('checked',true).trigger('change');
   assert.equal(q('#TradeName').prop('disabled'), !['Trade','Commissionee','Hauler'].includes(category));
   assert.equal(q('#DefaultExpenseNumber').prop('disabled'), category !== 'Non-Trade');
   assert.equal(q('#EmployeeNumber').prop('disabled'), category !== 'Employee');
  }
  supplier.dom.window.close();
 }
 for(const module of ['Customer','Supplier','Product','Service','BankAccount','ChartOfAccount']) for(const action of ['Create','Edit']) {
  const dom = new JSDOM(fs.readFileSync(path.join(output,`${module}-${action}.html`),'utf8'));
  assert.equal(dom.window.document.querySelector('form').method, 'post', `${module}/${action} posts`);
  assert(dom.window.document.querySelector('input[name="__RequestVerificationToken"]'), `${module}/${action} antiforgery`);
  dom.window.close();
 }
 const chartDom = new JSDOM(fs.readFileSync(path.join(output,'ChartOfAccount-Create.html'),'utf8'),{runScripts:'outside-only'});
 const cq = require(process.env.JQUERY_PATH || 'jquery')(chartDom.window);
 chartDom.window.$ = chartDom.window.jQuery = cq;
 chartDom.window.alert = message => { throw Error(message); };
 const requests = [];
 cq.getJSON = (url,data) => { const pending = cq.Deferred(); requests.push({url,data,pending}); return pending.promise(); };
 chartDom.window.eval(fs.readFileSync('wwwroot/js/chart-account-form.js','utf8'));
 chartDom.window.document.dispatchEvent(new chartDom.window.Event('DOMContentLoaded'));
 await new Promise(resolve=>setTimeout(resolve,20));
 cq('#mainLevel').append(new chartDom.window.Option('Main','1')).val('1').trigger('change');
 requests.pop().pending.resolve([{value:2,text:'Second'}]);
 cq('#secondLevel').val('2').trigger('change');
 requests.pop().pending.resolve([{value:3,text:'Third'}]);
 cq('#thirdLevel').val('3').trigger('change');
 requests.pop().pending.resolve([]);
 assert(cq('#fourthLevel').prop('disabled'));
 assert(!cq('#fourthLevel').prop('required'));
 requests.pop().pending.resolve('100001');
 assert.equal(cq('#AccountNumber').val(),'100001');
 cq('#thirdLevel').trigger('change');
 requests.pop().pending.resolve([{value:4,text:'Fourth'}]);
 assert(!cq('#fourthLevel').prop('disabled'));
 cq('#fourthLevel').val('create-new').trigger('change');
 const stale = requests.pop();
 cq('#mainLevel').val('').trigger('change');
 stale.pending.resolve('stale-number');
 assert.equal(cq('#AccountNumber').val(),'');
 assert.equal(cq('#thirdLevel option').length,1);
 chartDom.window.close();
 console.log('PASS: account cascade, hidden required field, new child numbering and stale response handling.');
 console.log('PASS: 12 rendered master forms; Customer rates/station; all Supplier categories, tax, validity and retained edit values.');
})().catch(e=>{console.error(e);process.exitCode=1;});
