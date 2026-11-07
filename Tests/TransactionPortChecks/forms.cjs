const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require(process.env.JSDOM_PATH || 'jsdom');
async function load(module, action, script) {
    const dom = new JSDOM(fs.readFileSync(path.join(process.argv[2], `${module}-${action}.html`), 'utf8'), { runScripts: 'outside-only' });
    const $ = require(process.env.JQUERY_PATH || 'jquery')(dom.window);
    dom.window.$ = dom.window.jQuery = $;
    dom.window.eval(fs.readFileSync(`wwwroot/js/${script}.js`, 'utf8'));
    dom.window.document.dispatchEvent(new dom.window.Event('DOMContentLoaded'));
    await new Promise(resolve => setTimeout(resolve, 20));
    return { dom, $ };
}
(async () => {
    for (const module of ['DebitMemo', 'CreditMemo']) {
        for (const action of ['Create', 'Edit']) {
            const { dom, $ } = await load(module, action, 'memo-form');
            assert.equal($('#SalesInvoiceId').prop('disabled'), false);
            assert.equal($('#ServiceInvoiceId').prop('disabled'), true);
            $('[name="Source"][value="Service Invoice"]').prop('checked', true).trigger('change');
            assert.equal($('#SalesInvoiceId').prop('disabled'), true);
            assert.equal($('#ServiceInvoiceId').prop('disabled'), false);
            $('[name="Source"][value="Sales Invoice"]').prop('checked', true).trigger('change');
            assert.equal($('#SalesInvoiceId').prop('disabled'), false);
            assert.equal($('#ServiceInvoiceId').prop('disabled'), true);
            dom.window.close();
        }
    }
    for (const module of ['CheckVoucherTrade', 'CheckVoucherNonTradeInvoice', 'CheckVoucherNonTradePayment']) {
        for (const action of module === 'CheckVoucherNonTradeInvoice' ? ['Create', 'Edit', 'CreatePayrollInvoice', 'EditPayrollInvoice'] : ['Create', 'Edit']) {
            const { dom, $ } = await load(module, action, 'voucher-documentation');
            const toggle = $('.documenting-company-toggle');
            const company = $('[name="Documentation.DocumentedByCompanyName"]');
            assert.equal(toggle.length, 1);
            assert.equal(company.length, 1);
            toggle.prop('checked', true).trigger('change');
            assert.equal(company.prop('required'), true);
            assert.equal(company.prop('disabled'), false);
            toggle.prop('checked', false).trigger('change');
            assert.equal(company.prop('required'), false);
            assert.equal(company.prop('disabled'), true);
            dom.window.close();
        }
    }
    console.log('PASS memo source selection and voucher documentation toggles');
})().catch(error => { console.error(error); process.exitCode = 1; });
