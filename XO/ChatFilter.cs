using System.Text.RegularExpressions;

namespace XO
{
    public static class ChatFilter
    {
        private static readonly string[] BlackList = { "дебил", "сука", "даун", "пиздец" };

        public static string Filter(string message)
        {
            foreach (var w in BlackList)
                message = Regex.Replace(message, $@"\b{Regex.Escape(w)}\b", "***", RegexOptions.IgnoreCase);
            return message;
        }
    }
}