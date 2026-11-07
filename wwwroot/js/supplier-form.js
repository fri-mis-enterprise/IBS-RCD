$(function () {
    function section(id, visible) {
        const box = $(id);
        box.toggle(visible);
        box.find('input,select,textarea').prop('disabled', !visible);
    }
    function refresh() {
        const category = $('input[name="Category"]:checked').val();
        section('#tradeList', ['Trade', 'Commissionee', 'Hauler'].includes(category));
        section('#nonTradeList', category === 'Non-Trade');
        section('#employeeList', category === 'Employee');
        $('#EmployeeNumber').prop('required', category === 'Employee');
        const exempt = $('#TaxType').val() === 'Exempt';
        const withheld = $('#TaxType').val() === 'Withholding Tax';
        section('#supplier-tax-rate', withheld);
        section('#WithholdingTaxTitle', withheld);
        $('#supplier-tax-rate select').prop('required', withheld);
        section('#exemptionFields', exempt);
        const temporary = exempt && $('input[name="Validity"]:checked').val() === 'Temporary';
        section('#validityDateField', temporary);
        $('#ValidityDate').prop('required', temporary);
    }
    $('input[name="Category"], #TaxType, input[name="Validity"]').on('change', refresh);
    refresh();
});
