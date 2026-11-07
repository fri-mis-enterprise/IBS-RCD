$(function () {
    function station() { $('#station-div').toggle($('select[name="CustomerType"]').val() === 'Retail'); }
    function withholding(flag, container, field, defaultRate) {
        const checked = $(flag).prop('checked');
        $(container).toggle(checked);
        const rate = $(field);
        if (!checked) rate.val('0').trigger('change');
        else if (!Number(rate.val())) rate.val(defaultRate).trigger('change');
    }
    function vat() { withholding('#WithHoldingVat', '#WithholdingVatPercent', '#CwVatPercent', '0.05'); }
    function tax() { withholding('#WithHoldingTax', '#WithholdingTaxPercent', '#CwtPercent', '0.01'); }
    $('select[name="CustomerType"]').on('change', station);
    $('#WithHoldingVat').on('change', vat);
    $('#WithHoldingTax').on('change', tax);
    station(); vat(); tax();
});
