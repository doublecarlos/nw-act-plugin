// ActMocks.cs
// Stub implementations of all Advanced_Combat_Tracker types used by Neverwinter.cs.
// These live in the same namespace so the plugin compiles without referencing the real ACT assembly.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace Advanced_Combat_Tracker
{
    // ---------------------------------------------------------------------------
    // Delegates
    // ---------------------------------------------------------------------------

    public delegate void LogLineEventDelegate(bool isImport, LogLineEventArgs logInfo);
    public delegate void CombatToggleEventDelegate(bool isImport, CombatToggleEventArgs encounterInfo);
    public delegate void LogFileChangedDelegate(bool IsImport, string NewLogFileName);

    // ---------------------------------------------------------------------------
    // IActPluginV1
    // ---------------------------------------------------------------------------

    public interface IActPluginV1
    {
        void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText);
        void DeInitPlugin();
    }

    // ---------------------------------------------------------------------------
    // SwingTypeEnum
    // ---------------------------------------------------------------------------

    public enum SwingTypeEnum
    {
        Melee = 1,
        NonMelee = 2,
        CureDispel = 3,
        Healing = 4,
        PowerDrain = 5,
        PowerHeal = 6,
        Miss = 7,
        Buff = 8,
        PowerHealing = 13,
    }

    // ---------------------------------------------------------------------------
    // Dnum  (damage number wrapper)
    // ---------------------------------------------------------------------------

    // Dnum is a class (reference type) so it can be assigned null in the plugin code.
    public class Dnum
    {
        private long _value;

        public static readonly Dnum Death = new Dnum(long.MinValue);
        public static readonly Dnum NoDamage = new Dnum(0);

        public Dnum(long value) { _value = value; }
        public Dnum(int value) { _value = value; }

        public static implicit operator Dnum(int value) { return new Dnum(value); }
        public static explicit operator int(Dnum d) { return (int)d._value; }
        public static explicit operator long(Dnum d) { return d._value; }

        public static bool operator >(Dnum d, int v) { return d._value > v; }
        public static bool operator <(Dnum d, int v) { return d._value < v; }
        public static bool operator >=(Dnum d, int v) { return d._value >= v; }
        public static bool operator <=(Dnum d, int v) { return d._value <= v; }

        public int CompareTo(Dnum other) { return _value.CompareTo(other._value); }
        public override string ToString() { return _value.ToString(); }
    }

    // ---------------------------------------------------------------------------
    // LogLineEventArgs
    // ---------------------------------------------------------------------------

    public class LogLineEventArgs
    {
        public string logLine;
        public int detectedType;
        public DateTime detectedTime;

        public LogLineEventArgs(string line, DateTime time)
        {
            logLine = line;
            detectedTime = time;
            detectedType = 0;
        }
    }

    // ---------------------------------------------------------------------------
    // CombatToggleEventArgs
    // ---------------------------------------------------------------------------

    public class CombatToggleEventArgs { }

    // ---------------------------------------------------------------------------
    // MasterSwing
    // ---------------------------------------------------------------------------

    public class MasterSwing
    {
        public int SwingType;
        public bool Critical;
        public string Special;
        public Dnum Damage;
        public DateTime Time;
        public int TimeSorter;
        public string AttackType;
        public string Attacker;
        public string DamageType;
        public string Victim;
        public EncounterData ParentEncounter;
        public Dictionary<string, object> Tags;

        public MasterSwing(
            int swingType, bool critical, string special, Dnum damage,
            DateTime time, int timeSorter,
            string attackType, string attacker, string damageType, string victim)
        {
            SwingType = swingType;
            Critical = critical;
            Special = special;
            Damage = damage;
            Time = time;
            TimeSorter = timeSorter;
            AttackType = attackType;
            Attacker = attacker;
            DamageType = damageType;
            Victim = victim;
            ParentEncounter = new EncounterData();
            Tags = new Dictionary<string, object>();
        }

        // Static column definitions — populated by FixupMasterSwingData(), ignored in tests.
        public static Dictionary<string, ColumnDef> ColumnDefs = new Dictionary<string, ColumnDef>();

        public class ColumnDef
        {
            public ColumnDef(
                string id, bool defaultVisible, string sqlType, string sqlName,
                Func<MasterSwing, string> getCellData,
                Func<MasterSwing, string> getSqlData,
                Func<MasterSwing, MasterSwing, int> compare)
            { }
        }
    }

    // ---------------------------------------------------------------------------
    // EncounterData
    // ---------------------------------------------------------------------------

    public class EncounterData
    {
        public string EncId = "";
        public string Title = "";
        public DateTime StartTime = DateTime.MaxValue;
        public DateTime EndTime = DateTime.MinValue;
        public TimeSpan Duration = TimeSpan.Zero;
        public string DurationS = "";
        public long Damage;
        public double DPS;
        public int AlliedKills;
        public int AlliedDeaths;

        public List<CombatantData> GetAllies() { return new List<CombatantData>(); }
        public string GetMaxHeal(bool showCrits, bool includeWards) { return ""; }
        public string GetMaxHit(bool showCrits) { return ""; }

        public static Dictionary<string, ColumnDef> ColumnDefs = new Dictionary<string, ColumnDef>();
        public static Dictionary<string, TextExportFormatter> ExportVariables = new Dictionary<string, TextExportFormatter>();

        public class ColumnDef
        {
            public ColumnDef(
                string id, bool defaultVisible, string sqlType, string sqlName,
                Func<EncounterData, string> getCellData,
                Func<EncounterData, string> getSqlData)
            { }
        }

        public class TextExportFormatter
        {
            public TextExportFormatter(
                string label, string displayLabel, string displayDesc,
                Func<EncounterData, List<CombatantData>, string, string> formatter)
            { }
        }
    }

    // ---------------------------------------------------------------------------
    // CombatantData
    // ---------------------------------------------------------------------------

    public class CombatantData
    {
        public string Name = "";
        public DateTime StartTime = DateTime.MaxValue;
        public DateTime EndTime = DateTime.MinValue;
        public TimeSpan Duration = TimeSpan.Zero;
        public string DurationS = "";
        public long Damage;
        public string DamagePercent = "";
        public long Healed;
        public string HealedPercent = "";
        public int Swings, Hits, CritHits, Heals, CritHeals, CureDispels, Misses, Kills, Deaths;
        public long PowerReplenish;
        public double DPS, EncDPS, EncHPS;
        public long HealsTaken;
        public long DamageTaken;
        public double CritDamPerc;
        public double CritHealPerc;
        public int Blocked;
        public float ToHit;
        public long PowerDamage;
        public Dictionary<string, AttackType> AllOut = new Dictionary<string, AttackType>();
        public EncounterData Parent = new EncounterData();

        public string GetMaxHit(bool showCrits) { return ""; }
        public string GetMaxHeal(bool showCrits, bool includeWards) { return ""; }
        public string GetThreatStr(string damageTypeName) { return ""; }
        public double GetThreatDelta(string damageTypeName) { return 0; }

        // Damage type breakdown keyed by the damage type name (e.g. "Damage (Out)")
        public Dictionary<string, DamageTypeData> Items = new Dictionary<string, DamageTypeData>();

        // Static column definitions
        public static Dictionary<string, ColumnDef> ColumnDefs = new Dictionary<string, ColumnDef>();

        // Static export variables
        public static Dictionary<string, TextExportFormatter> ExportVariables = new Dictionary<string, TextExportFormatter>();

        // Swing-type → damage type name mappings used by ACT to route actions
        public static SortedDictionary<int, List<string>> SwingTypeToDamageTypeDataLinksOutgoing = new SortedDictionary<int, List<string>>();
        public static SortedDictionary<int, List<string>> SwingTypeToDamageTypeDataLinksIncoming = new SortedDictionary<int, List<string>>();
        public static List<int> DamageSwingTypes = new List<int>();
        public static List<int> HealingSwingTypes = new List<int>();

        // Damage type display metadata
        public static Dictionary<string, DamageTypeDef> OutgoingDamageTypeDataObjects = new Dictionary<string, DamageTypeDef>();
        public static Dictionary<string, DamageTypeDef> IncomingDamageTypeDataObjects = new Dictionary<string, DamageTypeDef>();

        // Named damage-type bucket constants
        public static string DamageTypeDataNonSkillDamage = "";
        public static string DamageTypeDataOutgoingDamage = "";
        public static string DamageTypeDataOutgoingHealing = "";
        public static string DamageTypeDataIncomingDamage = "";
        public static string DamageTypeDataIncomingHealing = "";

        public class ColumnDef
        {
            public ColumnDef(
                string id, bool defaultVisible, string sqlType, string sqlName,
                Func<CombatantData, string> getCellData,
                Func<CombatantData, string> getSqlData,
                Func<CombatantData, CombatantData, int> compare)
            { }
        }

        public class TextExportFormatter
        {
            public TextExportFormatter(
                string label, string displayLabel, string displayDesc,
                Func<CombatantData, string, string> formatter)
            { }
        }

        public class DamageTypeDef
        {
            public DamageTypeDef(string name, int modifier, System.Drawing.Color color) { }
        }
    }

    // ---------------------------------------------------------------------------
    // DamageTypeData
    // ---------------------------------------------------------------------------

    public class DamageTypeData
    {
        public string Type = "";
        public CombatantData Parent = new CombatantData();
        public DateTime StartTime = DateTime.MaxValue;
        public DateTime EndTime = DateTime.MinValue;
        public TimeSpan Duration = TimeSpan.Zero;
        public string DurationS = "";
        public bool Outgoing;
        public long Damage;
        public double DPS, EncDPS, CharDPS;
        public double Average;
        public int Median, MinHit, MaxHit, CritHits, Swings, Hits;
        public double AverageDelay, CritPerc;
        public Dictionary<string, AttackType> Items = new Dictionary<string, AttackType>();

        public static Dictionary<string, ColumnDef> ColumnDefs = new Dictionary<string, ColumnDef>();

        public class ColumnDef
        {
            public ColumnDef(
                string id, bool defaultVisible, string sqlType, string sqlName,
                Func<DamageTypeData, string> getCellData,
                Func<DamageTypeData, string> getSqlData)
            { }
        }
    }

    // ---------------------------------------------------------------------------
    // AttackType
    // ---------------------------------------------------------------------------

    public class AttackType
    {
        public string Type = "";
        public bool Outgoing;
        public DamageTypeData Parent = new DamageTypeData();
        public DateTime StartTime = DateTime.MaxValue;
        public DateTime EndTime = DateTime.MinValue;
        public TimeSpan Duration = TimeSpan.Zero;
        public string DurationS = "";
        public long Damage;
        public double DPS, EncDPS, CharDPS;
        public double Average;
        public int Median, MinHit, MaxHit, CritHits, Swings, Hits;
        public double AverageDelay, CritPerc;
        public string Resist = "";
        public Dictionary<string, object> Tags = new Dictionary<string, object>();
        public List<MasterSwing> Items = new List<MasterSwing>();

        public static Dictionary<string, ColumnDef> ColumnDefs = new Dictionary<string, ColumnDef>();

        public class ColumnDef
        {
            public ColumnDef(
                string id, bool defaultVisible, string sqlType, string sqlName,
                Func<AttackType, string> getCellData,
                Func<AttackType, string> getSqlData,
                Func<AttackType, AttackType, int> compare)
            { }
        }
    }

    // ---------------------------------------------------------------------------
    // ActLocalization — returns a dummy string for any key so FixupEncounterData
    // doesn't throw when accessing LocalizationStrings["..."].DisplayedText
    // ---------------------------------------------------------------------------

    public class ActLocalization
    {
        public LocalizationStringDict LocalizationStrings = new LocalizationStringDict();

        public class LocalizationString
        {
            public string DisplayedText = "";
        }

        public class LocalizationStringDict
        {
            private Dictionary<string, LocalizationString> _inner = new Dictionary<string, LocalizationString>();

            public LocalizationString this[string key]
            {
                get
                {
                    LocalizationString val;
                    if (!_inner.TryGetValue(key, out val))
                    {
                        val = new LocalizationString();
                        _inner[key] = val;
                    }
                    return val;
                }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // FormSpellTimers
    // ---------------------------------------------------------------------------

    public class FormSpellTimers
    {
        public virtual void RemoveTimerMods(string name) { }
        public virtual void DispellTimerMods(string name) { }
    }

    // ---------------------------------------------------------------------------
    // FormActMain
    // ---------------------------------------------------------------------------

    public class FormActMain
    {
        public delegate DateTime DateTimeLogParser(string logLine);

        // Events the plugin hooks into
        public event LogLineEventDelegate BeforeLogLineRead;
        public event CombatToggleEventDelegate OnCombatEnd;
        public event LogFileChangedDelegate LogFileChanged;

        // Properties set by the plugin during InitPlugin
        public TreeView OptionsTreeView = new TreeView();
        public Dictionary<string, List<Control>> OptionsControlSets = new Dictionary<string, List<Control>>();
        public bool LogPathHasCharName;
        public string LogFileFilter;
        public string LogFileParentFolderName;
        public int TimeStampLen;
        public DateTimeLogParser GetDateTimeFromLog;

        // State read during parsing
        public bool InCombat = true;
        public int GlobalTimeSorter;
        public DateTime LastKnownTime = DateTime.Now;
        public DirectoryInfo AppDataFolder = new DirectoryInfo(Path.GetTempPath());

        // Collects every MasterSwing the plugin submits — the main test assertion target
        public List<MasterSwing> CapturedActions = new List<MasterSwing>();

        public virtual void ResetCheckLogs() { }
        public virtual bool SetEncounter(DateTime time, string attacker, string target) { return true; }
        public virtual void AddCombatAction(MasterSwing ms) { CapturedActions.Add(ms); }
        public virtual void ValidateLists() { }
        public virtual void ValidateTableSetup() { }
        public virtual void SetOptionsHelpText(string text) { }

        // Helper for tests: raise BeforeLogLineRead with a pre-built LogLineEventArgs
        public void FireBeforeLogLineRead(bool isImport, LogLineEventArgs args)
        {
            if (BeforeLogLineRead != null)
                BeforeLogLineRead(isImport, args);
        }

        // Convenience: build a LogLineEventArgs from a raw log line string and fire it
        public LogLineEventArgs FireLogLine(string logLine, bool isImport = false)
        {
            DateTime time;
            if (GetDateTimeFromLog != null)
                time = GetDateTimeFromLog(logLine);
            else
                time = DateTime.Now;

            var args = new LogLineEventArgs(logLine, time);
            FireBeforeLogLineRead(isImport, args);
            return args;
        }
    }

    // ---------------------------------------------------------------------------
    // SettingsSerializer
    // ---------------------------------------------------------------------------

    public class SettingsSerializer
    {
        public SettingsSerializer(UserControl control) { }
        public void AddControlSetting(string name, Control control) { }
        public void ImportFromXml(XmlTextReader reader) { }
        public void ExportToXml(XmlTextWriter writer) { }
    }

    // ---------------------------------------------------------------------------
    // ActGlobals — the central static context the plugin reads/writes
    // ---------------------------------------------------------------------------

    public static class ActGlobals
    {
        public static FormActMain oFormActMain;
        public static FormSpellTimers oFormSpellTimers = new FormSpellTimers();
        public static string charName = "";
        public static bool mainTableShowCommas = false;
        public static ActLocalization ActLocalization = new ActLocalization();
    }
}
