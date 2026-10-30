using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LOSTARK_GEM_FINDER.Core;

namespace LOSTARK_GEM_FINDER.Services
{
    // 🌟 Windows DPAPI로 암호화해서 저장 → 같은 Windows 사용자 계정에서만 복호화 가능
    public class ApiKeyStore : IAPIKeyStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(AppConstants.APP_NAME);

        public string Load()
        {
            string path = AppConstants.KeyFilePath;
            if (!File.Exists(path)) return null;

            try
            {
                byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException)
            {
                return null;   // 다른 PC·계정에서 만든 파일 → 키를 다시 입력받음
            }
            catch (IOException)
            {
                return null;
            }
        }

        public void Save(string apiKey)
        {
            Directory.CreateDirectory(AppConstants.DataDirectory);

            byte[] cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), Entropy, DataProtectionScope.CurrentUser);
            string tempPath = AppConstants.KeyFilePath + ".tmp";
            File.WriteAllBytes(tempPath, cipher);

            if (File.Exists(AppConstants.KeyFilePath)) File.Delete(AppConstants.KeyFilePath);
            File.Move(tempPath, AppConstants.KeyFilePath);
        }
    }
}