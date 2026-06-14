namespace Portal.Models;

public sealed class PayrollRun
{
    public int      RunID          { get; set; }
    public int      BatchID        { get; set; }
    public string   BatchName      { get; set; } = string.Empty;
    public int      LocationID     { get; set; }
    public string   LocationName   { get; set; } = string.Empty;
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd   { get; set; }
    public string   Status         { get; set; } = "Saved";   // Saved | Submitted | Voided
    public decimal  GrandTotal     { get; set; }
    public DateTime CreatedAt      { get; set; }
    public DateTime? SubmittedAt   { get; set; }
    public DateTime? VoidedAt      { get; set; }

    public List<PayrollRunLine> Lines { get; set; } = [];
}
