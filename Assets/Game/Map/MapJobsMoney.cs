namespace LogistiCore.Map
{
    /// <summary>
    /// Presentation-only money from getMyContracts decimal-string minor units.
    /// No float/double. Scale 0..6. Currency exactly three letters.
    /// </summary>
    public static class MapJobsMoney
    {
        public static bool TryFormat(string minorUnits, string currency, int scale, string decimalSeparator, out string display)
        {
            display = "";
            if (string.IsNullOrEmpty(minorUnits) || scale < 0 || scale > 6)
                return false;
            if (string.IsNullOrEmpty(currency) || currency.Length != 3)
                return false;
            for (var i = 0; i < currency.Length; i++)
            {
                if (!char.IsLetter(currency[i]))
                    return false;
            }

            for (var i = 0; i < minorUnits.Length; i++)
            {
                if (minorUnits[i] < '0' || minorUnits[i] > '9')
                    return false;
            }

            var code = currency.ToUpperInvariant();
            var digits = TrimLeadingZeros(minorUnits);
            var sep = string.IsNullOrEmpty(decimalSeparator) ? "." : decimalSeparator;
            if (scale == 0)
            {
                display = Group(digits, sep) + " " + code;
                return true;
            }

            if (digits.Length <= scale)
                digits = new string('0', scale - digits.Length + 1) + digits;
            var split = digits.Length - scale;
            display = Group(digits.Substring(0, split), sep) + sep + digits.Substring(split) + " " + code;
            return true;
        }

        public static bool TryGroupDigits(string digits, string thousandsSeparator, out string grouped)
        {
            grouped = "";
            if (string.IsNullOrEmpty(digits))
                return false;
            for (var i = 0; i < digits.Length; i++)
            {
                if (digits[i] < '0' || digits[i] > '9')
                    return false;
            }

            var decimalSeparator = thousandsSeparator == "." ? "," : ".";
            grouped = Group(TrimLeadingZeros(digits), decimalSeparator);
            return true;
        }

        static string TrimLeadingZeros(string digits)
        {
            var i = 0;
            while (i < digits.Length - 1 && digits[i] == '0')
                i++;
            return digits.Substring(i);
        }

        static string Group(string integer, string decimalSeparator)
        {
            var thousands = decimalSeparator == "," ? "." : ",";
            if (integer.Length <= 3)
                return integer;
            var text = "";
            var remain = integer.Length;
            while (remain > 3)
            {
                remain -= 3;
                text = thousands + integer.Substring(remain, 3) + text;
            }

            return integer.Substring(0, remain) + text;
        }
    }
}
