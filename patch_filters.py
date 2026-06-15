import re

BASE = r"C:\ClaudeOutput\Culinaire\Portal\Components\Pages\Admin"

PAGES = [
    {
        "file": "AccountTypes.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fName", "_fDesc"],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.AccountType> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _types\n        : _types.Where(x => Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.AccountType> Filtered => _types.Where(x => Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "AccountCategories.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fName", "_fDesc"],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.AccountCategory> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _categories\n        : _categories.Where(x => Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.AccountCategory> Filtered => _categories.Where(x => Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "Countries.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fCode", "_fName", "_fDesc", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.Country> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _items\n        : _items.Where(x => Matches(x.Code) || Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fCode = string.Empty; private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.Country> Filtered => _items.Where(x => Mtch(x.Code,_fCode) && Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "StatesRegions.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fCode", "_fName", "_fDesc", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.StateRegion> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _items\n        : _items.Where(x => Matches(x.Code) || Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fCode = string.Empty; private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.StateRegion> Filtered => _items.Where(x => Mtch(x.Code,_fCode) && Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "ShippingMethods.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fName", "_fDesc", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.ShippingMethod> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _methods\n        : _methods.Where(x => Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.ShippingMethod> Filtered => _methods.Where(x => Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "EodSections.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fName", "_fDesc", None, None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.EodSection> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _items\n        : _items.Where(x => Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.EodSection> Filtered => _items.Where(x => Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "EodColumns.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fName", "_fDesc", None, "_fSegVal", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.EodColumn> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _items\n        : _items.Where(x => Matches(x.Name) || Matches(x.Description) || Matches(x.SegmentValue)).ToList();",
        "new_code": "    private string _fName = string.Empty; private string _fDesc = string.Empty; private string _fSegVal = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.EodColumn> Filtered => _items.Where(x => Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc) && Mtch(x.SegmentValue,_fSegVal)).ToList();",
    },
    {
        "file": "EodRows.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fSection", "_fName", "_fDesc", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.EodRow> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _items\n        : _items.Where(x => Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fSection = string.Empty; private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.EodRow> Filtered => _items.Where(x => Mtch(x.SectionName,_fSection) && Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "Items.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fCode", "_fName", "_fDesc", None, None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.Item> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _items\n        : _items.Where(x => Matches(x.ItemCode) || Matches(x.ItemName) || Matches(x.ItemDescription)).ToList();",
        "new_code": "    private string _fCode = string.Empty; private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.Item> Filtered => _items.Where(x => Mtch(x.ItemCode,_fCode) && Mtch(x.ItemName,_fName) && Mtch(x.ItemDescription,_fDesc)).ToList();",
    },
    {
        "file": "Locations.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fCode", "_fName", "_fAddr", "_fDesc", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.Location> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _items\n        : _items.Where(x => Matches(x.Code) || Matches(x.Name) || Matches(x.Description) || Matches(x.City) || Matches(x.State)).ToList();",
        "new_code": "    private string _fCode = string.Empty; private string _fName = string.Empty; private string _fAddr = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.Location> Filtered => _items.Where(x => Mtch(x.Code,_fCode) && Mtch(x.Name,_fName) && (string.IsNullOrEmpty(_fAddr)||Mtch(x.City,_fAddr)||Mtch(x.State,_fAddr)) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "Vendors.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fCode", "_fName", "_fCity", "_fState", "_fCountry", "_fPayables", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.Vendor> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _vendors\n        : _vendors.Where(x => Matches(x.VendorCode) || Matches(x.Name) || Matches(x.City) || Matches(x.PayablesAccount)).ToList();",
        "new_code": "    private string _fCode = string.Empty; private string _fName = string.Empty; private string _fCity = string.Empty; private string _fState = string.Empty; private string _fCountry = string.Empty; private string _fPayables = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.Vendor> Filtered => _vendors.Where(x => Mtch(x.VendorCode,_fCode) && Mtch(x.Name,_fName) && Mtch(x.City,_fCity) && Mtch(x.State,_fState) && Mtch(x.CountryCode,_fCountry) && Mtch(x.PayablesAccount,_fPayables)).ToList();",
    },
    {
        "file": "DepartmentSetup.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fCode", "_fName", "_fDesc", None, None, None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Department> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _depts\n        : _depts.Where(x => Matches(x.Code) || Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": "    private string _fCode = string.Empty; private string _fName = string.Empty; private string _fDesc = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Department> Filtered => _depts.Where(x => Mtch(x.Code,_fCode) && Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc)).ToList();",
    },
    {
        "file": "JobRoles.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fExtId", "_fName", "_fDesc", "_fPayType", "_fStatus", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<JobRole> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _roles\n        : _roles.Where(x => Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": '    private string _fExtId = string.Empty; private string _fName = string.Empty; private string _fDesc = string.Empty; private string _fPayType = string.Empty; private string _fStatus = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<JobRole> Filtered => _roles.Where(x => Mtch(x.ExternalID,_fExtId) && Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc) && (string.IsNullOrEmpty(_fPayType)||(x.IsExempt?"Exempt":"Hourly").Contains(_fPayType,StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrEmpty(_fStatus)||(x.IsActive?"Active":"Inactive").Contains(_fStatus,StringComparison.OrdinalIgnoreCase))).ToList();',
    },
    {
        "file": "EmployeeSetup.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fExtId", "_fName", "_fDesc", None, "_fStatus", None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Employee> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _employees\n        : _employees.Where(x => Matches(x.ExternalID) || Matches(x.Name) || Matches(x.Description)).ToList();",
        "new_code": '    private string _fExtId = string.Empty; private string _fName = string.Empty; private string _fDesc = string.Empty; private string _fStatus = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Employee> Filtered => _employees.Where(x => Mtch(x.ExternalID,_fExtId) && Mtch(x.Name,_fName) && Mtch(x.Description,_fDesc) && (string.IsNullOrEmpty(_fStatus)||(x.IsActive?"Active":"Inactive").Contains(_fStatus,StringComparison.OrdinalIgnoreCase))).ToList();',
    },
    {
        "file": "Users.razor",
        "filter_row_indent": "                    ",
        "filter_cols": ["_fUsername", "_fFullName", "_fEmail", "_fRole", "_fCity", "_fState", "_fStatus", None, None],
        "old_code": "    private string _filter = string.Empty;\n    private bool Matches(string? s) => s?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false;\n    private List<Portal.Models.User> Filtered => string.IsNullOrWhiteSpace(_filter)\n        ? _users\n        : _users.Where(x => Matches(x.Username) || Matches(x.FullName) || Matches(x.Email) || Matches(x.RoleType)).ToList();",
        "new_code": '    private string _fUsername = string.Empty; private string _fFullName = string.Empty; private string _fEmail = string.Empty; private string _fRole = string.Empty; private string _fCity = string.Empty; private string _fState = string.Empty; private string _fStatus = string.Empty;\n    private static bool Mtch(string? s, string f) => string.IsNullOrEmpty(f) || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);\n    private List<Portal.Models.User> Filtered => _users.Where(x => Mtch(x.Username,_fUsername) && Mtch(x.FullName,_fFullName) && Mtch(x.Email,_fEmail) && Mtch(x.RoleType,_fRole) && Mtch(x.City,_fCity) && Mtch(x.State,_fState) && (string.IsNullOrEmpty(_fStatus)||(x.IsActive?"Active":"Inactive").Contains(_fStatus,StringComparison.OrdinalIgnoreCase))).ToList();',
    },
]

def build_filter_row(indent, cols):
    lines = [f'{indent}<tr class="filter-row">']
    for c in cols:
        if c:
            lines.append(f'{indent}    <th><input type="search" @bind="{c}" @bind:event="oninput" placeholder="Filter…" /></th>')
        else:
            lines.append(f'{indent}    <th></th>')
    lines.append(f'{indent}</tr>')
    return '\n'.join(lines)

ok = 0
fail = 0
for p in PAGES:
    path = BASE + '\\' + p['file']
    try:
        text = open(path, encoding='utf-8').read()

        # 1. Remove filter input from header bar (both indentation variants)
        removed = False
        for pattern in [
            '            <input type="search" placeholder="Filter…" @bind="_filter" @bind:event="oninput"\n                   style="margin-left:auto;width:220px;font-size:13px;padding:4px 8px;border:1px solid var(--color-border);border-radius:4px;" />\n',
            '                <input type="search" placeholder="Filter…" @bind="_filter" @bind:event="oninput"\n                       style="margin-left:auto;width:220px;font-size:13px;padding:4px 8px;border:1px solid var(--color-border);border-radius:4px;" />\n',
        ]:
            if pattern in text:
                text = text.replace(pattern, '')
                removed = True
                break
        if not removed:
            print(f'FAIL {p["file"]}: filter input not found')
            fail += 1
            continue

        # 2. Insert filter row after first </tr> inside <thead>
        filter_row = build_filter_row(p['filter_row_indent'], p['filter_cols'])
        thead_pos = text.find('<thead>')
        if thead_pos == -1:
            print(f'FAIL {p["file"]}: no <thead>')
            fail += 1
            continue
        first_tr_end = text.find('</tr>', thead_pos)
        if first_tr_end == -1:
            print(f'FAIL {p["file"]}: no </tr>')
            fail += 1
            continue
        insert_pos = first_tr_end + len('</tr>')
        text = text[:insert_pos] + '\n' + filter_row + text[insert_pos:]

        # 3. Replace @code filter section
        if p['old_code'] not in text:
            print(f'FAIL {p["file"]}: old_code not found')
            fail += 1
            continue
        text = text.replace(p['old_code'], p['new_code'])

        open(path, 'w', encoding='utf-8').write(text)
        print(f'OK   {p["file"]}')
        ok += 1
    except Exception as e:
        import traceback
        print(f'ERROR {p["file"]}: {e}')
        traceback.print_exc()
        fail += 1

print(f'\nDone: {ok} ok, {fail} failed')
