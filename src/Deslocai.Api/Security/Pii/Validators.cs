namespace Deslocai.Security.Pii;

public static class Validators
{
    public static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());

    public static bool IsValidCpf(string raw)
    {
        var d = Digits(raw);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;
        for (int t = 9; t < 11; t++)
        {
            int sum = 0;
            for (int i = 0; i < t; i++) sum += (d[i] - '0') * (t + 1 - i);
            int dv = (sum * 10) % 11 % 10;
            if (dv != d[t] - '0') return false;
        }
        return true;
    }

    public static bool IsValidCnpj(string raw)
    {
        var d = Digits(raw);
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;
        int[] w1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] w2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int Dv(int len, int[] w)
        {
            int sum = 0;
            for (int i = 0; i < len; i++) sum += (d[i] - '0') * w[i];
            int r = sum % 11;
            return r < 2 ? 0 : 11 - r;
        }
        return Dv(12, w1) == d[12] - '0' && Dv(13, w2) == d[13] - '0';
    }

    
    public static bool IsValidLuhn(string raw)
    {
        var d = Digits(raw);
        if (d.Length < 13 || d.Length > 19 || d.Distinct().Count() == 1) return false;
        int sum = 0;
        bool dbl = false;
        for (int i = d.Length - 1; i >= 0; i--)
        {
            int n = d[i] - '0';
            if (dbl) { n *= 2; if (n > 9) n -= 9; }
            sum += n;
            dbl = !dbl;
        }
        return sum % 10 == 0;
    }

    public static bool IsPlausibleDate(string raw)
    {
        var d = Digits(raw);
        if (d.Length != 8) return false;
        int day = int.Parse(d[..2]), month = int.Parse(d[2..4]), year = int.Parse(d[4..]);
        return year is >= 1900 and <= 2100 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);
    }
}
