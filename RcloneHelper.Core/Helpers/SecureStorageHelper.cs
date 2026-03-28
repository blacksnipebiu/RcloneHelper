using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace RcloneHelper.Helpers;

/// <summary>
/// 安全存储辅助类，用于加密/解密敏感数据
/// 使用 Windows DPAPI 进行加密
/// </summary>
public static class SecureStorageHelper
{
    // 加密前缀，用于标识已加密的数据
    private const string EncryptedPrefix = "ENC:";

    /// <summary>
    /// 加密敏感数据
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <returns>加密后的字符串（Base64编码）</returns>
    public static string Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        // 如果已经加密，直接返回
        if (plainText.StartsWith(EncryptedPrefix))
            return plainText;

        // 非 Windows 平台暂不支持加密，返回原值
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return plainText;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser);

            return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            // 加密失败，记录诊断信息并返回原值
            Console.Error.WriteLine($"[SecureStorage] 加密失败: {ex.Message}");
            return plainText;
        }
    }

    /// <summary>
    /// 解密敏感数据
    /// </summary>
    /// <param name="encryptedText">加密字符串</param>
    /// <returns>明文</returns>
    public static string Decrypt(string? encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        // 如果没有加密前缀，直接返回（兼容旧数据）
        if (!encryptedText.StartsWith(EncryptedPrefix))
            return encryptedText;

        // 非 Windows 平台，尝试去掉前缀返回
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return encryptedText.Substring(EncryptedPrefix.Length);

        try
        {
            var base64Data = encryptedText.Substring(EncryptedPrefix.Length);
            var encryptedBytes = Convert.FromBase64String(base64Data);
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            // 解密失败，记录诊断信息并返回空字符串
            Console.Error.WriteLine($"[SecureStorage] 解密失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 检查数据是否已加密
    /// </summary>
    public static bool IsEncrypted(string? text)
    {
        return !string.IsNullOrEmpty(text) && text.StartsWith(EncryptedPrefix);
    }
}