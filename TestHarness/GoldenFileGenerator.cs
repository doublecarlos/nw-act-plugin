using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Advanced_Combat_Tracker;

using NUnit.Framework;

using NWLogParsing;

namespace TestHarness
{
    /// <summary>
    /// Generates encounter-N-parsed.json golden files from encounter-N.log fixtures.
    /// Run explicitly (not part of the normal test suite):
    ///   dotnet test --filter "FullyQualifiedName~GoldenFileGenerator"
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class GoldenFileGenerator
    {
        private static string FixturesDir()
        {
            // Executable lives at TestHarness/bin/Debug|Release/net48/ — three levels up is TestHarness/
            string bin = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "fixtures"));
        }

        [Test, Explicit("Generates golden files — run manually, not in CI")]
        public void Generate_AllEncounters()
        {
            string fixturesDir = FixturesDir();
            string logsDir = Path.Combine(fixturesDir, "golden_logs");
            string parsedDir = Path.Combine(fixturesDir, "golden_logs_parsed");
            Directory.CreateDirectory(parsedDir);

            string[] logs = Directory.GetFiles(logsDir, "encounter-*.log");
            Array.Sort(logs); // lexicographic is fine; files are named encounter-1 … encounter-N

            Assert.Greater(logs.Length, 0, "No encounter-N.log files found in " + logsDir);

            foreach (string logFile in logs)
            {
                string baseName = Path.GetFileNameWithoutExtension(logFile); // e.g. encounter-3
                string jsonFile = Path.Combine(parsedDir, baseName + "-parsed.json");

                var totals = ParseEncounter(logFile);
                WriteJson(totals, jsonFile);

                Console.WriteLine("Wrote " + jsonFile + " (" + totals.Count + " combatants)");
            }
        }

        // -----------------------------------------------------------------------
        // Parsing
        // -----------------------------------------------------------------------

        private Dictionary<string, long> ParseEncounter(string logFile)
        {
            var form = new FormActMain();
            form.InCombat = true;
            ActGlobals.oFormActMain = form;
            ActGlobals.charName = "";

            var parser = new NWParserActPlugin();
            parser.InitPlugin(new TabPage(), new Label());

            using (var reader = new StreamReader(logFile, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;
                    form.FireLogLine(line);
                }
            }

            // Aggregate damage_out per attacker (SwingType == Melee && Damage > 0)
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (MasterSwing ms in form.CapturedActions)
            {
                if (ms.SwingType != (int)SwingTypeEnum.Melee)
                    continue;
                if (ms.Damage <= 0)
                    continue;

                long dmg = (long)ms.Damage;
                long current;
                totals.TryGetValue(ms.Attacker, out current);
                totals[ms.Attacker] = current + dmg;
            }

            return totals;
        }

        // -----------------------------------------------------------------------
        // JSON serialisation (hand-rolled to avoid adding a dependency)
        // -----------------------------------------------------------------------

        private static void WriteJson(Dictionary<string, long> totals, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            int idx = 0;
            foreach (var kv in totals)
            {
                string comma = (idx < totals.Count - 1) ? "," : "";
                sb.AppendLine("  " + JsonString(kv.Key) + ": {");
                sb.AppendLine("    \"damage_out\": " + kv.Value);
                sb.AppendLine("  }" + comma);
                idx++;
            }

            sb.Append("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string JsonString(string s)
        {
            // Escape the few characters that matter in combatant names
            s = s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
            return "\"" + s + "\"";
        }
    }
}
