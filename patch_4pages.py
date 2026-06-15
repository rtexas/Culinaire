BASE = r"C:\ClaudeOutput\Culinaire\Portal\Components\Pages\Admin"
NO_MATCH = 'padding:16px;color:var(--color-text-muted);font-style:italic;text-align:center;'

PAGES = [
    ("DepartmentSetup.razor", "Filtered", "_depts",     6),
    ("JobRoles.razor",        "Filtered", "_roles",     6),
    ("EmployeeSetup.razor",   "Filtered", "_employees", 6),
    ("Users.razor",           "Filtered", "_users",     9),
]

for fname, filt_var, list_var, cols in PAGES:
    path = BASE + '\\' + fname
    text = open(path, encoding='utf-8').read()

    # Insert no-match row after first <tbody>
    insert_after = '                <tbody>\n'
    block = (
        f'                <tbody>\n'
        f'                    @if ({filt_var}.Count == 0 && {list_var}.Count > 0)\n'
        f'                    {{\n'
        f'                        <tr><td colspan="{cols}" style="{NO_MATCH}">No matching records. Clear the filter to see all items.</td></tr>\n'
        f'                    }}\n'
    )

    if insert_after not in text:
        print(f'FAIL {fname}: tbody not found with expected indentation')
        continue

    text = text.replace(insert_after, block, 1)
    open(path, 'w', encoding='utf-8').write(text)
    print(f'OK   {fname}')
