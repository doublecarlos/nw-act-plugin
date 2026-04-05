using System;
using System.Threading;
using System.Windows.Forms;

using Advanced_Combat_Tracker;

using NUnit.Framework;

using NWParsing_Plugin;

namespace TestHarness
{
    // WinForms controls (TreeView in FormActMain) require STA thread.
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ParserTests
    {
        private FormActMain _form;
        private NW_Parser _parser;

        [SetUp]
        public void SetUp()
        {
            // Reset static state between tests
            _form = new FormActMain();
            _form.InCombat = true;
            ActGlobals.oFormActMain = _form;
            ActGlobals.charName = "";

            _parser = new NW_Parser();
            _parser.InitPlugin(new TabPage(), new Label());
        }

        // Helper: fire a single log line and return all captured MasterSwing objects.
        private System.Collections.Generic.List<MasterSwing> Fire(string logLine)
        {
            _form.CapturedActions.Clear();
            _form.FireLogLine(logLine);
            return _form.CapturedActions;
        }

        // -----------------------------------------------------------------------
        // Basic damage
        // -----------------------------------------------------------------------

        [Test]
        public void PlayerDamage_ProducesOneAction()
        {
            var actions = Fire(
                "25:01:15:20:16:32.6::Ameise,P[100083146@5846877 Ameise@antday]," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Goblin,C[6261 Skeleton_Basic]," +
                "At-Will,Pn.Jy04um1,Physical,,500.0,600.0");

            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual("Ameise", actions[0].Attacker);
            // NPC names include their unique ID, e.g. "Goblin [6261]"
            StringAssert.StartsWith("Goblin", actions[0].Victim);
            Assert.AreEqual("Physical", actions[0].DamageType);
        }

        [Test]
        public void PlayerDamage_FlankFlag_IsFalse()
        {
            var actions = Fire(
                "25:01:15:20:16:32.6::Ameise,P[100083146@5846877 Ameise@antday]," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Goblin,C[6261 Skeleton_Basic]," +
                "At-Will,Pn.Jy04um1,Physical,,500.0,600.0");

            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(false, actions[0].Tags["Flank"]);
        }

        [Test]
        public void PlayerDamage_DamageFTagMatchesMagnitude()
        {
            var actions = Fire(
                "25:01:15:20:16:32.6::Ameise,P[100083146@5846877 Ameise@antday]," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Goblin,C[6261 Skeleton_Basic]," +
                "At-Will,Pn.Jy04um1,Physical,,500.0,600.0");

            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(500.0f, (float)actions[0].Tags["DamageF"], 0.01f);
        }

        // -----------------------------------------------------------------------
        // Critical hit
        // -----------------------------------------------------------------------

        [Test]
        public void CriticalDamage_CriticalFlagIsTrue()
        {
            var actions = Fire(
                "25:01:15:20:16:33.0::Ameise,P[100083146@5846877 Ameise@antday]," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Goblin,C[6261 Skeleton_Basic]," +
                "At-Will,Pn.Jy04um1,Physical,Critical,1250.0,600.0");

            Assert.AreEqual(1, actions.Count);
            Assert.IsTrue(actions[0].Critical);
        }

        // -----------------------------------------------------------------------
        // Flank
        // -----------------------------------------------------------------------

        [Test]
        public void FlankDamage_FlankTagIsTrue()
        {
            var actions = Fire(
                "25:01:15:20:16:34.0::Wolf,C[42358 Monster_Wolf],,*," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Bite,Pn.Lp6b6g1,Physical,Flank,43.4474,47.6017");

            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(true, actions[0].Tags["Flank"]);
        }

        // -----------------------------------------------------------------------
        // Malformed / short lines
        // -----------------------------------------------------------------------

        [Test]
        public void MalformedLine_ProducesNoActions()
        {
            var actions = Fire("INVALID LINE");
            Assert.AreEqual(0, actions.Count);
        }

        [Test]
        public void EmptyLine_ProducesNoActions()
        {
            var actions = Fire("");
            Assert.AreEqual(0, actions.Count);
        }

        // -----------------------------------------------------------------------
        // Environmental damage — must not start combat
        // -----------------------------------------------------------------------

        [Test]
        public void FallDamage_OutOfCombat_ProducesNoActions()
        {
            _form.InCombat = false;
            var actions = Fire(
                "25:01:15:20:16:40.0::Ameise,P[100083146@5846877 Ameise@antday],,*," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Fall,Autodesc.Combatevent.Falling,Physical,,42.0,42.0");

            Assert.AreEqual(0, actions.Count);
        }

        // -----------------------------------------------------------------------
        // Healing
        // -----------------------------------------------------------------------

        [Test]
        public void HealLine_ProducesAction_WithHealingDamageType()
        {
            var actions = Fire(
                "25:01:15:20:16:37.0::Ameise,P[100083146@5846877 Ameise@antday],,*," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Healing Word,Pn.Hx1234,HitPoints,,800.0,0");

            Assert.AreEqual(1, actions.Count);
            // The plugin passes l.type ("HitPoints") as DamageType; ACT routes it to
            // the Heals bucket based on SwingType = Healing (4).
            Assert.AreEqual("HitPoints", actions[0].DamageType);
            Assert.AreEqual((int)SwingTypeEnum.Healing, actions[0].SwingType);
        }

        // -----------------------------------------------------------------------
        // Non-damaging proc (ShowPowerDisplayName)
        // -----------------------------------------------------------------------

        [Test]
        public void NonDamagingProc_ProducesAction_WithNonDamageType()
        {
            var actions = Fire(
                "25:01:15:20:16:38.0::Ameise,P[100083146@5846877 Ameise@antday],,*," +
                "Goblin,C[6261 Skeleton_Basic]," +
                "Mark,Pn.Mk5678,Physical,ShowPowerDisplayName,0,0");

            Assert.AreEqual(1, actions.Count);
            // The plugin passes l.type ("Physical") as DamageType; ACT routes it to the
            // Non-Damage bucket based on SwingType = NonMelee (2).
            Assert.AreEqual("Physical", actions[0].DamageType);
            Assert.AreEqual((int)SwingTypeEnum.NonMelee, actions[0].SwingType);
        }

        // -----------------------------------------------------------------------
        // Kill blow
        // -----------------------------------------------------------------------

        [Test]
        public void KillBlow_ProducesTwoActions_DamageAndKilling()
        {
            var actions = Fire(
                "25:01:15:20:16:39.0::Ameise,P[100083146@5846877 Ameise@antday]," +
                "Ameise,P[100083146@5846877 Ameise@antday]," +
                "Goblin,C[6261 Skeleton_Basic]," +
                "At-Will,Pn.Jy04um1,Physical,Kill,300.0,600.0");

            // Expect one damage action and one "Killing" action
            Assert.AreEqual(2, actions.Count);

            bool hasKilling = false;
            foreach (MasterSwing ms in actions)
            {
                if (ms.AttackType == "Killing")
                    hasKilling = true;
            }
            Assert.IsTrue(hasKilling, "Expected a Killing action");
        }

        // -----------------------------------------------------------------------
        // Companion entity powers (Blue Fire Eye / Tutor)
        // These use powers listed in companionEntityPowers and should be attributed
        // to the entity's DamageType view, not merged into the player.
        // -----------------------------------------------------------------------

        [Test]
        public void CompanionEntityPower_AttackerIsEntity_NotOwner()
        {
            // Pn.Prookc1 = "Hexed earth" from Blue Fire Eye — a companionEntityPower
            var actions = Fire(
                "25:01:15:20:16:36.0::Ameise,P[100083146@5846877 Ameise@antday]," +
                "HexedEarth,C[12345 Entity_Hexedearth]," +
                "Goblin,C[6261 Skeleton_Basic]," +
                "Hexed Earth,Pn.Prookc1,Physical,,200.0,250.0");

            Assert.AreEqual(1, actions.Count);
            // Without merging, the attacker should be the entity itself, not the player owner
            Assert.AreNotEqual("Ameise", actions[0].Attacker);
        }
    }
}
