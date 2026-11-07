$(function () {
    const form = $('#account-form');
    let version = 0;
    function reset(select) { $(select).empty().append(new Option('', '')).val('').trigger('change.select2'); }
    function clearNumber() { $('#AccountNumber').val(''); }
    function fail() { clearNumber(); window.alert('Unable to load accounts. Please select the parent account again.'); }
    function children(parent, target, revision, fourth) {
        if (!parent) return;
        $.getJSON(form.data('children-url'), { number: parent }).done(function (data) {
            if (revision !== version) return;
            if (fourth) {
                const hasChildren = Array.isArray(data) && data.length > 0;
                $('#fourthLevelDiv').toggle(hasChildren);
                $('#fourthLevel').prop('disabled', !hasChildren).prop('required', hasChildren);
                if (!hasChildren) { number(parent, revision); return; }
                $(target).append(new Option('? Add New', 'create-new'));
            }
            (data || []).forEach(item => $(target).append(new Option(item.text, item.value)));
        }).fail(fail);
    }
    function number(parent, revision) {
        if (!parent) return;
        $.getJSON(form.data('number-url'), { parent: parent }).done(function (value) {
            if (revision === version) $('#AccountNumber').val(value);
        }).fail(fail);
    }
    $('#mainLevel').on('change', function () {
        const revision = ++version;
        reset('#secondLevel'); reset('#thirdLevel'); reset('#fourthLevel'); clearNumber();
        $('#fourthLevelDiv').hide(); $('#fourthLevel').prop('disabled', true).prop('required', false);
        children($(this).val(), '#secondLevel', revision, false);
    });
    $('#secondLevel').on('change', function () {
        const revision = ++version;
        reset('#thirdLevel'); reset('#fourthLevel'); clearNumber();
        $('#fourthLevelDiv').hide(); $('#fourthLevel').prop('disabled', true).prop('required', false);
        children($(this).val(), '#thirdLevel', revision, false);
    });
    $('#thirdLevel').on('change', function () {
        const revision = ++version;
        reset('#fourthLevel'); clearNumber();
        $('#fourthLevelDiv').hide(); $('#fourthLevel').prop('disabled', true).prop('required', false);
        children($(this).val(), '#fourthLevel', revision, true);
    });
    $('#fourthLevel').on('change', function () {
        clearNumber();
        number($(this).val() === 'create-new' ? $('#thirdLevel').val() : $(this).val(), ++version);
    });
});
