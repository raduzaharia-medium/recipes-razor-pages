// Small progressive enhancements; every page also works without them.
//
//  data-autocomplete="/lookup?handler=Products"   suggestions while typing
//      data-id-target="NewRow.ItemId"             input (by name) that receives the chosen id
//      data-type-target="NewRow.Type"                 input that receives the chosen item type
//      data-fill='{"Input.CustomerAddress":"address"}' other inputs (by name) filled from the chosen item
//  data-enter="#id"                               Enter inside this container clicks that button instead of saving the form
//  data-confirm="text"                            asks before a button or link is used
//  body[data-autoprint]                           opens the print dialog when the page has loaded

const field = (form, name) => form?.elements[name] ?? document.getElementsByName(name)[0];

function autocomplete(input) {
    const url = input.dataset.autocomplete;
    const minLength = Number(input.dataset.minLength ?? 2);
    const fill = input.dataset.fill ? JSON.parse(input.dataset.fill) : {};
    const list = document.createElement('ul');
    list.className = 'suggestions';
    list.hidden = true;
    input.parentNode.classList.add('autocomplete');
    input.after(list);
    input.setAttribute('autocomplete', 'off');

    let items = [], active = -1, timer, version = 0;
    const close = () => { list.hidden = true; active = -1; };
    const highlight = index => { active = index; [...list.children].forEach((li, i) => li.classList.toggle('active', i === index)); };

    const set = (name, value) => { const target = field(input.form, name); if (target) target.value = value ?? ''; };
    const choose = item => {
        input.value = item.label;
        if (input.dataset.idTarget) set(input.dataset.idTarget, item.id);
        if (input.dataset.typeTarget) set(input.dataset.typeTarget, item.type);
        for (const [name, property] of Object.entries(fill)) set(name, item[property]);
        close();
    };

    input.addEventListener('input', () => {
        if (input.dataset.idTarget) set(input.dataset.idTarget, 0);
        clearTimeout(timer);
        if (input.value.trim().length < minLength) return close();

        timer = setTimeout(async () => {
            const mine = ++version;
            try {
                const response = await fetch(`${url}${url.includes('?') ? '&' : '?'}q=${encodeURIComponent(input.value.trim())}`);
                if (mine !== version || !response.ok) return;
                items = (await response.json()).map(item => ({ ...item, id: item.id ?? item.ID }));
                list.replaceChildren(...items.map(item => {
                    const li = document.createElement('li');
                    li.textContent = item.label;
                    if (item.detail) { const small = document.createElement('small'); small.textContent = item.detail; li.append(small); }
                    li.addEventListener('mousedown', event => { event.preventDefault(); choose(item); });
                    return li;
                }));
                list.hidden = items.length === 0;
                active = -1;
            } catch { close(); }
        }, 150);
    });

    input.addEventListener('keydown', event => {
        if (list.hidden) return;
        if (event.key === 'ArrowDown') { event.preventDefault(); highlight(Math.min(active + 1, items.length - 1)); }
        else if (event.key === 'ArrowUp') { event.preventDefault(); highlight(Math.max(active - 1, 0)); }
        else if (event.key === 'Escape') close();
        else if (event.key === 'Enter' && active >= 0) { event.preventDefault(); event.stopImmediatePropagation(); choose(items[active]); }
    });
    input.addEventListener('blur', () => setTimeout(close, 120));
}

document.querySelectorAll('input[data-autocomplete]').forEach(autocomplete);

// Enter in a line-entry row adds the line instead of submitting the whole form
document.querySelectorAll('[data-enter]').forEach(container => container.addEventListener('keydown', event => {
    if (event.key !== 'Enter' || !(event.target instanceof HTMLInputElement) || event.target.type === 'checkbox' || event.defaultPrevented) return;
    event.preventDefault();
    document.querySelector(container.dataset.enter)?.click();
}));

document.addEventListener('click', event => {
    const item = event.target.closest('[data-confirm]');
    if (item && !confirm(item.dataset.confirm)) event.preventDefault();
});

document.addEventListener('focusin', event => { if (event.target.matches?.('input.num')) event.target.select(); });

if (document.body.hasAttribute('data-autoprint')) addEventListener('load', () => print());

// input[data-filter="#table"] hides the rows (tr[data-search]) of that table that don't contain the typed text
document.querySelectorAll('input[data-filter]').forEach(input => input.addEventListener('input', () => {
    const text = input.value.trim().toLowerCase();
    document.querySelectorAll(`${input.dataset.filter} tr[data-search]`).forEach(row => { row.hidden = text !== '' && !row.dataset.search.includes(text); });
}));

// select[data-sync-check="Input.PrintFiscalReceipt"][data-sync-values="a|b"]: checks that checkbox when the selected value is one of a, b
document.querySelectorAll('select[data-sync-check]').forEach(select => select.addEventListener('change', () => {
    const values = select.dataset.syncValues.split('|');
    // asp-for checkboxes post a hidden twin with the same name; the checkbox is the one of type=checkbox
    [...document.getElementsByName(select.dataset.syncCheck)].filter(e => e.type === 'checkbox').forEach(e => { e.checked = values.includes(select.value); });
}));

// [data-show-when="Input.PrintInvoice"] is shown only while that checkbox is checked (always visible without JavaScript)
document.querySelectorAll('[data-show-when]').forEach(panel => {
    const box = [...document.getElementsByName(panel.dataset.showWhen)].find(e => e.type === 'checkbox');
    if (!box) return;
    const update = () => { panel.hidden = !box.checked; };
    box.addEventListener('change', update);
    update();
});
