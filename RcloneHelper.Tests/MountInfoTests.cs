using RcloneHelper.Models;
using Xunit;

namespace RcloneHelper.Tests;

public class MountInfoTests
{
    [Fact]
    public void IsWebDavType_WhenTypeIsWebdav_ReturnsTrue()
    {
        // Arrange
        var mount = new MountInfo { Type = "webdav" };

        // Act & Assert
        Assert.True(mount.IsWebDavType);
    }

    [Fact]
    public void IsWebDavType_WhenTypeIsFtp_ReturnsFalse()
    {
        // Arrange
        var mount = new MountInfo { Type = "ftp" };

        // Act & Assert
        Assert.False(mount.IsWebDavType);
    }

    [Fact]
    public void IsFtpType_WhenTypeIsFtp_ReturnsTrue()
    {
        // Arrange
        var mount = new MountInfo { Type = "ftp" };

        // Act & Assert
        Assert.True(mount.IsFtpType);
    }

    [Fact]
    public void IsSftpType_WhenTypeIsSftp_ReturnsTrue()
    {
        // Arrange
        var mount = new MountInfo { Type = "sftp" };

        // Act & Assert
        Assert.True(mount.IsSftpType);
    }

    [Fact]
    public void IsS3Type_WhenTypeIsS3_ReturnsTrue()
    {
        // Arrange
        var mount = new MountInfo { Type = "s3" };

        // Act & Assert
        Assert.True(mount.IsS3Type);
    }

    [Fact]
    public void DefaultPort_WebdavHttps_Returns443()
    {
        // Arrange
        var mount = new MountInfo { Type = "webdav", UseHttps = true };

        // Act & Assert
        Assert.Equal(443, mount.DefaultPort);
    }

    [Fact]
    public void DefaultPort_WebdavHttp_Returns80()
    {
        // Arrange
        var mount = new MountInfo { Type = "webdav", UseHttps = false };

        // Act & Assert
        Assert.Equal(80, mount.DefaultPort);
    }

    [Fact]
    public void DefaultPort_Ftp_Returns21()
    {
        // Arrange
        var mount = new MountInfo { Type = "ftp" };

        // Act & Assert
        Assert.Equal(21, mount.DefaultPort);
    }

    [Fact]
    public void DefaultPort_Sftp_Returns22()
    {
        // Arrange
        var mount = new MountInfo { Type = "sftp" };

        // Act & Assert
        Assert.Equal(22, mount.DefaultPort);
    }

    [Fact]
    public void ComputedUrl_WebdavWithHost_ReturnsCorrectUrl()
    {
        // Arrange
        var mount = new MountInfo
        {
            Type = "webdav",
            Host = "example.com",
            Port = 443,
            UseHttps = true,
            Path = "/dav"
        };

        // Act
        var url = mount.ComputedUrl;

        // Assert
        Assert.Equal("https://example.com/dav", url);
    }

    [Fact]
    public void ComputedUrl_FtpWithHost_ReturnsCorrectUrl()
    {
        // Arrange
        var mount = new MountInfo
        {
            Type = "ftp",
            Host = "ftp.example.com",
            Port = 21,
            Path = "/uploads"
        };

        // Act
        var url = mount.ComputedUrl;

        // Assert
        Assert.Equal("ftp://ftp.example.com/uploads", url);
    }

    [Fact]
    public void ComputedUrl_SftpWithCustomPort_ReturnsCorrectUrl()
    {
        // Arrange
        var mount = new MountInfo
        {
            Type = "sftp",
            Host = "sftp.example.com",
            Port = 2222,
            Path = "/home/user"
        };

        // Act
        var url = mount.ComputedUrl;

        // Assert
        Assert.Equal("sftp://sftp.example.com:2222/home/user", url);
    }

    [Fact]
    public void ToConfig_FromConfig_RoundTripPreservesData()
    {
        // Arrange
        var original = new MountInfo
        {
            Name = "TestMount",
            RemotePath = "/remote/path",
            LocalDrive = "Z:",
            Type = "webdav",
            Host = "example.com",
            Port = 443,
            User = "testuser",
            Password = "testpass",
            AutoMountOnStart = true,
            UseNetworkMode = false
        };

        // Act
        var config = original.ToConfig();
        var restored = MountInfo.FromConfig(config);

        // Assert
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.RemotePath, restored.RemotePath);
        Assert.Equal(original.LocalDrive, restored.LocalDrive);
        Assert.Equal(original.Type, restored.Type);
        Assert.Equal(original.Host, restored.Host);
        Assert.Equal(original.Port, restored.Port);
        Assert.Equal(original.User, restored.User);
        Assert.Equal(original.Password, restored.Password); // Decrypted back
        Assert.Equal(original.AutoMountOnStart, restored.AutoMountOnStart);
    }

    [Fact]
    public void ToConfig_EncryptsPassword()
    {
        // Arrange
        var mount = new MountInfo
        {
            Name = "Test",
            Password = "secret123"
        };

        // Act
        var config = mount.ToConfig();

        // Assert - Password should be encrypted
        if (OperatingSystem.IsWindows())
        {
            Assert.StartsWith("ENC:", config.Password);
            Assert.NotEqual("secret123", config.Password);
        }
    }
}