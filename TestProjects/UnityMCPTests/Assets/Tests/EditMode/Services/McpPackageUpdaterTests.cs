using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Services;
using NUnit.Framework;

namespace MCPForUnityTests.Editor.Services
{
    public class McpPackageUpdaterTests
    {
        [Test]
        public void SplitGitSource_DropsPackagePathAndKeepsBranch()
        {
            var (url, reference) = McpPackageUpdater.SplitGitSource("https://github.com/nongvietanh/novahn-unity-mcp.git?path=/MCPForUnity#main");
            Assert.AreEqual("https://github.com/nongvietanh/novahn-unity-mcp.git", url);
            Assert.AreEqual("main", reference);
        }

        [Test]
        public void SplitGitSource_DefaultsToHeadAndStripsGitPlus()
        {
            var (url, reference) = McpPackageUpdater.SplitGitSource("git+https://example.com/repo.git");
            Assert.AreEqual("https://example.com/repo.git", url);
            Assert.AreEqual("HEAD", reference);
        }

        [Test]
        public void UpdateSource_KeepsBranchOfTheNovahnRepository()
        {
            Assert.AreEqual(ProductInfo.PackageGitUrl + "#beta",
                McpPackageUpdater.UpdateSource("https://github.com/nongvietanh/novahn-unity-mcp.git?path=/MCPForUnity#beta"));
            Assert.AreEqual(ProductInfo.PackageGitUrl + "#main",
                McpPackageUpdater.UpdateSource("https://nongvietanh@github.com/nongvietanh/novahn-unity-mcp.git?path=/MCPForUnity"));
        }

        [Test]
        public void UpdateSource_MovesOtherInstallsToNovahnMain()
        {
            Assert.AreEqual(ProductInfo.PackageGitUrl + "#main",
                McpPackageUpdater.UpdateSource("https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#beta"));
            Assert.AreEqual(ProductInfo.PackageGitUrl + "#main", McpPackageUpdater.UpdateSource(null));
        }

        [Test]
        public void ParseLsRemote_ReadsFirstCommit()
        {
            string output = "0123456789abcdef0123456789abcdef01234567\trefs/heads/main\n";
            Assert.AreEqual("0123456789abcdef0123456789abcdef01234567", McpPackageUpdater.ParseLsRemote(output));
            Assert.IsNull(McpPackageUpdater.ParseLsRemote(""));
        }
    }
}
