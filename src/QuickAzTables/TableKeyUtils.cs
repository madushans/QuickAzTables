using System.Text;

namespace QuickAzTables
{
    public static class TableKey
    {
        /// <summary>
        /// Performs a best effort sanitization for the given string to be a valid 
        /// Table Storage partition key or a row key.
        /// This performs a non-exhaustive set of steps and could still result in an
        /// invalid key.
        /// Using <see cref="TableStore"/> will automatically perform this on partition 
        /// and row keys.
        /// For all the requirements for a key, refer to 
        /// https://learn.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model
        /// </summary>
        /// <param name="key"></param>
        /// <param name="invalidCharReplacement"></param>
        /// <returns></returns>
        public static string Sanitize(string? key, string invalidCharReplacement = "")
        {
            return new string((key ?? "")
                .Replace("/", invalidCharReplacement)
                .Replace("\\", invalidCharReplacement)
                .Replace("#", invalidCharReplacement)
                .Replace("?", invalidCharReplacement)
                .Replace("\t", invalidCharReplacement)
                .Replace("\n", invalidCharReplacement)
                .Replace("\r", invalidCharReplacement)
                .Where(c => !char.IsControl(c))
                .Take(1024) // keys allow up to 1 KiB. This only works if all chars are in ASCII range.
                .ToArray());
        }

        /// <summary>
        /// Performs a subset of validations on the given string, to explain the reason
        /// why it may be invalid to be a Table Storage partiton key or row key.
        /// Returns null if the input can be a valid key.
        /// For all the requirements for a key, refer to 
        /// https://learn.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string? Validate(string? key)
        {
            if (key is null) return "key is null";
            if (key == "") return "key is an empty string";
            var bytes = Encoding.UTF8.GetBytes(key).Length;

            if (bytes > 1024) return $"key is {bytes}b which is larger than allowed 1024KiB";

            foreach (var invalidChar in new[] {"/" ,"\\","#" ,"?" ,"\t","\n","\r", "-"})
            {
                var index = key.IndexOf(invalidChar);
                if (index < 0) continue;
                return $"Invalid character '{invalidChar}' found at index {index}";
            }

            for (int i = 0; i < key.Length; i++)
            {
                if (char.IsControl( key[i])) return $"Control character found at index {i}";
            }

            return null;
        }

    }
}