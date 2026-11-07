$(function () {
    $('.receipt-bank-form').each(function () {
        const form = $(this), bank = form.find('[name="bankId"]');
        if (bank.length) $.getJSON(form.data('banks-url')).done(function (data) {
            data.forEach(item => bank.append(new Option(item.text, item.value)));
        }).fail(function () { alert('Unable to load bank accounts. Please reload the page.'); });
        form.on('submit', function (event) {
            event.preventDefault();
            const buttons = form.find('button').prop('disabled', true);
            $.ajax({ url: form.attr('action'), method: 'POST', data: form.serialize() })
                .done(function () { window.location.reload(); })
                .fail(function (xhr) { alert(xhr.responseText || 'Unable to save bank information.'); buttons.prop('disabled', false); });
        });
    });
});
