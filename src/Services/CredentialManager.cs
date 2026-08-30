using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DevBoard.Services
{
    public static class CredentialManager
    {
        private const string Prefix = "github_account_";

        public static bool StoreToken(Guid accountId, string token)
        {
            if (string.IsNullOrEmpty(token))
                return DeleteToken(accountId);

            try
            {
                var key = GetOrCreateMasterKey();
                var nonce = RandomNumberGenerator.GetBytes(12);
                var plain = Encoding.UTF8.GetBytes(token);
                var cipher = new byte[plain.Length];
                var tag = new byte[16];
                using var aes = new AesGcm(key, 16);
                aes.Encrypt(nonce, plain, cipher, tag);

                var payload = new byte[nonce.Length + tag.Length + cipher.Length];
                Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
                Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
                Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
                File.WriteAllBytes(GetCredentialFilePath(accountId), payload);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetToken(Guid accountId)
        {
            try
            {
                var payload = File.ReadAllBytes(GetCredentialFilePath(accountId));
                if (payload.Length < 29)
                    return string.Empty;

                var nonce = payload[..12];
                var tag = payload[12..28];
                var cipher = payload[28..];
                var plain = new byte[cipher.Length];
                using var aes = new AesGcm(GetOrCreateMasterKey(), 16);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool DeleteToken(Guid accountId)
        {
            var deleted = false;
            try
            {
                var file = GetCredentialFilePath(accountId);
                if (File.Exists(file))
                    File.Delete(file);
                deleted = true;
            }
            catch
            {
            }

            return deleted;
        }

        private static byte[] GetOrCreateMasterKey()
        {
            var dir = GetCredentialDirectory();
            var file = Path.Combine(dir, ".master-key");
            if (File.Exists(file))
                return File.ReadAllBytes(file);

            var key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(file, key);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return key;
        }

        private static string GetCredentialFilePath(Guid accountId)
            => Path.Combine(GetCredentialDirectory(), $"{Prefix}{accountId:N}.dat");

        private static string GetCredentialDirectory()
        {
            var root = Native.OS.DataDir;
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Path.GetTempPath(), "DevBoard");
            var dir = Path.Combine(root, "credentials");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
