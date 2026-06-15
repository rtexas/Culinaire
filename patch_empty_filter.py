import re

BASE = r"C:\ClaudeOutput\Culinaire\Portal\Components\Pages\Admin"

PAGES = [
    # (filename, list_var, filtered_var, colspan, no_data_message_snippet)
    ("AccountTypes.razor",      "_types",      "Filtered",        2, None),
    ("AccountCategories.razor", "_categories", "Filtered",        2, None),
    ("Countries.razor",         "_items",      "Filtered",        4, None),
    ("StatesRegions.razor",     "_items",      "Filtered",        4, None),
    ("ShippingMethods.razor",   "_methods",    "Filtered",        3, None),
    ("EodSections.razor",       "_items",      "Filtered",        4, None),
    ("EodColumns.razor",        "_items",      "Filtered",        5, None),
    ("EodRows.razor",           "_items",      "Filtered",        4, None),
    ("Items.razor",             "_items",      "Filtered",        5, None),
    ("Locations.razor",         "_items",      "Filtered",        5, None),
    ("Vendors.razor",           "_vendors",    "Filtered",        7, None),
    ("DepartmentSetup.razor",   "_depts",      "Filtered",        6, None),
    ("JobRoles.razor",          "_roles",      "Filtered",        6, None),
    ("EmployeeSetup.razor",     "_employees",  "Filtered",        6, None),
    ("Users.razor",             "_users",      "Filtered",        9, None),
    ("ChartOfAccounts.razor",   "_accounts",   "FilteredAccounts",6, None),
]

NO_MATCH_STYLE = 'padding:16px;color:var(--color-text-muted);font-style:italic;text-align:center;'

ok = 0
fail = 0
for fname, list_var, filt_var, cols, _ in PAGES:
    path = BASE + '\\' + fname
    try:
        text = open(path, encoding='utf-8').read()

        # 1. Replace outer if-check: @if (Filtered/FilteredAccounts.Count == 0)
        old_outer = f'@if ({filt_var}.Count == 0)'
        new_outer = f'@if ({list_var}.Count == 0)'
        if old_outer not in text:
            print(f'FAIL {fname}: outer check "{old_outer}" not found')
            fail += 1
            continue
        text = text.replace(old_outer, new_outer, 1)  # only first occurrence

        # 2. Insert no-matches row right after <tbody>
        # Find the indentation of <tbody> to match style
        tbody_match = re.search(r'( *)<tbody>', text)
        if not tbody_match:
            print(f'FAIL {fname}: no <tbody> found')
            fail += 1
            continue
        td_indent = tbody_match.group(1)
        inner = td_indent + '    '  # one extra level

        no_match_row = (
            f'\n{inner}@if ({filt_var}.Count == 0 && {list_var}.Count > 0)'
            f'\n{inner}{{'
            f'\n{inner}    <tr><td colspan="{cols}" style="{NO_MATCH_STYLE}">No matching records. Clear the filter to see all items.</td></tr>'
            f'\n{inner}}}'
        )

        # Insert after first <tbody>
        insert_after = '<tbody>'
        pos = text.find(insert_after)
        if pos == -1:
            print(f'FAIL {fname}: no <tbody>')
            fail += 1
            continue
        pos += len(insert_after)
        text = text[:pos] + no_match_row + text[pos:]

        open(path, 'w', encoding='utf-8').write(text)
        print(f'OK   {fname}')
        ok += 1
    except Exception as e:
        import traceback
        print(f'ERROR {fname}: {e}')
        traceback.print_exc()
        fail += 1

print(f'\nDone: {ok} ok, {fail} failed')
