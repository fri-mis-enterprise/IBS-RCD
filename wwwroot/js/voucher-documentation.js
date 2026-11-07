$(function () {
    $('.voucher-documentation').each(function () {
        const section = $(this);
        function toggle() {
            const enabled = section.find('.documenting-company-toggle').prop('checked');
            section.find('.documenting-company-fields').toggle(enabled).find('input').prop('disabled', !enabled).prop('required', enabled);
        }
        section.find('.documenting-company-toggle').on('change', toggle);
        toggle();
    });
});
