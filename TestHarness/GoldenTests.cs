using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using Advanced_Combat_Tracker;

using NUnit.Framework;

using NWParsing_Plugin;

namespace TestHarness
{
    /// <summary>
    /// Regression tests: parse each committed golden_logs/encounter-N.log and compare
    /// damage_out totals against the expected values in golden_logs_parsed/encounter-N-parsed.json.
    /// A test is generated for each encounter file that has a matching parsed JSON.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class GoldenTests
    {
        // -----------------------------------------------------------------------
        // Fixture discovery
        // -----------------------------------------------------------------------

        private static string FixturesDir()
        {
            string bin = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "fixtures"));
        }

        public static IEnumerable EncounterCases()
        {
            string fixturesDir = FixturesDir();
            string logsDir = Path.Combine(fixturesDir, "golden_logs");
            string parsedDir = Path.Combine(fixturesDir, "golden_logs_parsed");

            if (!Directory.Exists(logsDir) || !Directory.Exists(parsedDir))
                yield break;

            string[] logs = Directory.GetFiles(logsDir, "encounter-*.log");
            Array.Sort(logs);

            foreach (string logFile in logs)
            {
                string baseName = Path.GetFileNameWithoutExtension(logFile);
                string jsonFile = Path.Combine(parsedDir, baseName + "-parsed.json");
                if (File.Exists(jsonFile))
                    yield return new TestCaseData(logFile, jsonFile).SetName(baseName);
            }
        }

        // -----------------------------------------------------------------------
        // Test
        // -----------------------------------------------------------------------

        [Test, TestCaseSource("EncounterCases")]
        public void GoldenEncounter_DamageOut_MatchesExpected(string logFile, string jsonFile)
        {
            var expected = ReadExpected(jsonFile);
            var actual = ParseEncounter(logFile);

            // Every combatant in the golden file must match exactly
            foreach (string name in expected.Keys)
            {
                long exp = expected[name];
                long act;
                actual.TryGetValue(name, out act);
                Assert.AreEqual(exp, act,
                    "damage_out mismatch for '{0}' in {1}", name, Path.GetFileName(logFile));
            }

            // No extra combatants in the actual output that aren't in the golden file
            foreach (string name in actual.Keys)
            {
                Assert.IsTrue(expected.ContainsKey(name),
                    "Unexpected combatant '{0}' in {1} (not in golden file)", name, Path.GetFileName(logFile));
            }
        }

        // -----------------------------------------------------------------------
        // Parsing
        // -----------------------------------------------------------------------

        private static Dictionary<string, long> ParseEncounter(string logFile)
        {
            var form = new FormActMain();
            form.InCombat = true;
            ActGlobals.oFormActMain = form;
            ActGlobals.charName = "";

            var parser = new NW_Parser();
            parser.InitPlugin(new TabPage(), new Label());

            using (var reader = new StreamReader(logFile, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    form.FireLogLine(line);
                }
            }

            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (MasterSwing ms in form.CapturedActions)
            {
                if (ms.SwingType != (int)SwingTypeEnum.Melee) continue;
                if (ms.Damage <= 0) continue;

                long current;
                totals.TryGetValue(ms.Attacker, out current);
                totals[ms.Attacker] = current + (long)ms.Damage;
            }

            return totals;
        }

        // -----------------------------------------------------------------------
        // JSON reader — hand-rolled to match the hand-rolled writer in GoldenFileGenerator
        // -----------------------------------------------------------------------

        private static readonly Regex JsonEntryRe = new Regex(
            @"""([^""]+)""\s*:\s*\{\s*""damage_out""\s*:\s*(\d+)\s*\}",
            RegexOptions.Compiled);

        private static Dictionary<string, long> ReadExpected(string jsonFile)
        {
            string json = File.ReadAllText(jsonFile, Encoding.UTF8);
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (Match m in JsonEntryRe.Matches(json))
                result[m.Groups[1].Value] = long.Parse(m.Groups[2].Value);
            return result;
        }
    }
}
