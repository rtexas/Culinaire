import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side

# (Name, Section, Description)
rows = [
    # ── Sales (+1) — food & beverage revenue ──────────────────────────────
    ("Food Sales",               "Sales", "Total food revenue for the period"),
    ("Beverage Sales",           "Sales", "Total non-alcoholic beverage revenue"),
    ("Beer Sales",               "Sales", "Draft and bottled beer revenue"),
    ("Wine Sales",               "Sales", "Bottled and by-the-glass wine revenue"),
    ("Liquor Sales",             "Sales", "Spirits and cocktail revenue"),
    ("Appetizers",               "Sales", "Starter and appetizer category sales"),
    ("Entrees",                  "Sales", "Main course / entrée category sales"),
    ("Desserts",                 "Sales", "Dessert category sales"),
    ("Non-Alcoholic Beverages",  "Sales", "Soft drinks, juices, coffee, and tea revenue"),
    ("Catering Sales",           "Sales", "Off-site and on-site catering revenue"),
    ("Private Event Sales",      "Sales", "Private dining and buyout event revenue"),
    ("Delivery Sales",           "Sales", "Third-party and in-house delivery revenue"),
    ("Takeout Sales",            "Sales", "To-go and curbside pickup revenue"),
    ("Gift Card Sales",          "Sales", "Gift card purchases for the period"),
    ("Merchandise Sales",        "Sales", "Branded merchandise and retail sales"),

    # ── Income (-1) — payment tenders ──────────────────────────────────────
    ("Cash",                     "Income", "Cash payments received"),
    ("Amex",                     "Income", "American Express credit card payments"),
    ("Discover",                 "Income", "Discover credit card payments"),
    ("Visa",                     "Income", "Visa credit card payments"),
    ("Mastercard",               "Income", "Mastercard credit card payments"),
    ("Gift Card Redemption",     "Income", "Gift card redemptions applied as payment"),
    ("House Account",            "Income", "Payments charged to internal house accounts"),

    # ── Liabilities (0) — expenses & obligations ───────────────────────────
    ("Sales Tax",                "Liabilities", "Sales tax collected and due to state/local authority"),
    ("Gratuity",                 "Liabilities", "Auto-gratuity and service charge collected"),
    ("Landscape",                "Liabilities", "Grounds maintenance and landscaping expense"),
    ("Building",                 "Liabilities", "Building rent, lease, or mortgage expense"),
    ("Equipment",                "Liabilities", "Kitchen and facility equipment expense"),
    ("Utilities",                "Liabilities", "Electric, gas, and water expense"),
    ("Insurance",                "Liabilities", "General liability and property insurance expense"),
    ("Repairs & Maintenance",    "Liabilities", "Facility and equipment repair expense"),
    ("Supplies",                 "Liabilities", "Operating supply and consumable expense"),
    ("Credit Card Fees",         "Liabilities", "Merchant processing and transaction fees"),
]

wb = openpyxl.Workbook()
ws = wb.active
ws.title = "EOD Rows"

HDR_BG = "2B6B35"; HDR_FG = "FFFFFF"; ALT = "F0F7F0"
header_fill  = PatternFill("solid", fgColor=HDR_BG)
header_font  = Font(name="Arial", bold=True, color=HDR_FG, size=11)
header_align = Alignment(horizontal="center", vertical="center")
data_font    = Font(name="Arial", size=10)
thin         = Border(
    left=Side(style="thin", color="C8DFC8"), right=Side(style="thin", color="C8DFC8"),
    top=Side(style="thin", color="C8DFC8"),  bottom=Side(style="thin", color="C8DFC8"),
)

# Section colour bands
section_fills = {
    "Sales":       PatternFill("solid", fgColor="EAF4EA"),
    "Income":      PatternFill("solid", fgColor="EAF0FA"),
    "Liabilities": PatternFill("solid", fgColor="FAF4EA"),
}
section_fonts = {
    "Sales":       Font(name="Arial", size=10, color="1A5C1A"),
    "Income":      Font(name="Arial", size=10, color="1A3A6B"),
    "Liabilities": Font(name="Arial", size=10, color="6B4A1A"),
}

headers    = ["Name", "Section", "Description"]
col_widths = [28,     16,        55]

for col, (h, w) in enumerate(zip(headers, col_widths), start=1):
    cell = ws.cell(row=1, column=col, value=h)
    cell.font = header_font; cell.fill = header_fill
    cell.alignment = header_align; cell.border = thin
    ws.column_dimensions[cell.column_letter].width = w
ws.row_dimensions[1].height = 20

for row_idx, (name, section, desc) in enumerate(rows, start=2):
    row_fill = section_fills.get(section, PatternFill())
    row_font = section_fonts.get(section, Font(name="Arial", size=10))
    for col, val in enumerate([name, section, desc], start=1):
        cell = ws.cell(row=row_idx, column=col, value=val)
        cell.font = row_font; cell.fill = row_fill; cell.border = thin
        cell.alignment = Alignment(vertical="center")

ws.freeze_panes = "A2"
out = r"C:\ClaudeOutput\Culinaire\SampleData\EOD Rows.xlsx"
wb.save(out)
print(f"Saved: {out}  ({len(rows)} rows)")
print(f"  Sales:       {sum(1 for _,s,_ in rows if s=='Sales')}")
print(f"  Income:      {sum(1 for _,s,_ in rows if s=='Income')}")
print(f"  Liabilities: {sum(1 for _,s,_ in rows if s=='Liabilities')}")
