using RcloneHelper.Helpers;
using Xunit;

namespace RcloneHelper.Tests;

public class PathUtilTests
{
    [Fact]
    public void AppDataDir_ShouldNotBeEmpty()
    {
        // Arrange & Act
        var path = PathUtil.AppDataDir;

        // Assert
        Assert.False(string.IsNullOrEmpty(path));
        Assert.Contains("RcloneHelper", path);
    }

    [Fact]
    public void MountsConfigPath_ShouldEndWithMountsJson()
    {
        // Arrange & Act
        var path = PathUtil.MountsConfigPath;

        // Assert
        Assert.EndsWith("mounts.json", path);
    }

    [Fact]
    public void SettingsPath_ShouldEndWithSettingsJson()
    {
        // Arrange & Act
        var path = PathUtil.SettingsPath;

        // Assert
        Assert.EndsWith("settings.json", path);
    }

    [Fact]
    public void LogPath_ShouldContainLog()
    {
        // Arrange & Act
        var path = PathUtil.LogPath;

        // Assert
        Assert.Contains("log", path);
    }
}