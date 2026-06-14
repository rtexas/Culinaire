namespace Portal.Services;

public static class AmountToWords
{
    private static readonly string[] Ones =
        ["", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
         "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN",
         "SEVENTEEN", "EIGHTEEN", "NINETEEN"];
    private static readonly string[] Tens =
        ["", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY"];

    public static string Convert(decimal amount)
    {
        if (amount < 0) return "*** INVALID AMOUNT ***";
        var dollars = (long)Math.Floor(amount);
        var cents   = (int)Math.Round((amount - dollars) * 100);
        var words   = dollars == 0 ? "ZERO" : ConvertInteger(dollars);
        return $"{words} AND {cents:00}/100 DOLLARS";
    }

    private static string ConvertInteger(long n)
    {
        if (n == 0) return "";
        if (n < 20) return Ones[n];
        if (n < 100) return Tens[n / 10] + (n % 10 > 0 ? "-" + Ones[n % 10] : "");
        if (n < 1_000) return Ones[n / 100] + " HUNDRED" + (n % 100 > 0 ? " " + ConvertInteger(n % 100) : "");
        if (n < 1_000_000) return ConvertInteger(n / 1000) + " THOUSAND" + (n % 1000 > 0 ? " " + ConvertInteger(n % 1000) : "");
        if (n < 1_000_000_000) return ConvertInteger(n / 1_000_000) + " MILLION" + (n % 1_000_000 > 0 ? " " + ConvertInteger(n % 1_000_000) : "");
        return ConvertInteger(n / 1_000_000_000) + " BILLION" + (n % 1_000_000_000 > 0 ? " " + ConvertInteger(n % 1_000_000_000) : "");
    }
}
