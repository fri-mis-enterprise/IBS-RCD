$(function () {
    function source() {
        const sales = $('input[name="Source"]:checked').val() === 'Sales Invoice';
        const service = $('input[name="Source"]:checked').val() === 'Service Invoice';
        $('#siList').toggle(sales).find('input,select').prop('disabled', !sales);
        $('#svList').toggle(service).find('input,select').prop('disabled', !service);
    }
    $('input[name="Source"]').on('change', source);
    $('#ServiceInvoiceId').on('change', function () {
        const id = $(this).val();
        if (!id) return;
        $.getJSON($(this).closest('form').data('service-details-url'), { svId: id })
            .done(function (data) { if (data && data.period) $('#Period').val(data.period.substring(0, 7)); })
            .fail(function () { alert('Unable to load the service invoice period. Please select the invoice again.'); });
    });
    source();
});
