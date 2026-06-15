namespace Portal.Models;

public sealed class PayableTerm
{
    public int     PayableTermID { get; set; }
    public string  Name         { get; set; } = string.Empty;
    public string  Description  { get; set; } = string.Empty;
    public string  ExternalCode { get; set; } = string.Empty;
    public int?    NumberOfDays { get; set; }

    /// <summary>
    /// "Invoice" = days after invoice date; "Current" = days after today; "FixedDOM" = fixed day of month (uses NumberOfDays as day number)
    /// </summary>
    public string  DateBasis    { get; set; } = "Invoice";

    public DateTime CreatedAt   { get; set; }
    public DateTime UpdatedAt   { get; set; }

    /// <summary>Compute the due date given an invoice date and today's date.</summary>
    public DateTime? ComputeDueDate(DateTime invoiceDate, DateTime today) => DateBasis switch
    {
        "Invoice" when NumberOfDays.HasValue => invoiceDate.AddDays(NumberOfDays.Value),
        "Current" when NumberOfDays.HasValue => today.AddDays(NumberOfDays.Value),
        "FixedDOM" when NumberOfDays is >= 1 and <= 31 =>
            new DateTime(today.Month == 12 && today.Day >= NumberOfDays.Value ? today.Year + 1 : today.Year,
                         today.Month == 12 && today.Day >= NumberOfDays.Value ? 1 : today.Month,
                         1).AddDays(NumberOfDays.Value - 1),
        _ => null
    };
}
