using System.Globalization;
using System.Text;

namespace PhotoArchiver.Extensions
{
    internal static class StringExtensions
    {
        public static string RemoveDiacritics(this string input)
        {
            string stFormD = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(input.Length);

            for (int i = 0; i < stFormD.Length; i++)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[i]);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(stFormD[i]);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
