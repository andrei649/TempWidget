using System;
using System.IO;
using System.Xml;

namespace TempWidget.Tests
{
    internal static class MsixStartupTests
    {
        internal static void Register(Action<string, Action> run)
        {
            run("MSIX manifest declares disabled startup with an explicit startup marker", () =>
            {
                var document = new XmlDocument { XmlResolver = null };
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "AppxManifest.xml"), settings))
                    document.Load(reader);
                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("d", "http://schemas.microsoft.com/appx/manifest/desktop/windows10");
                var extensions = document.SelectNodes("//d:Extension[@Category='windows.startupTask']", namespaces);
                True(extensions.Count == 1);
                var extension = (XmlElement)extensions[0];
                True(extension.GetAttribute("Executable") == "TempWidget.exe");
                True(extension.GetAttribute("EntryPoint") == "Windows.FullTrustApplication");
                string arguments = extension.GetAttribute("Parameters", "http://schemas.microsoft.com/appx/manifest/uap/windows10/10");
                True(arguments == "--startup");
                True(((XmlElement)extension.SelectSingleNode("d:StartupTask", namespaces)).GetAttribute("Enabled") == "false");
                var options = new LaunchOptions(arguments.Split(' '), packaged: true);
                True(options.Startup && !options.Setup && !options.AutoStart);
                True(!options.ShouldShowOnboarding(onboarded: false));
            });
            run("Marked MSIX startup suppresses onboarding and setup chaining after settings reset", () =>
            {
                var options = new LaunchOptions(new[] { "--startup", "--setup", "--autostart" }, packaged: true);
                True(options.Startup && !options.Setup && !options.AutoStart);
                True(!options.ShouldShowOnboarding(false));
                True(!options.ShouldShowOnboarding(true));
            });
            run("Unmarked packaged launch conservatively suppresses onboarding after settings reset", () =>
            {
                // Simulates absent/ignored optional manifest parameters. This does not claim
                // activation-kind detection and deliberately covers manual MSIX launch too.
                var options = new LaunchOptions(new string[0], packaged: true);
                True(!options.Startup && !options.Setup && !options.AutoStart);
                True(!options.ShouldShowOnboarding(onboarded: false));
            });
            run("Unpackaged first manual launch retains onboarding while marked startup skips it", () =>
            {
                var manual = new LaunchOptions(new string[0], packaged: false);
                True(manual.ShouldShowOnboarding(false));
                True(!manual.ShouldShowOnboarding(true));
                True(!new LaunchOptions(new[] { "--startup" }, packaged: false).ShouldShowOnboarding(false));
            });
            run("Packaged explicit setup remains available without automatically showing onboarding", () =>
            {
                var options = new LaunchOptions(new[] { "--restarted", "--setup" }, packaged: true);
                True(!options.Startup && options.Setup && !options.AutoStart);
                True(!options.ShouldShowOnboarding(false));
            });
        }

        private static void True(bool condition) { if (!condition) throw new Exception("MSIX startup assertion failed"); }
    }
}
