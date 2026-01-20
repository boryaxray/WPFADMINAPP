using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WPFAPP.Utils
{
    public static class HashUtils
    {
        public static string CalculateSHA256(string filePath)
        {
            try
            {
                using (SHA256 sha256 = SHA256.Create())
                using (FileStream stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HashUtils.CalculateSHA256 error: {ex.Message}");
                return null;
            }
        }

        public static bool ValidateHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            if (hash.Length != 64)
                return false;

            foreach (char c in hash)
            {
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'a' && c <= 'f') ||
                      (c >= 'A' && c <= 'F')))
                    return false;
            }

            return true;
        }

        public static bool IsSystemFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string lowerPath = filePath.ToLowerInvariant();

            return lowerPath.Contains(@"\windows\") ||
                   lowerPath.Contains(@"\system32\") ||
                   lowerPath.Contains(@"\syswow64\");
        }
    }
}