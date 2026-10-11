using System.Globalization;
using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>Палитра и форматирование текста HUD (цвета взяты из веб-прототипа).</summary>
    public static class HudStyle
    {
        public static readonly Color Background = Hex("10151D");
        public static readonly Color Panel = Hex("19212B");
        public static readonly Color Line = Hex("303A47");
        public static readonly Color Muted = Hex("93A0B1");
        public static readonly Color Text = Hex("EDF1F5");
        public static readonly Color Body = Hex("BCC6D2");
        public static readonly Color Gold = Hex("F5C66B");
        public static readonly Color Mint = Hex("8BDDC3");
        public static readonly Color Red = Hex("FA7F89");
        public static readonly Color Orange = Hex("ED9B68");
        public static readonly Color Dark = Hex("202029");

        public const string MutedHex = "#93A0B1";
        public const string GoldHex = "#F5C66B";

        // Обычный пробел как разделитель тысяч: в шрифте может не быть узкого неразрывного пробела.
        private static readonly NumberFormatInfo MoneyFormat = new NumberFormatInfo
        {
            NumberGroupSeparator = " ",
            NumberDecimalDigits = 0
        };

        public static string Money(int value) => value.ToString("N0", MoneyFormat);

        /// <summary>Русское склонение: 1 монета, 2 монеты, 5 монет.</summary>
        public static string Plural(int n, string one, string few, string many)
        {
            int a = Mathf.Abs(n) % 100;
            int b = a % 10;
            if (a > 10 && a < 20) return many;
            if (b == 1) return one;
            if (b >= 2 && b <= 4) return few;
            return many;
        }

        public static string Coins(int n) => Plural(n, "монета", "монеты", "монет");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
