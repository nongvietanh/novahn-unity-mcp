using System.Linq;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Clients.Configurators;
using MCPForUnity.Editor.Services.Hub;
using NUnit.Framework;

namespace MCPForUnityTests.Editor.Hub
{
    public class StudioMachineScanTests
    {
        private sealed class FakeCursor : CursorConfigurator
        {
            public override bool IsInstalled => true;
        }

        private sealed class FakeCodex : CodexConfigurator
        {
            public override bool IsInstalled => true;
        }

        [Test]
        public void ScanMachine_ListsCliFoundOnPath()
        {
            var found = StudioClientSetup.ScanMachine(new IMcpClientConfigurator[] { new CodexConfigurator() },
                exe => exe == "codex" ? @"C:\bin\codex.cmd" : null);

            Assert.AreEqual(new[] { "codex" }, found.Select(d => d.Client.Id).ToArray());
            Assert.AreEqual(@"C:\bin\codex.cmd", found[0].CliPath);
        }

        [Test]
        public void ScanMachine_SkipsCliWithOnlyAConfigurationFolder()
        {
            var found = StudioClientSetup.ScanMachine(new IMcpClientConfigurator[] { new FakeCodex() }, exe => null);
            Assert.IsEmpty(found);
        }

        [Test]
        public void ScanMachine_SkipsInstalledDesktopApps()
        {
            bool probed = false;
            var found = StudioClientSetup.ScanMachine(new IMcpClientConfigurator[] { new FakeCursor() },
                exe => { probed = true; return @"C:\bin\cursor.exe"; });

            Assert.IsEmpty(found);
            Assert.IsFalse(probed);
        }
    }
}
