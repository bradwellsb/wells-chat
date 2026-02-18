using System.Security.Cryptography;

namespace CohoistChat.Shared
{
    public class Aes256Cipher
    {
        private readonly byte[] _key;

        public Aes256Cipher(byte[] key)
        {
            _key = key;
        }

        private string DecryptString(string text, string iv)
        {
            if (text is null) return string.Empty;
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = Convert.FromBase64String(iv);
                using var decryptor = aes.CreateDecryptor();

                byte[] bytes = Convert.FromBase64String(text);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }            
        }

        public MessageDto DecryptMessage(MessageDto messageDto)
        {
            return new()
            {
                Payload = DecryptString(messageDto.Payload, messageDto.IV),
                SenderEmail = DecryptString(messageDto.SenderEmail, messageDto.IV),
                SenderDisplayName = DecryptString(messageDto.SenderDisplayName, messageDto.IV),
                TimeSent = DecryptString(messageDto.TimeSent, messageDto.IV)
            };
        }

        public string EncryptString(string text, string iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = Convert.FromBase64String(iv);
                using var encryptor = aes.CreateEncryptor();

                byte[] encryptedData;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(text);
                        }
                        encryptedData = ms.ToArray();
                    }
                }

                return Convert.ToBase64String(encryptedData);
            }
        }

        public MessageDto EncryptMessage(MessageDto messageDto)
        {
            var IV = GenerateIV();
            return new()
            {
                IV = IV,
                Payload = EncryptString(messageDto.Payload, IV),
                SenderEmail = EncryptString(messageDto.SenderEmail, IV),
                SenderDisplayName = EncryptString(messageDto.SenderDisplayName, IV),
                TimeSent = EncryptString(messageDto.TimeSent, IV)
            };
        }

        public static string GenerateIV()
        {
            using var aes = Aes.Create();
            aes.GenerateIV();
            return Convert.ToBase64String(aes.IV);
        }
    }
}
