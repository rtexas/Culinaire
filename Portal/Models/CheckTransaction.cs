namespace Portal.Models;

public sealed class CheckTransaction
{
    public int      CheckTransactionID   { get; set; }
    public int      LocationID           { get; set; }
    public int      CheckNumber          { get; set; }
    public DateOnly TransactionDate      { get; set; }
    public int?     VendorID             { get; set; }
    public bool     IsManualVendor       { get; set; }
    public string   ManualVendorName     { get; set; } = string.Empty;
    public string   ManualVendorAddress1 { get; set; } = string.Empty;
    public string   ManualVendorAddress2 { get; set; } = string.Empty;
    public string   ManualVendorCity     { get; set; } = string.Empty;
    public string   ManualVendorState    { get; set; } = string.Empty;
    public string   ManualVendorZip      { get; set; } = string.Empty;
    public decimal  Amount               { get; set; }
    public string   Memo                 { get; set; } = string.Empty;
    public int?     ExpenseAccountID     { get; set; }
    public bool     IsSubmitted          { get; set; }
    public DateTime? SubmittedAt         { get; set; }
    public int?     SubmittedByUserID    { get; set; }
    public bool     IsVoided             { get; set; }
    public DateTime? VoidedAt            { get; set; }
    public int?     VoidedByUserID       { get; set; }
    public int?     CreatedByUserID      { get; set; }
    public DateTime CreatedAt            { get; set; }

    // Joined display
    public string VendorName  { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
}
