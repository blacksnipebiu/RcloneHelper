using RcloneHelper.Helpers;
using Xunit;

namespace RcloneHelper.Tests;

public class SecureStorageHelperTests
{
    [Fact]
    public void Encrypt_WithNull_ReturnsEmpty()
    {
        // Arrange
        string? input = null;

        // Act
        var result = SecureStorageHelper.Encrypt(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Encrypt_WithEmpty_ReturnsEmpty()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var result = SecureStorageHelper.Encrypt(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Encrypt_WithPlainText_ReturnsEncryptedWithPrefix()
    {
        // Arrange
        var input = "test_password";

        // Act
        var result = SecureStorageHelper.Encrypt(input);

        // Assert - On Windows, should be encrypted with prefix
        if (OperatingSystem.IsWindows())
        {
            Assert.StartsWith("ENC:", result);
            Assert.NotEqual(input, result);
        }
        else
        {
            // On non-Windows, returns as-is
            Assert.Equal(input, result);
        }
    }

    [Fact]
    public void Encrypt_WithAlreadyEncrypted_ReturnsSameValue()
    {
        // Arrange
        var input = "ENC:some_encrypted_value";

        // Act
        var result = SecureStorageHelper.Encrypt(input);

        // Assert
        Assert.Equal(input, result);
    }

    [Fact]
    public void Decrypt_WithNull_ReturnsEmpty()
    {
        // Arrange
        string? input = null;

        // Act
        var result = SecureStorageHelper.Decrypt(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Decrypt_WithEmpty_ReturnsEmpty()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var result = SecureStorageHelper.Decrypt(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Decrypt_WithUnencryptedValue_ReturnsSameValue()
    {
        // Arrange
        var input = "plain_text";

        // Act
        var result = SecureStorageHelper.Decrypt(input);

        // Assert
        Assert.Equal(input, result);
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        // Arrange
        var original = "my_secret_password_123!@#";

        // Act
        var encrypted = SecureStorageHelper.Encrypt(original);
        var decrypted = SecureStorageHelper.Decrypt(encrypted);

        // Assert
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void IsEncrypted_WithEncryptedValue_ReturnsTrue()
    {
        // Arrange
        var input = "ENC:some_value";

        // Act
        var result = SecureStorageHelper.IsEncrypted(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEncrypted_WithUnencryptedValue_ReturnsFalse()
    {
        // Arrange
        var input = "plain_value";

        // Act
        var result = SecureStorageHelper.IsEncrypted(input);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEncrypted_WithNull_ReturnsFalse()
    {
        // Arrange
        string? input = null;

        // Act
        var result = SecureStorageHelper.IsEncrypted(input);

        // Assert
        Assert.False(result);
    }
}