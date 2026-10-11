using System.Globalization;
using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>Палитра мультяшного ограбления и форматирование текста HUD.</summary>
    public static class HudStyle
    {
        public static readonly Color Background = Hex("10151D");
        public static readonly Color Panel = Hex("252238");
        public static readonly Color Line = Hex("474158");
        public static readonly Color Muted = Hex("A5A5B5");
        public static readonly Color Text = Hex("F6E8CB");
        public static readonly Color Body = Hex("DED4C5");
        public static readonly Color Gold = Hex("F3BA55");
        public static readonly Color Mint = Hex("66B9AB");
        public static readonly Color Red = Hex("EE655F");
        public static readonly Color Orange = Hex("ED9B68");
        public static readonly Color Dark = Hex("202029");

        public const string MutedHex = "#A5A5B5";
        public const string GoldHex = "#F3BA55";

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
