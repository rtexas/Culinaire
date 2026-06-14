namespace Portal.Models;

public sealed class PayrollBatch
{
    public int      BatchID           { get; set; }
    public int      LocationID        { get; set; }
    public string   LocationName      { get; set; } = string.Empty;
    public string   BatchNameTemplate { get; set; } = string.Empty;
    public string   PayPeriodLength   { get; set; } = string.Empty;
    /// <summary>Required when PayPeriodLength is "Every Two Weeks" or "Weekly".</summary>
    public string?  StartDayOfWeek    { get; set; }
    public bool     IsActive          { get; set; } = true;
    public DateTime CreatedAt         { get; set; }
}
