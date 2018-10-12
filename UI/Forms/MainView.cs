using System.Configuration;
using TESVSnip.Domain.Scripts;

namespace TESVSnip.UI.Forms
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Windows.Forms;
    using System.Xml.Serialization;
    using System.Xml.Linq;

    using RTF;

    using TESVSnip.Domain.Data.RecordStructure;
    using TESVSnip.Domain.Model;
    using TESVSnip.Domain.Services;
    using TESVSnip.Framework;
    using TESVSnip.Framework.Services;
    using TESVSnip.Properties;
    using TESVSnip.UI.Docking;
    using TESVSnip.UI.ObjectControls;

    using WeifenLuo.WinFormsUI.Docking;
    using JWC;

    using Settings = TESVSnip.Properties.Settings;
    using Timer = System.Threading.Timer;
    using System.Text;

    public struct ConversationOwners
    {
        public string sSkyrimFormID { get; set; }
        public string sDAOResRefName { get; set; }
    }

    public struct DialogConnector //only for NPC lines
    {
        public int LineIndex { get; set; }
        public Dictionary<uint, string> LinkToFormID { get; set; }
        public List<FConvNode> LineNodes { get; set; }
        public string LineComment { get; set; }//for DEBUG
        public string ConversationOwner { get; set; }//only one, the last if multiple
        public int BranchOwner { get; set; }
        public DialGroupInfo LineDialog { get; set; }
        public bool IsTainted { get; set; }
        public FConvNode NodeSwitch { get; set; }//ugly TODO
    }

    public enum ENestingType
    {
        NoNesting,
        NewNesting,
        CurrentNestedInPrevious,
        PreviousNestedInCurrent,
        CurrentNestedInPreviousParentElement,
        ResetCurrent
    }

    public enum EDialogTopicSubtype
    {
        ACAC,
        ACYI,
        AGRE,
        ALIL,
        ALTC,
        ALTN,
        ASNC,
        ASSA,
        ATCK,
        BASH,
        BLED,
        BLOC,
        BREA,
        BRIB,
        COLO,
        COTN,
        CUST,
        DEOB,
        DETH,
        DFDA,
        ENBZ,
        EXBZ,
        FEXT,
        FIWE,
        FLEE,
        GBYE,
        GRNT,
        HELO,
        HIT_,
        IDAT,
        IDLE,
        INTI,
        KNOO,
        LOIL,
        LOOB,
        LOTC,
        LOTN,
        LWBS,
        MREF,
        MUNC,
        MURD,
        NOTA,
        NOTC,
        NOTI,
        OBCO,
        OUTB,
        PCSH,
        PFGT,
        PICC,
        PICN,
        PICT,
        PIRN,
        POAT,
        PURS,
        REFU,
        RUMO,
        SCEN,
        SHOW,
        STEA,
        STFN,
        STOF,
        SWMW,
        TAUT,
        TITG,
        TRAN,
        TRES,
        VPEL,
        VPES,
        VPSL,
        VPSS,
        WTCR,
        ZKEY
    }

    public enum EDialogTopicInfoType
    {
        Invalid,
        MergePrevious,//If last one and stuff?
        BlankLine,//Blank|No Audio|Has Cond
        BlankLineAction,//Blank|No Audio|Has Cond
        BlankLineBoth,//Blank|No Audio|Has Cond
        BlankLineCondition,//Blank|No Audio|Has Cond
        SimpleLinkTo,//Has TCLT LinkTo
        GoodbyeAction,//Goodbye|Has Cond GetIsID|Has VMAD|SetStage Local
        GoodbyeActionExternal,
        GoodbyeBoth,//Goodbye|Has Cond GetIsID|Has VMAD|SetStage Local
        GoodbyeBothExternal,
        GoodbyeCondition,//Goodbye|Has Cond GetIsID|Has VMAD|SetStage Local
        GoodbyeSimple,//No Cond|No VMAD
        ActionExternal,//Has VMAD|2 Properties
        ActionSimple,//No Cond|Has VMAD|SetStage Local
        ConditionSimple,//SimpleGetStageDone//Has Cond GetStageDone Local|No VMAD
        SimpleGetIsID,//Has Cond GetIsID|No VMAD
        Both,//A/C
        BothExternal//AE/C
    }

    internal partial class MainView : Form
    {
        #region ints

        private const int GROUP_INVALID = 0;
        private const int GROUP_PC = 1;
        private const int GROUP_HOSTILE = 2;
        private const int GROUP_FRIENDLY = 3;
        private const int GROUP_NEUTRAL = 4;
        private const int GROUP_HOSTILE_ON_GROUND = 5;

        private const int RACE_INVALID = 0;
        private const int RACE_DWARF = 1;
        private const int RACE_ELF = 2;
        private const int RACE_HUMAN = 3;
        private const int RACE_QUNARI = 4;
        private const int RACE_ANIMAL = 5;
        private const int RACE_BEAST = 6;
        private const int RACE_DARKSPAWN = 7;
        private const int RACE_DRAGON = 8;
        private const int RACE_GOLEM = 9;
        private const int RACE_SPIRIT = 10;
        private const int RACE_UNDEAD = 11;

        private const int CLASS_WARRIOR = 1;
        private const int CLASS_WIZARD = 2;
        private const int CLASS_ROGUE = 3;
        private const int CLASS_SHAPESHIFTER = 4;
        private const int CLASS_SPIRITHEALER = 5;
        private const int CLASS_CHAMPION = 6;
        private const int CLASS_TEMPLAR = 7;
        private const int CLASS_BERSERKER = 8;
        private const int CLASS_REAVER = 9;
        private const int CLASS_ARCANE_WARRIOR = 10;
        private const int CLASS_ASSASSIN = 11;
        private const int CLASS_BLOOD_MAGE = 12;
        private const int CLASS_BARD = 13;
        private const int CLASS_RANGER = 14;
        private const int CLASS_DUELIST = 15;
        private const int CLASS_SHALE = 16;
        private const int CLASS_DOG = 17;
        private const int CLASS_MONSTER_ANIMAL = 18;

        private const int APP_INVALID = 0;
        private const int APP_Blank_Model = 1;
        private const int APP_Elf = 2;
        private const int APP_Dwarf = 3;
        private const int APP_Dragon_Normal = 4;
        private const int APP_Golem_DEPRECATED = 5;
        private const int APP_Golem_Stone = 6;
        private const int APP_Golem_Steel = 7;
        private const int APP_Bear_Great = 8;
        private const int APP_Bear_Black = 9;
        private const int APP_Broodmother = 10;
        private const int APP_Bronto = 11;
        private const int APP_Arcane_Horror = 12;
        private const int APP_Qunari = 13;
        private const int APP_Wisp = 14;
        private const int APP_Human = 15;
        private const int APP_Hurlock_Normal = 16;
        private const int APP_Hurlock_Alpha = 17;
        private const int APP_Hurlock_Emissary = 18;
        private const int APP_Nug = 20;
        private const int APP_Werewolf_A = 22;
        private const int APP_Shriek_A = 23;
        private const int APP_Succubus = 24;
        private const int APP_Abomination = 25;
        private const int APP_Revenant_A = 26;
        private const int APP_Rage_Demon = 27;
        private const int APP_Corpse_D = 28;
        private const int APP_Corpse_E = 29;
        private const int APP_Corpse_S = 30;
        private const int APP_Shade = 31;
        private const int APP_Ash_Wraith = 32;
        private const int APP_Deepstalker = 33;
        private const int APP_Dog_Mabari_ = 34;
        private const int APP_Dog_Party_Member = 35;
        private const int APP_Squirel = 36;
        private const int APP_Rat_Large = 37;
        private const int APP_Human_Boy = 38;
        private const int APP_Human_Servant_Ambient = 39;
        private const int APP_Human_Library_Ambient = 40;
        private const int APP_Rat_Small = 41;
        private const int APP_Human_Guard_Ambient = 42;
        private const int APP_Human_Noble_Ambient = 43;
        private const int APP_Human_Male_Fat = 44;
        private const int APP_Human_Female_Fat = 45;
        private const int APP_NPC_Duncan = 46;
        private const int APP_Ogre_A = 47;
        private const int APP_Wolf = 49;
        private const int APP_Genlock_Normal = 50;
        private const int APP_Genlock_Alpha = 51;
        private const int APP_Genlock_Emissary = 52;
        private const int APP_Witherfang = 53;
        private const int APP_Ambient_Goat = 54;
        private const int APP_Ambient_Mutt = 55;
        private const int APP_Spider_Corrupted = 57;
        private const int APP_Spider_Giant = 58;
        private const int APP_Spider_Poisonous = 59;
        private const int APP_Human_Dying_Ambient = 60;
        private const int APP_Human_Prelude_Wizard = 61;
        private const int APP_Cat = 63;
        private const int APP_Dragonling = 64;
        private const int APP_Wild_Sylvan = 65;
        private const int APP_Dragon_High = 66;
        private const int APP_Human_Girl = 67;
        private const int APP_Bear_Beareskan = 68;
        private const int APP_Skeleton_A = 69;
        private const int APP_Skeleton_F = 70;
        private const int APP_Skeleton_S = 71;
        private const int APP_Pride_Demon = 72;
        private const int APP_Broodmother_Tentacle = 73;
        private const int APP_Wolf_Blight = 74;
        private const int APP_Lady_of_the_Forest = 75;
        private const int APP_Pig = 76;
        private const int APP_Deer = 77;
        private const int APP_Ox = 78;
        private const int APP_Ram = 79;
        private const int APP_Dragon_Drake = 80;
        private const int APP_Spirit_Apparatus_Head = 81;
        private const int APP_Archdemon_Wounded = 82;
        private const int APP_Raven = 84;
        private const int APP_Halla = 85;
        private const int APP_Chicken = 86;
        private const int APP_Owl = 87;
        private const int APP_Grand_Oak = 88;
        private const int APP_Archdemon = 89;

        #endregion

        private const int WM_SETREDRAW = 0x0b;

        private List<string> combs = new List<string>();

        //move records
        public string groupName = "";

        public List<Record> ArrRecordsToMove = new List<Record>();
        public List<string> ArrTemplates = new List<string>();

        public Dictionary<string, string> ArrDictNamesSource = new Dictionary<string, string>();
        public Dictionary<string, string> ArrDictTemplates = new Dictionary<string, string>();

        public uint CounterFormID = uint.MaxValue;
        public int CounterTalk = -1;

        private Record ACharacter = null;//switcheroo ACHR :D
        private byte[] ArrData = null;//loc/rot

        public bool BoolCELL = false;
        List<BaseRecord> Cells = new List<BaseRecord>();

        private bool BoolFToM = false;//female to male
        private bool BoolMToF = false;//male to female

        private bool BoolRemoveACHRFromWorld = false;//broken, don't use

        public bool BoolExport = false;
        public bool BoolMove = true;
        public bool BoolDialog = true;

        private bool BoolTCLT = false;
        private bool BoolDelete = false;
        private bool BoolGetHELO = false;//for multi-response check
        private bool BoolUpdateVMAD = false;
        private bool BoolUpdateQUST = false;
        private bool BoolUpdateDLBR = false;
        private bool BoolUpdateDIAL = false;
        private bool BoolUpdateNPC_ = true;
        private bool BoolUpdateACHR = false;
        private bool BoolUpdateCELL = false;//copy Group CELL not Records
        private bool BoolVoiceType = false;
        private bool BoolCopyDialog = false;//!!! copies the wav files to the folder for FaceFX process
        private bool BoolUpdateXML = false;//update CELL XML 

        public string TextToFind = "";
        public GroupRecord GroupInfo = null;
        public SubRecord GenericVMAD = null;

        private static readonly Regex linkRegex =
            new Regex(
                "^(?:(?<text>[^#]*)#)?(?:(?<plugin>[^\\/:*?\"<>|@]*)@)?(?<type>[0-z][A-Z][A-Z][A-Z_]):(?<id>[0-9a-zA-Z]+)$",
                RegexOptions.None);

        private static object s_clipboard;

        internal Dictionary<string, ToolStripMenuItem> languageToolBarItems =
            new Dictionary<string, ToolStripMenuItem>(StringComparer.InvariantCultureIgnoreCase);

        private readonly SelectionContext Selection;

        private readonly PluginTreeContent pluginTreeContent = new PluginTreeContent();

        private readonly RichTextContent selectedTextContent = new RichTextContent();

        private readonly SubrecordListContent subrecordListContent = new SubrecordListContent();

        private volatile bool backgroundWorkCanceled;

        private bool inRebuildSelection;

        private DeserializeDockContent mDeserializeDockContent;

        private Timer statusTimer;

        private StringsEditor stringEditor;

        private MruStripMenu mruMenu;
        private static string mruRegKey = "SOFTWARE\\TESVSnip (Skyrim Edition)\\MRU";

        public MainView()
        {
            if (!RecordStructure.Loaded)
            {
                RecordStructure.Load();
            }

            this.InitializeComponent();
            this.InitializeToolStripFind();
            this.InitializeDockingWindows();
            this.RegisterMessageFilter();

            this.PluginTree.SelectionChanged += (o, e) => this.RebuildSelection();

            if (string.IsNullOrEmpty(Settings.Default.DefaultSaveFolder) ||
                !Directory.Exists(Settings.Default.DefaultSaveFolder))
            {
                this.SaveModDialog.InitialDirectory = Options.Value.GameDataDirectory;
            }
            else
            {
                this.SaveModDialog.InitialDirectory = Settings.Default.DefaultSaveFolder;
            }

            if (string.IsNullOrEmpty(Settings.Default.DefaultOpenFolder) ||
                !Directory.Exists(Settings.Default.DefaultOpenFolder))
            {
                this.OpenModDialog.InitialDirectory = Options.Value.GameDataDirectory;
            }
            else
            {
                this.OpenModDialog.InitialDirectory = Settings.Default.DefaultOpenFolder;
            }

            Icon = Resources.tesv_ico;
            try
            {
                if (!Settings.Default.IsFirstTimeOpening)
                {
                    Domain.Services.Settings.GetWindowPosition("TESsnip", this);
                }
                else
                {
                    Domain.Services.Settings.SetWindowPosition("TESsnip", this);
                    Settings.Default.IsFirstTimeOpening = false;
                    Settings.Default.Save();
                }
            }
            catch
            {
            }

            this.useWindowsClipboardToolStripMenuItem.Checked = Settings.Default.UseWindowsClipboard;
            this.noWindowsSoundsToolStripMenuItem.Checked = Settings.Default.NoWindowsSounds;
            this.disableHyperlinksToolStripMenuItem.Checked = Settings.Default.DisableHyperlinks;
            this.SelectedText.DetectUrls = !Settings.Default.DisableHyperlinks;
            this.saveStringsFilesToolStripMenuItem.Checked = Settings.Default.SaveStringsFiles;

            this.useNewSubrecordEditorToolStripMenuItem.Checked = !Settings.Default.UseOldSubRecordEditor;
            this.hexModeToolStripMenuItem.Checked = Settings.Default.UseHexSubRecordEditor;
            this.uTF8ModeToolStripMenuItem.Checked = Settings.Default.UseUTF8;

            this.Selection = new SelectionContext();
            this.Selection.formIDLookup = this.LookupFormIDI;
            this.Selection.strLookup = this.LookupFormStrings;
            this.Selection.formIDLookupR = this.GetRecordByID;

            this.SubrecordList.SetContext(this.Selection);
            this.InitializeLanguage();

            ClipboardChanged += (o, e) => this.RebuildSelection();
            this.Selection.RecordChanged += (o, a) => this.RebuildSelection();
            this.Selection.SubRecordChanged += (o, a) => this.RebuildSelection();

            this.PluginTree.OnSelectionUpdated += this.PluginTree_OnSelectionUpdated;

            this.PluginTree.SelectionChanged += this.PluginTree_SelectionChanged;
            this.SubrecordList.SelectionChanged += this.subrecordPanel_SelectionChanged;
            this.SubrecordList.OnSubrecordChanged += this.subrecordPanel_OnSubrecordChanged;
            this.SubrecordList.DataChanged += this.subrecordPanel_DataChanged;

            this.LocalizeApp();
            PyInterpreter.InitPyInterpreter();

            mruMenu = new MruStripMenu(recentFilelToolStripMenuItem, new MruStripMenu.ClickedHandler(OnMruFile),
                                       mruRegKey + "\\MRU", true, 16);
        }

        public static event EventHandler ClipboardChanged;

        public static object Clipboard
        {
            get { return GetClipboardData(); }

            set
            {
                SetClipboardData(value);
                if (ClipboardChanged != null)
                {
                    ClipboardChanged(null, EventArgs.Empty);
                }
            }
        }

        public int CopyRecordsTo(object[] nodes)
        {
            var src = nodes[0] as BaseRecord[];
            var dst = nodes[1] as IGroupRecord;
            return Spells.CopyRecordsTo(src, dst);
        }

        public RichTextBox SelectedText
        {
            get { return this.selectedTextContent.RtfInfo; }
        }

        private PluginTreeView PluginTree
        {
            get { return this.pluginTreeContent.PluginTree; }
        }

        private SubrecordListEditor SubrecordList
        {
            get { return this.subrecordListContent.SubrecordList; }
        }

        public static void PostStatusText(string text)
        {
            PostStatusText(text, SystemColors.ControlText);
        }

        public static void PostStatusText(string text, Color color)
        {
            var form = Application.OpenForms.OfType<MainView>().FirstOrDefault();
            if (form != null)
            {
                form.SendStatusText(text, color);
            }
        }

        public static void PostStatusWarning(string text)
        {
            PostStatusText(text, Color.OrangeRed);
        }

        public void CancelBackgroundProcess()
        {
            this.backgroundWorkCanceled = true;
            this.backgroundWorker1.CancelAsync();
        }

        public RecordSearchForm CreateSearchWindow()
        {
            int id = Application.OpenForms.OfType<RecordSearchForm>().Count() + 1;
            var form = new RecordSearchForm();
            form.Text = string.Format("Search {0}", id);

            var searchform = Application.OpenForms.OfType<RecordSearchForm>().LastOrDefault(x => x.Visible);
            if (searchform != null)
            {
                if (searchform.Pane != null)
                {
                    // second item in list
                    form.Show(searchform.Pane, null);
                }
                else if (searchform.PanelPane != null)
                {
                    form.Show(searchform.PanelPane, null);
                }
            }
            else
            {
                if (this.dockPanel.ActiveDocumentPane != null)
                {
                    form.Show(this.dockPanel.ActiveDocumentPane, DockAlignment.Bottom, 0.33);
                }
                else
                {
                    form.Show(this.dockPanel, DockState.Document);
                }
            }

            return form;
        }

        public bool IsBackroundProcessCanceled()
        {
            return this.backgroundWorkCanceled;
        }

        /// <summary>
        /// Send text to status and then clear 5 seconds later.
        /// </summary>
        /// <param name="text">
        /// </param>
        public void SendStatusText(string text)
        {
            this.SendStatusText(text, SystemColors.ControlText);
        }

        public void SendStatusText(string text, Color color)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string, Color>(this.SendStatusText), new object[] { text, color });
                }
                else
                {
                    this.toolStripStatusLabel.ForeColor = color;
                    this.toolStripStatusLabel.Text = text;
                    if (this.statusTimer == null)
                    {
                        this.statusTimer = new Timer(
                            o =>
                            Invoke(new TimerCallback(o2 => { this.toolStripStatusLabel.Text = string.Empty; }),
                                   new object[] { string.Empty }), string.Empty, TimeSpan.FromSeconds(15),
                            TimeSpan.FromMilliseconds(-1));
                    }
                    else
                    {
                        this.statusTimer.Change(TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(-1));
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public void StartBackgroundWork(Action workAction, Action completedAction)
        {
            if (this.backgroundWorker1.IsBusy)
            {
                return;
            }

            this.EnableUserInterface(false);
            this.backgroundWorkCanceled = false;
            this.toolStripStatusProgressBar.ProgressBar.Value = this.toolStripStatusProgressBar.Minimum;
            this.toolStripStatusProgressBar.Visible = true;
            this.toolStripStopProgress.Visible = true;
            this.backgroundWorker1.RunWorkerAsync(new[] { workAction, completedAction });
        }

        public void UpdateBackgroundProgress(int percentProgress)
        {
            this.backgroundWorker1.ReportProgress(percentProgress);
        }

        internal static object GetClipboardData()
        {
            if (Settings.Default.UseWindowsClipboard)
            {
                var od = System.Windows.Forms.Clipboard.GetDataObject();
                if (od != null)
                {
                    var cliptype = od.GetData("TESVSnip");
                    if (cliptype is string)
                    {
                        return od.GetData(cliptype.ToString());
                    }
                }

                return null;
            }

            return s_clipboard;
        }

        internal static T GetClipboardData<T>() where T : class
        {
            if (Settings.Default.UseWindowsClipboard)
            {
                var od = System.Windows.Forms.Clipboard.GetDataObject();
                if (od != null)
                {
                    var clip = od.GetData(typeof(T).FullName);
                    return clip as T;
                }

                return default(T);
            }

            return s_clipboard as T;
        }

        internal static bool HasClipboardData()
        {
            if (Settings.Default.UseWindowsClipboard)
            {
                var od = System.Windows.Forms.Clipboard.GetDataObject();
                return od != null && od.GetDataPresent("TESVSnip");
            }

            return Clipboard != null;
        }

        internal static bool HasClipboardData<T>()
        {
            if (Settings.Default.UseWindowsClipboard)
            {
                var od = System.Windows.Forms.Clipboard.GetDataObject();
                return od != null && od.GetDataPresent(typeof(T).FullName);
            }

            return Clipboard is T;
        }

        internal static void PostReferenceSearch(uint formid)
        {
            var form = Application.OpenForms.OfType<MainView>().FirstOrDefault();
            if (form != null)
            {
                form.ReferenceSearch(formid);
            }
        }

        internal static void SetClipboardData(object value)
        {
            if (Settings.Default.UseWindowsClipboard)
            {
                var cloneable = value as ICloneable;
                if (cloneable != null)
                {
                    var ido = new DataObject();
                    var srFormat = value.GetType().FullName;
                    ido.SetData(srFormat, cloneable.Clone());
                    ido.SetData("TESVSnip", srFormat);
                    System.Windows.Forms.Clipboard.Clear();
                    System.Windows.Forms.Clipboard.SetDataObject(ido, true);
                }
            }
            else
            {
                s_clipboard = value;
            }
        }

        internal static void SynchronizeSelection(IEnumerable<BaseRecord> selection)
        {
            var form = Application.OpenForms.OfType<MainView>().FirstOrDefault();
            if (form != null)
            {
                form.PluginTree.SetSelectedRecords(selection);
            }
        }

        public string GetGroup(int nGroup)
        {
            switch (nGroup)
            {
                case GROUP_INVALID: return "GROUP_INVALID";
                case GROUP_PC: return "GROUP_PC";
                case GROUP_HOSTILE: return "GROUP_HOSTILE";
                case GROUP_FRIENDLY: return "GROUP_FRIENDLY";
                case GROUP_NEUTRAL: return "GROUP_NEUTRAL";
                case GROUP_HOSTILE_ON_GROUND: return "GROUP_HOSTILE_ON_GROUND";
                default: return "GROUP_NEUTRAL_" + nGroup;
            }
        }

        public string GetRace(int nRace)
        {
            switch (nRace)
            {
                case RACE_INVALID: return "RACE_INVALID";
                case RACE_DWARF: return "RACE_DWARF";
                case RACE_ELF: return "RACE_ELF";
                case RACE_HUMAN: return "RACE_HUMAN";
                case RACE_QUNARI: return "RACE_QUNARI";
                case RACE_ANIMAL: return "RACE_ANIMAL";
                case RACE_BEAST: return "RACE_BEAST";
                case RACE_DARKSPAWN: return "RACE_DARKSPAWN";
                case RACE_DRAGON: return "RACE_DRAGON";
                case RACE_GOLEM: return "RACE_GOLEM";
                case RACE_SPIRIT: return "RACE_SPIRIT";
                case RACE_UNDEAD: return "RACE_UNDEAD";
                default: return "RACE_INVALID";
            }
        }

        public string GetClass(int nClass)
        {
            switch (nClass)
            {
                case CLASS_WARRIOR: return "CLASS_WARRIOR";
                case CLASS_WIZARD: return "CLASS_WIZARD";
                case CLASS_ROGUE: return "CLASS_ROGUE";
                case CLASS_SHAPESHIFTER: return "CLASS_SHAPESHIFTER";
                case CLASS_SPIRITHEALER: return "CLASS_SPIRITHEALER";
                case CLASS_CHAMPION: return "CLASS_CHAMPION";
                case CLASS_TEMPLAR: return "CLASS_TEMPLAR";
                case CLASS_BERSERKER: return "CLASS_BERSERKER";
                case CLASS_REAVER: return "CLASS_REAVER";
                case CLASS_ARCANE_WARRIOR: return "CLASS_ARCANE_WARRIOR";
                case CLASS_ASSASSIN: return "CLASS_ASSASSIN";
                case CLASS_BLOOD_MAGE: return "CLASS_BLOOD_MAGE";
                case CLASS_BARD: return "CLASS_BARD";
                case CLASS_RANGER: return "CLASS_RANGER";
                case CLASS_DUELIST: return "CLASS_DUELIST";
                case CLASS_SHALE: return "CLASS_SHALE";
                case CLASS_DOG: return "CLASS_DOG";
                case CLASS_MONSTER_ANIMAL: return "CLASS_MONSTER_ANIMAL";
                default: return "NOTFOUND";
            }
        }

        public string GetAppearance(int nApp)
        {
            switch (nApp)
            {
                case APP_INVALID: return "APP_INVALID";
                case APP_Blank_Model: return "APP_Blank_Model";
                case APP_Elf: return "APP_Elf";
                case APP_Dwarf: return "APP_Dwarf";
                case APP_Dragon_Normal: return "APP_Dragon_Normal";
                case APP_Golem_DEPRECATED: return "APP_Golem_DEPRECATED";
                case APP_Golem_Stone: return "APP_Golem_Stone";
                case APP_Golem_Steel: return "APP_Golem_Steel";
                case APP_Bear_Great: return "APP_Bear_Great";
                case APP_Bear_Black: return "APP_Bear_Black";
                case APP_Broodmother: return "APP_Broodmother";
                case APP_Bronto: return "APP_Bronto";
                case APP_Arcane_Horror: return "APP_ArcaneHorror";
                case APP_Qunari: return "APP_Qunari";
                case APP_Wisp: return "APP_Wisp";
                case APP_Human: return "APP_Human";
                case APP_Hurlock_Normal: return "APP_Hurlock_Normal";
                case APP_Hurlock_Alpha: return "APP_Hurlock_Alpha";
                case APP_Hurlock_Emissary: return "APP_Hurlock_Emissary";
                case APP_Nug: return "APP_Nug";
                case APP_Werewolf_A: return "APP_Werewolf_A";
                case APP_Shriek_A: return "APP_Shriek_A";
                case APP_Succubus: return "APP_Succubus";
                case APP_Abomination: return "APP_Abomination";
                case APP_Revenant_A: return "APP_Revenant_A";
                case APP_Rage_Demon: return "APP_Rage_Demon";
                case APP_Corpse_D: return "APP_Corpse_D";
                case APP_Corpse_E: return "APP_Corpse_E";
                case APP_Corpse_S: return "APP_Corpse_S";
                case APP_Shade: return "APP_Shade";
                case APP_Ash_Wraith: return "APP_AshWraith";
                case APP_Deepstalker: return "APP_Deepstalker";
                case APP_Dog_Mabari_: return "APP_Dog_Mabari";
                case APP_Dog_Party_Member: return "APP_Dog_Party_Member";
                case APP_Squirel: return "APP_Squirel";
                case APP_Rat_Large: return "APP_Rat_Large";
                case APP_Human_Boy: return "APP_HumanBoy";
                case APP_Human_Servant_Ambient: return "APP_Human_Servant_Ambient";
                case APP_Human_Library_Ambient: return "APP_Human_Library_Ambient";
                case APP_Rat_Small: return "APP_Rat_Small";
                case APP_Human_Guard_Ambient: return "APP_Human_Guard_Ambient";
                case APP_Human_Noble_Ambient: return "APP_Human_Noble_Ambient";
                case APP_Human_Male_Fat: return "APP_Human_Male_Fat";
                case APP_Human_Female_Fat: return "APP_Human_Female_Fat";
                case APP_NPC_Duncan: return "APP_Human_Duncan";
                case APP_Ogre_A: return "APP_Ogre_A";
                case APP_Wolf: return "APP_Wolf";
                case APP_Genlock_Normal: return "APP_Genlock_Normal";
                case APP_Genlock_Alpha: return "APP_Genlock_Alpha";
                case APP_Genlock_Emissary: return "APP_Genlock_Emissary";
                case APP_Witherfang: return "APP_Witherfang";
                case APP_Ambient_Goat: return "APP_Goat";
                case APP_Ambient_Mutt: return "APP_Mutt";
                case APP_Spider_Corrupted: return "APP_Spider_Corrupted";
                case APP_Spider_Giant: return "APP_Spider_Giant";
                case APP_Spider_Poisonous: return "APP_Spider_Poisonous";
                case APP_Human_Dying_Ambient: return "APP_Human_Dying_Ambient";
                case APP_Human_Prelude_Wizard: return "APP_Human_Prelude_Wizard";
                case APP_Cat: return "APP_Cat";
                case APP_Dragonling: return "APP_Dragonling";
                case APP_Wild_Sylvan: return "APP_WildSylvan";
                case APP_Dragon_High: return "APP_Dragon_High";
                case APP_Human_Girl: return "APP_HumanGirl";
                case APP_Bear_Beareskan: return "APP_Bear_Beareskan";
                case APP_Skeleton_A: return "APP_Skeleton_A";
                case APP_Skeleton_F: return "APP_Skeleton_F";
                case APP_Skeleton_S: return "APP_Skeleton_S";
                case APP_Pride_Demon: return "APP_PrideDemon";
                case APP_Broodmother_Tentacle: return "APP_Broodmother_Tentacle";
                case APP_Wolf_Blight: return "APP_Wolf_Blight";
                case APP_Lady_of_the_Forest: return "APP_LadyOfTheForest";
                case APP_Pig: return "APP_Pig";
                case APP_Deer: return "APP_Deer";
                case APP_Ox: return "APP_Ox";
                case APP_Ram: return "APP_Ram";
                case APP_Dragon_Drake: return "APP_Dragon_Drake";
                case APP_Spirit_Apparatus_Head: return "APP_Spirit_Apparatus_Head";
                case APP_Archdemon_Wounded: return "APP_Archdemon_Wounded";
                case APP_Raven: return "APP_Raven";
                case APP_Halla: return "APP_Halla";
                case APP_Chicken: return "APP_Chicken";
                case APP_Owl: return "APP_Owl";
                case APP_Grand_Oak: return "APP_GrandOak";
                case APP_Archdemon: return "APP_Archdemon";
                default: return "NOTFOUND";
            }
        }

        internal void LoadPlugin(string s)
        {
            bool bRenameSimple = false;
            if (bRenameSimple)
            {
                string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTC_2477Simple\",
                  "*.xml",
                  SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    string[] f1 = _file.Split('\\');
                    string f2 = f1[f1.Length - 1];

                    string[] f3 = f2.Split('_');
                    string f4 = "";
                    for (int i = 0; i < f3.Length - 2; i++)
                    {
                        f4 = f4 + f3[i] + "_";
                    }
                    f4 = f4.TrimEnd('_');
                    f4 = f4 + ".xml";

                    string f5 = "";
                    for (int i = 0; i < f1.Length - 1; i++)
                    {
                        f5 = f5 + f1[i] + "\\";
                    }
                    f5 = f5 + f4;

                    System.IO.File.Move(_file, f5);
                }
            }

            bool bRenameResID = false;
            if (bRenameResID)
            {
                string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\PLO_900ResID\",
                  "*.xml",
                  SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    string[] f1 = _file.Split('\\');
                    string f2 = f1[f1.Length - 1];

                    string[] f3 = f2.Split('_');
                    string f4 = f3[f3.Length - 1];
                    //f4 = f4 + ".xml";

                    string f5 = "";
                    for (int i = 0; i < f1.Length - 1; i++)
                    {
                        f5 = f5 + f1[i] + "\\";
                    }
                    f5 = f5 + f4;

                    System.IO.File.Move(_file, f5);
                }
            }

            bool bRenameDIAL = false;
            if (bRenameDIAL)
            {
                string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\Dialog\3_DIAL - Copy\",
                  "*.xml",
                  SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);
                    string sSubType = xdDoc.Root.Descendants("Subtype_Name").ToList()[0].Value;

                    string[] f1 = _file.Split('\\');
                    string f2 = f1[f1.Length - 1];

                    string sDir = "";
                    for (int i = 0; i < f1.Length - 1; i++)
                        sDir += f1[i] + "\\";

                    sDir += sSubType + "\\";

                    if (!Directory.Exists(sDir))
                        Directory.CreateDirectory(sDir);

                    System.IO.File.Move(_file, sDir + f2);
                }
            }

            bool bDialogOwner = false;
            if (bDialogOwner)
            {
                Dictionary<string, string> occDAO = new Dictionary<string, string>();
                Dictionary<string, string> occSKY = new Dictionary<string, string>();

                string[] filesUTC = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTCConv - Copy\",
                          "*.xml",
                          SearchOption.AllDirectories);

                string sFileExists = @"d:\Work\R_idle\DAO_All\DAO_extracted\XML\DLG_2159ResID\";

                foreach (string _file in filesUTC)
                {
                    XDocument xdDoc = XDocument.Load(_file);

                    string sConv = xdDoc.Root.Descendants("ConversationURI").ToList()[0].Value;

                    if (!File.Exists(sFileExists + sConv + ".xml"))
                        Console.WriteLine();

                    if (!occDAO.ContainsKey(sConv))
                        occDAO.Add(sConv, xdDoc.Root.Descendants("ResRefName").ToList()[0].Value);
                    else
                    {
                        string temp = occDAO[sConv];
                        temp += '|' + xdDoc.Root.Descendants("ResRefName").ToList()[0].Value;
                        occDAO[sConv] = temp;
                    }
                }

                foreach (var d in occDAO)
                {
                    List<string> lString = new List<string>();

                    if (d.Value.Contains('|'))
                        lString = d.Value.Split('|').ToList();
                    else lString.Add(d.Value);

                    foreach (var l in lString)
                    {
                        string[] filesSky = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\NPC_\",
                          "*.xml",
                          SearchOption.AllDirectories);

                        foreach (string _file in filesSky)
                        {
                            XDocument xdDoc = XDocument.Load(_file);

                            string sConv = xdDoc.Root.Descendants("EDID").ToList()[0].
                                                Descendants("Name").ToList()[0].Value;

                            string sFormID = xdDoc.Root.Attribute("formID").Value;

                            if (sConv == l)
                            {
                                if (!occSKY.ContainsKey(d.Key))
                                    occSKY.Add(d.Key, sFormID);
                                else
                                {
                                    string temp = occSKY[d.Key];
                                    temp += '|' + sFormID;
                                    occSKY[d.Key] = temp;
                                }

                                break;
                            }
                        }
                    }
                }

                var csvDAO = new StringBuilder();

                foreach (var d in occDAO)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csvDAO.AppendLine(newLine);
                }

                File.WriteAllText("UTC_To_ConvDAO.csv", csvDAO.ToString());

                var csvSKY = new StringBuilder();

                foreach (var d in occSKY)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csvSKY.AppendLine(newLine);
                }

                File.WriteAllText("UTC_To_ConvSKY.csv", csvSKY.ToString());
            }

            bool bDialogCUT = false;
            if (bDialogCUT)
            {
                List<string> missingQUST = new List<string>();
                Dictionary<string, int> missingDLG = new Dictionary<string, int>();

                string[] filesCUT = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\CUT_2331DLG\",
                          "*.xml", SearchOption.AllDirectories);

                foreach (string _file in filesCUT)
                {
                    XDocument xdDoc = XDocument.Load(_file);

                    foreach (var t in xdDoc.Root.Descendants("text"))
                    {
                        int nConv = Convert.ToInt32(t.Value.ToString().Split('|')[0]);
                        string sLine = t.Value.ToString().Split('|')[1];

                        string[] filesDLG = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\DLG_2159\",
                            "*_" + nConv + ".xml", SearchOption.AllDirectories);

                        var xd = XDocument.Load(filesDLG[0]);

                        IEnumerable<XElement> blobs = from blob in xd.Root.Descendants("Agent")
                                                      where blob.Parent != xd.Root &&
                                                      blob.Descendants("StringID").ToList().Count != 0 &&
                                                      blob.Descendants("StringID").ToList()[0].Value == sLine
                                                      select blob;

                        if (blobs.ToList().Count > 1)
                            Console.WriteLine();

                        if (blobs.ToList().Count == 0) //CUT has DLG doesn't
                        {
                            if (!missingDLG.ContainsKey(xd.Root.Descendants("ResRefName").ToList()[0].Value))
                                missingDLG.Add(xd.Root.Descendants("ResRefName").ToList()[0].Value, Convert.ToInt32(sLine));
                        }

                        if (blobs.ToList().Count == 1)
                        {
                            if (blobs.ToList()[0].Descendants("ConditionPlotURI").ToList()[0].Value != "")
                            {
                                string sPlot = blobs.ToList()[0].Descendants("ConditionPlotURI").ToList()[0].Value;
                                string[] filesPLO = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\PLO_900\",
                                "*_" + sPlot + ".xml", SearchOption.AllDirectories);

                                var plot = XDocument.Load(filesPLO[0]);

                                string[] filesQUST = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                 "*" + plot.Descendants("ResRefName").ToList()[0].Value + ".xml", SearchOption.AllDirectories);

                                if (filesQUST.Length > 1)
                                    Console.WriteLine();

                                if (filesQUST.Length == 0)//not found
                                {
                                    if (!missingQUST.Contains(plot.Descendants("ResRefName").ToList()[0].Value))
                                        missingQUST.Add(plot.Descendants("ResRefName").ToList()[0].Value);
                                }
                            }
                            else
                            {
                                string[] filesQUST = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                 "*_Quest_" + xd.Descendants("ResRefName").ToList()[0].Value + ".xml", SearchOption.AllDirectories);

                                if (filesQUST.Length > 1)
                                    Console.WriteLine();

                                if (filesQUST.Length == 0)//not found
                                {
                                    if (!missingQUST.Contains(xd.Descendants("ResRefName").ToList()[0].Value))
                                        missingQUST.Add(xd.Descendants("ResRefName").ToList()[0].Value);
                                }
                            }
                        }

                    }
                }

                Console.WriteLine();
            }

            bool bGetUTC = false;
            if (bGetUTC)
            {
                string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTC_2477Simple\",
                  "*.xml",
                  SearchOption.AllDirectories);

                List<string> lines = new List<string>();
                List<string> apps = new List<string>();
                List<string> dups = new List<string>();

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);

                    string[] f1 = _file.Split('\\');
                    string f2 = f1[f1.Length - 1];

                    string f3 = f2.Split('.')[0];

                    bool bUnderscore = false;
                    if (f3.Contains('_'))
                        bUnderscore = true;

                    string sResRefName = xdDoc.Root.Descendants("ResRefName").ToList()[0].Value;

                    if (sResRefName != f3)
                        Console.WriteLine();

                    string sPattern = "";
                    if (bUnderscore)
                    {
                        sPattern = sResRefName.Split('_')[1];
                    }

                    bool bDup = false;
                    if (dups.Contains(sPattern))
                        bDup = true;
                    else dups.Add(sPattern);

                    int nRace = Convert.ToInt32(xdDoc.Root.Descendants("Race").ToList()[0].Value);
                    int nGender = Convert.ToInt32(xdDoc.Root.Descendants("Gender").ToList()[0].Value);
                    int nGroup = Convert.ToInt32(xdDoc.Root.Descendants("Group").ToList()[0].Value);
                    int nTeam = Convert.ToInt32(xdDoc.Root.Descendants("Team").ToList()[0].Value);
                    int nClass = Convert.ToInt32(xdDoc.Root.Descendants("Class").ToList()[0].Value);
                    int nAppearance = Convert.ToInt32(xdDoc.Root.Descendants("Appearance").ToList()[0].Value);

                    string line = sResRefName + ',' +
                        GetRace(nRace) + ',' +
                        ((nGender == 2) ? "Female" : "Male") + ',' +
                        GetGroup(nGroup) + ", Team " +
                        nTeam + ',' +
                        GetClass(nClass) + ',' +
                        (GetAppearance(nAppearance).Split('_')[1]);

                    lines.Add(line);
                    if (!apps.Contains(GetAppearance(nAppearance).Split('_')[1]))
                        apps.Add(GetAppearance(nAppearance).Split('_')[1]);

                    if (bDup)
                    {
                        //System.IO.File.Move(_file, _file + "_dup_of_" + sPattern);
                    }
                }

                var csv = new StringBuilder();

                foreach (var line in lines)
                {
                    csv.AppendLine(line);
                }

                File.WriteAllText("UTC.csv", csv.ToString());

                csv = new StringBuilder();

                foreach (var line in apps)
                {
                    csv.AppendLine(line);
                }

                File.WriteAllText("apps.csv", csv.ToString());
            }

            bool bMaxStage = false;
            if (bMaxStage)
            {
                List<string> lines = new List<string>();

                //INFO
                string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\QUST\Stages\",
                          "*.xml",
                          SearchOption.AllDirectories);

                int n = 0;

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);
                    foreach (var si in xdDoc.Descendants("Stage_Index"))
                    {
                        if (Convert.ToInt32(si.Value) < 3000)
                            n = Math.Max(n, Convert.ToInt32(si.Value));
                    }
                }

                var csv = new StringBuilder();
            }

            bool bPropertyNamesXML = false;
            if (bPropertyNamesXML)
            {
                Dictionary<string, uint> occ = new Dictionary<string, uint>();

                string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\NPC_\",
                          "*.xml",
                          SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);
                    if (xdDoc.Descendants("VMAD").ToList().Count > 0)
                    {
                        var xVMAD = xdDoc.Descendants("VMAD").ToList()[0];
                        var pnl = xVMAD.Descendants("propertyName").ToList();
                        foreach (var pn in pnl)
                        {
                            if (!occ.ContainsKey(pn.Value.ToString()))
                                occ.Add(pn.Value.ToString(), 1);
                            else
                            {
                                uint u = occ[pn.Value.ToString()];
                                u = u + 1;
                                occ[pn.Value.ToString()] = u;
                            }
                        }
                    }
                }

                var csv = new StringBuilder();

                foreach (var d in occ)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("PropertyNames.csv", csv.ToString());
            }

            bool bFunction = false;
            if (bFunction)
            {
                Dictionary<string, uint> occ = new Dictionary<string, uint>();

                //INFO
                string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\Dialog\4_INFO\",
                          "*.xml",
                          SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);

                    var cond = xdDoc.Root.Descendants("Function_Name");

                    foreach (var fn in cond)
                    {
                        if (!occ.ContainsKey(fn.Attribute("options").Value.ToString()))
                            occ.Add(fn.Attribute("options").Value.ToString(), 1);
                        else
                        {
                            uint u = occ[fn.Attribute("options").Value.ToString()];
                            u = u + 1;
                            occ[fn.Attribute("options").Value.ToString()] = u;
                        }
                    }
                }

                var csv = new StringBuilder();

                foreach (var d in occ)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("FunctionNames.csv", csv.ToString());
            }

            bool bCondition = false;
            if (bCondition)
            {
                Dictionary<string, uint> occ = new Dictionary<string, uint>();

                //INFO
                string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\Dialog\Misc\GetEventData\",
                          "*.xml",
                          SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);

                    IEnumerable<XElement> blobs = from blob in xdDoc.Root.Descendants("Condition")
                                                  where blob.Descendants("Function_Name").ToList()[0].Value == "576"
                                                  select blob;

                    foreach (var fn in blobs)
                    {
                        string p1 = fn.Descendants("Parameter_1").ToList()[0].Attribute("name").Value.ToString();
                        string p2 = fn.Descendants("Parameter_2").ToList()[0].Attribute("name").Value.ToString();
                        if (!occ.ContainsKey(p1 + '|' + p2))
                            occ.Add(p1 + '|' + p2, 1);
                        else
                        {
                            uint u = occ[p1 + '|' + p2];
                            u = u + 1;
                            occ[p1 + '|' + p2] = u;
                        }
                    }
                }

                var csv = new StringBuilder();

                foreach (var d in occ)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("FunctionNames.csv", csv.ToString());
            }

            bool bStageSkyrim = false;
            if (bStageSkyrim)
            {
                Dictionary<int, string> occ = new Dictionary<int, string>();

                //INFO
                string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\QUST\Stages\",
                          "*.xml",
                          SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);

                    IEnumerable<XElement> blobs = from blob in xdDoc.Root.Descendants("Stage")
                                                  select blob;

                    if (!occ.ContainsKey(blobs.ToList().Count))
                        occ.Add(blobs.ToList().Count, _file);
                }

                var csv = new StringBuilder();

                foreach (var d in occ)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("stages.csv", csv.ToString());
            }

            bool bStageDAO = false;
            if (bStageDAO)
            {
                Dictionary<string, int> occ = new Dictionary<string, int>();

                //INFO
                string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\PLO_900Simple\",
                          "*.xml",
                          SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    XDocument xdDoc = XDocument.Load(_file);

                    IEnumerable<XElement> blobs = from blob in xdDoc.Root.Descendants("StatusList").Descendants("Agent")
                                                  select blob;

                    occ.Add(_file, blobs.ToList().Count);
                }

                var csv = new StringBuilder();

                foreach (var d in occ)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("StagesDAO.csv", csv.ToString());
            }

            bool bNSS = false;
            if (bNSS)
            {
                List<string> lines = new List<string>();

                //INFO
                string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\NSS_E_2784\",
                          "*.nss",
                          SearchOption.AllDirectories);

                foreach (string _file in files)
                {
                    using (var _fileReader = File.OpenText(_file))
                    {
                        string _line;
                        while ((_line = _fileReader.ReadLine()) != null)
                        {
                            if (_line.Contains("uti") && _line.Contains(".uti"))
                            {
                                bool bQuote = false;
                                List<char> charArray = new List<char>();

                                foreach (var c in _line.ToCharArray())
                                {
                                    if ((c == ';' || c == ',') && bQuote == true)
                                    {
                                        bQuote = false;
                                        break;
                                    }

                                    if (c == '\"' && bQuote == true)
                                    {
                                        bQuote = false;
                                    }

                                    if (bQuote)
                                    {
                                        charArray.Add(c);
                                    }

                                    if (c == '\"' && bQuote == false)
                                    {
                                        bQuote = true;
                                    }
                                }

                                string itm = new string(charArray.ToArray());

                                if (!lines.Contains(itm.Split('.')[0]))
                                    lines.Add(itm.Split('.')[0]);
                            }

                        }

                        _fileReader.Close();
                    }
                }

                string[] nLines = lines.ToArray();
                File.WriteAllLines("uti.txt", nLines);
            }

            bool bItem = false;
            if (bItem)
            {
                Dictionary<string, string> occ = new Dictionary<string, string>();
                Dictionary<string, uint> it = new Dictionary<string, uint>();

                Dictionary<uint, string> baseItems = new Dictionary<uint, string>();

                using (var _fileReader = File.OpenText(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\BaseItem.csv"))
                {
                    string _line;
                    while ((_line = _fileReader.ReadLine()) != null)
                    {
                        baseItems.Add(Convert.ToUInt32(_line.Split(',')[0]), _line.Split(',')[1]);
                    }
                }

                string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTI_1261Simple\",
                  "*.xml",
                  SearchOption.AllDirectories);

                bool bRenameLocal = false;
                //rename
                if (bRenameLocal)
                {
                    foreach (string _file in files)
                    {
                        string[] f1 = _file.Split('\\');
                        string f2 = f1[f1.Length - 1];

                        string[] f3 = f2.Split('_');
                        string f4 = "";
                        for (int i = 0; i < f3.Length - 2; i++)
                        {
                            f4 = f4 + f3[i] + "_";
                        }
                        f4 = f4.TrimEnd('_');
                        f4 = f4 + ".xml";

                        string f5 = "";
                        for (int i = 0; i < f1.Length - 1; i++)
                        {
                            f5 = f5 + f1[i] + "\\";
                        }
                        f5 = f5 + f4;

                        System.IO.File.Move(_file, f5);
                    }

                    files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTI_1261Simple\",
                      "*.xml",
                      SearchOption.AllDirectories);
                }

                using (var _fileReader = File.OpenText(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\uti.txt"))
                {
                    string _line;
                    while ((_line = _fileReader.ReadLine()) != null)
                    {
                        foreach (string _file in files)
                        {
                            if (_file.Split('\\')[_file.Split('\\').Length - 1] == _line + ".xml")
                            {
                                XDocument xdDoc = XDocument.Load(_file);

                                uint nBaseItem = Convert.ToUInt32(xdDoc.Root.Descendants("BaseItemType").ToList()[0].Value);

                                if (baseItems.ContainsKey(nBaseItem))
                                {
                                    if (!occ.ContainsKey(_line))
                                    {
                                        occ.Add(_line, baseItems[nBaseItem] + '_' + nBaseItem);

                                        if (!it.ContainsKey(baseItems[nBaseItem]))
                                        {
                                            it.Add(baseItems[nBaseItem], nBaseItem);
                                        }
                                    }
                                    else
                                        Console.WriteLine();
                                }
                                else
                                {
                                    Console.WriteLine();
                                }
                            }
                        }
                    }
                }

                var csv = new StringBuilder();

                /*foreach (var d in occ)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }*/

                foreach (var d in it)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("Items.csv", csv.ToString());
            }

            bool bCombs = false;
            if (bCombs)
            {
                Console.WriteLine("\nSecond Test");
                char[] set2 = { 'S', 'k', 'y', 'r' };
                int k = 4;
                PrintAllKLength(set2, k);

                List<string> newCombs = new List<string>();
                foreach (var s1 in combs)
                {
                    if (s1.Length == 4)
                        newCombs.Add(s1);
                }

                File.WriteAllLines("combs.csv", newCombs.ToArray());
            }

            bool bGetItem = false;
            if (bGetItem)
            {
                List<string> sInfo = new List<string>();

                string[] directories = Directory.GetDirectories(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\Items\");
                List<string> lines = new List<string>();

                foreach (string ss in directories)
                {
                    lines = new List<string>();
                    string[] files = Directory.GetFiles(ss, "*.xml", SearchOption.AllDirectories);

                    foreach (string _file in files)
                    {
                        string newLine = "";

                        XDocument xdDoc = XDocument.Load(_file);

                        var keywords = xdDoc.Root.Descendants("KYWD_FormID");

                        foreach (var keyword in keywords)
                        {
                            string v = keyword.Attribute("name").Value;

                            if (v.Contains("Material"))
                                v = v.Replace("Material", "");
                            if (v.Contains("Armor"))
                                v = v.Replace("Armor", "");
                            if (v.Contains("Weap"))
                                v = v.Replace("Weap", "");
                            if (!v.Contains("Vendor"))
                            {
                                if (v.Contains("Type") && newLine.Length == 0)
                                    newLine += ',';

                                newLine = newLine + v + ',';
                            }
                        }

                        if (xdDoc.Root.Descendants("Armor_type").ToList().Count > 0)
                        {
                            string v = xdDoc.Root.Descendants("Armor_type").ToList()[0].Attribute("options").Value;
                            v = v.Replace(' ', '_');

                            newLine = newLine + v;
                        }

                        /*newLine = newLine.TrimStart(',');
                        newLine = newLine.TrimEnd(',');*/

                        if (newLine.Length > 0)
                            lines.Add(newLine);
                    }

                    File.WriteAllLines(ss + ".csv", lines.ToArray());
                }
            }

            try
            {
                var p = new Plugin(s, false, this.GetRecordFilter(s));

                RecordStructure.LoadPost();
                var rr = RecordStructure.Records;
                Console.WriteLine(rr.Count);

                PluginList.All.AddRecord(p);
                this.UpdateStringEditor();
                this.FixMasters();
                this.PluginTree.UpdateRoots();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            finally
            {
                GC.Collect();
            }
        }

        internal void PrintAllKLength(char[] set, int k)
        {
            int n = set.Length;
            PrintAllKLengthRec(set, "", n, k);
        }

        // The main recursive method
        // to print all possible 
        // strings of length k
        internal void PrintAllKLengthRec(char[] set,
                                    String prefix,
                                    int n, int k)
        {

            // Base case: k is 0,
            // print prefix
            if (k == 0)
            {
                Console.WriteLine(prefix);
                combs.Add(prefix);
                return;
            }

            // One by one add all characters 
            // from set and recursively 
            // call for k equals to k-1
            for (int i = 0; i < n; ++i)
            {

                String newPrefix = "";
                // Next character of input added
                if (!prefix.Contains(set[i]))
                    newPrefix = prefix + set[i];

                // k is decreased, because 
                // we have added a new character
                PrintAllKLengthRec(set, newPrefix,
                                        n, k - 1);
            }
        }

        internal bool PreFilterMessage(ref Message m)
        {
            // Intercept the left mouse button down message.
            if (m.Msg == MainViewMessageFilter.WM_KEYDOWN)
            {
                if (m.WParam == new IntPtr((int)Keys.F6))
                {
                    var current = this.dockPanel.ActiveContent;
                    if (current != null)
                    {
                        var next = current.DockHandler.NextActive;
                        if (next != null)
                        {
                            next.DockHandler.Activate();
                            return true;
                        }
                    }

                    bool forward = !MainViewMessageFilter.IsShiftDown();
                    var formList = Application.OpenForms.OfType<IDockContent>().ToList();
                    var first = formList.Where(
                        x =>
                        {
                            var f = x as Control;
                            return f != null && f.ContainsFocus;
                        }).FirstOrDefault();
                    if (first != null)
                    {
                        int idx = formList.IndexOf(first);

                        if (idx >= 0)
                        {
                            idx = ++idx % formList.Count;
                        }

                        var c = formList[idx];
                        if (c != null)
                        {
                            c.DockHandler.Activate();
                        }
                        else
                        {
                            first.DockHandler.GiveUpFocus();
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private static void ControlEnable(Control control, bool enable)
        {
            var ipenable = new IntPtr(enable ? 1 : 0);
            SendMessage(control.Handle, WM_SETREDRAW, ipenable, IntPtr.Zero);
        }

        private static bool IsVisible(IDockContent content)
        {
            return content.DockHandler.DockState != DockState.Hidden &&
                   content.DockHandler.DockState != DockState.Unknown;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void CloseAllContents()
        {
            // we don't want to create another instance of tool window, set DockPanel to null
            this.pluginTreeContent.DockPanel = null;
            this.subrecordListContent.DockPanel = null;
        }

        private void CloseStringEditor()
        {
            if (this.stringEditor != null)
            {
                StringsEditor editor = this.stringEditor;
                this.stringEditor = null;
                try
                {
                    if (!editor.IsDisposed)
                    {
                        editor.Close();
                    }
                }
                catch
                {
                }
            }
        }

        private void CopySelectedSubRecord()
        {
            var sr = this.GetSelectedSubrecords();
            if (sr == null)
            {
                return;
            }

            Clipboard = sr.Select(ss => (SubRecord)ss.Clone()).ToArray();
        }

        private void CopySelection()
        {
            // Route to focused control.
            if (this.PluginTree.ContainsFocus)
            {
                this.PluginTree.CopySelectedRecord();
            }
            else if (this.SubrecordList.ContainsFocus)
            {
                if (this.Selection.SelectedSubrecord)
                {
                    this.CopySelectedSubRecord();
                }
            }
        }

        private void EnableUserInterface(bool enable)
        {
            // ControlEnable(this.splitHorizontal, enable);
            // ControlEnable(this.splitVertical, enable);
            ControlEnable(this.menuStrip1, enable);
            ControlEnable(this.toolStripIncrFind, enable);
            ControlEnable(this.toolStripIncrInvalidRec, enable);
        }

        private void FixMasters()
        {
            PluginList.FixMasters();
        }

        private IDockContent GetContentFromPersistString(string persistString)
        {
            if (persistString == typeof(PluginTreeContent).ToString())
            {
                return this.pluginTreeContent;
            }

            if (persistString == typeof(SubrecordListContent).ToString())
            {
                return this.subrecordListContent;
            }

            if (persistString == typeof(RichTextContent).ToString())
            {
                return this.selectedTextContent;
            }

            return null;
        }

        private Plugin GetPluginFromNode(BaseRecord node)
        {
            BaseRecord tn = node;
            if (tn is Plugin)
            {
                return (Plugin)tn;
            }

            while (!(tn is Plugin) && tn != null)
            {
                tn = tn.Parent;
            }

            if (tn != null)
            {
                return tn as Plugin;
            }

            return new Plugin();
        }

        private Record GetRecordByID(uint id)
        {
            if (this.Selection != null && this.Selection.Record != null)
            {
                var p = this.GetPluginFromNode(this.Selection.Record);
                if (p != null)
                {
                    return p.GetRecordByID(id);
                }
            }

            return null;
        }

        private string[] GetRecordFilter(string s)
        {
            string[] recFilter = null;
            bool bAskToApplyFilter = true;
            if (Settings.Default.IsFirstTimeOpeningSkyrimESM)
            {
                if (string.Compare(Path.GetFileName(s), "skyrim.esm", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    DialogResult result = MessageBox.Show(
                        this, Resources.MainView_FirstTimeSkyrimLoad_ExcludeInquiry, Resources.FirstLoadOptions,
                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                    if (result == DialogResult.Yes)
                    {
                        Settings.Default.EnableESMFilter = true;
                        Settings.Default.DontAskUserAboutFiltering = true;
                        using (var settings = new LoadSettings())
                        {
                            result = settings.ShowDialog(this);
                            if (result == DialogResult.Cancel)
                            {
                                // cancel will be same as No
                                Settings.Default.EnableESMFilter = false;
                                Settings.Default.DontAskUserAboutFiltering = true;
                            }
                        }

                        Settings.Default.IsFirstTimeOpeningSkyrimESM = false;
                    }
                    else if (result == DialogResult.No)
                    {
                        Settings.Default.IsFirstTimeOpeningSkyrimESM = false;
                        Settings.Default.DontAskUserAboutFiltering = true;
                    }
                    else
                    {
                        Settings.Default.IsFirstTimeOpeningSkyrimESM = false;
                        return null;
                    }
                }

                bAskToApplyFilter = false;
            }

            if (Settings.Default.EnableESMFilter)
            {
                bool applyfilter;
                if (Settings.Default.ApplyFilterToAllESM)
                {
                    applyfilter = string.Compare(Path.GetExtension(s), ".esm", StringComparison.OrdinalIgnoreCase) == 0;
                }
                else
                {
                    applyfilter =
                        string.Compare(Path.GetFileName(s), "skyrim.esm", StringComparison.OrdinalIgnoreCase) == 0;
                }

                if (applyfilter && bAskToApplyFilter && !Settings.Default.DontAskUserAboutFiltering)
                {
                    DialogResult result = MessageBox.Show(
                        this, Resources.ESM_Large_File_Size_Inquiry, Resources.Filter_Options_Text,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                    applyfilter = result == DialogResult.Yes;
                }

                if (applyfilter)
                {
                    recFilter = Settings.Default.FilteredESMRecords.Trim().Split(new[] { ';', ',' },
                                                                                 StringSplitOptions.RemoveEmptyEntries);
                }
            }

            return recFilter;
        }

        private SelectionContext GetSelectedContext()
        {
            return this.Selection;

            // context.Record = this.parentRecord
            // context.SubRecord = GetSelectedSubrecord();
        }

        private SubRecord GetSelectedSubrecord()
        {
            return this.SubrecordList.GetSelectedSubrecord();
        }

        private IEnumerable<SubRecord> GetSelectedSubrecords()
        {
            return this.SubrecordList.GetSelectedSubrecords();
        }

        private void InitializeDockingWindows()
        {
            this.mDeserializeDockContent = this.GetContentFromPersistString;
        }

        private void InitializeLanguage()
        {
            this.languageToolBarItems.Add("English", this.englishToolStripMenuItem);
            this.languageToolBarItems.Add("Czech", this.czechToolStripMenuItem);
            this.languageToolBarItems.Add("French", this.frenchToolStripMenuItem);
            this.languageToolBarItems.Add("German", this.germanToolStripMenuItem);
            this.languageToolBarItems.Add("Italian", this.italianToolStripMenuItem);
            this.languageToolBarItems.Add("Spanish", this.spanishToolStripMenuItem);
            this.languageToolBarItems.Add("Russian", this.russianToolStripMenuItem);
            this.languageToolBarItems.Add("Polish", this.polishToolStripMenuItem);
        }

        private void LayoutDockingWindows(bool force)
        {
            try
            {
                if (!force && IsVisible(this.pluginTreeContent) && IsVisible(this.subrecordListContent) &&
                    IsVisible(this.selectedTextContent))
                {
                    return;
                }

                this.dockPanel.SuspendLayout(true);
                if (force)
                {
                    this.pluginTreeContent.DockPanel = null;
                    this.subrecordListContent.DockPanel = null;
                    this.selectedTextContent.DockPanel = null;
                }

                if (!IsVisible(this.pluginTreeContent) || force)
                {
                    this.pluginTreeContent.Show(this.dockPanel, DockState.DockLeft);
                    this.dockPanel.Width = Math.Max(this.dockPanel.Width, this.pluginTreeContent.MinimumSize.Width);
                }

                if (!IsVisible(this.subrecordListContent) || force)
                {
                    this.subrecordListContent.Show(this.pluginTreeContent.Pane, DockAlignment.Bottom, 0.5);
                }

                if (!IsVisible(this.selectedTextContent) || force)
                {
                    this.selectedTextContent.Show(this.dockPanel, DockState.Document);
                }
            }
            catch
            {
            }
            finally
            {
                this.dockPanel.ResumeLayout(true, true);
            }
        }

        private void LoadDockingWindows()
        {
            string configFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), @"conf\DockPanel.config");
            if (File.Exists(configFile))
            {
                try
                {
                    this.dockPanel.SuspendLayout(true);
                    this.dockPanel.LoadFromXml(configFile, this.mDeserializeDockContent);
                }
                catch
                {
                    if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
                    {
                        try
                        {
                            File.Delete(configFile);
                        }
                        catch
                        {
                        }
                    }
                }
                finally
                {
                    this.dockPanel.ResumeLayout(true, true);
                }
            }

            this.LayoutDockingWindows(force: false);
        }

        private string LookupFormIDI(uint id)
        {
            return this.LookupFormIDI(this.Selection, id);
        }

        private string LookupFormIDI(SelectionContext context, uint id)
        {
            if (context != null && context.Record != null)
            {
                var p = this.GetPluginFromNode(context.Record);
                if (p != null)
                {
                    p.LookupFormID(id);
                }
            }

            return "No selection";
        }

        private string LookupFormStrings(uint id)
        {
            if (this.Selection != null && this.Selection.Record != null)
            {
                var p = this.GetPluginFromNode(this.Selection.Record);
                if (p != null)
                {
                    return p.LookupFormStrings(id);
                }
            }

            return null;
        }

        private void MainView_Load(object sender, EventArgs e)
        {
            this.LoadDockingWindows();
            this.FixMasters();
            this.toolStripIncrFind.Visible = false;
            this.toolStripIncrFind.Enabled = false;
            this.toolStripIncrInvalidRec.Visible = false;
            this.toolStripIncrInvalidRec.Enabled = false;
        }

        private void MainView_Shown(object sender, EventArgs e)
        {
            //// Only prevent content hiding after window if first shown
            // dockingManagerExtender.DockingManager.ContentHiding +=
            // delegate(Content c, CancelEventArgs cea) { cea.Cancel = true; };
            // dockingManagerExtender.DockingManager.ShowAllContents();
            this.ShowDockingWindows();

            if (!DesignMode)
            {
                try
                {
                    Assembly asm = Assembly.GetExecutingAssembly();
                    var attr =
                        asm.GetCustomAttributes(true).OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
                    if (attr != null)
                    {
                        Text = attr.InformationalVersion;
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// This routine assigns Structure definitions to subrecords.
        /// </summary>
        /// <returns>
        /// The System.Boolean.
        /// </returns>
        private bool MatchRecordStructureToRecord()
        {
            var rec = this.Selection.Record as Record;
            if (rec == null)
            {
                return false;
            }

            return rec.MatchRecordStructureToRecord();
        }

        private bool MatchRecordStructureToRecord(Record rec)
        {
            //var rec = this.Selection.Record as Record;
            if (rec == null)
            {
                return false;
            }

            return rec.MatchRecordStructureToRecord();
        }
        private void PasteFromClipboard(bool recordOnly, bool asNew)
        {
            if (!HasClipboardData())
            {
                MessageBox.Show(Resources.TheClipboardIsEmpty, Resources.ErrorText);
                return;
            }

            if (this.PluginTree.ContainsFocus)
            {
                this.PluginTree.PasteFromClipboard(recordOnly, asNew);
            }
            else if (this.SubrecordList.ContainsFocus)
            {
                this.SubrecordList.PasteFromClipboard();
            }
        }

        private void PluginTree_OnSelectionUpdated(object sender, EventArgs e)
        {
            // fix EDID if relevant
            this.UpdateMainText(this.PluginTree.SelectedRecord);
            this.PluginTree.RefreshObject(this.PluginTree.SelectedRecord);
        }

        private void PluginTree_SelectionChanged(object sender, EventArgs e)
        {
            this.UpdateMainText(this.PluginTree.SelectedRecord);
        }

        private void RebuildSelection()
        {
            if (this.inRebuildSelection)
            {
                return;
            }

            bool oldInRebuildSelection = this.inRebuildSelection;
            try
            {
                this.inRebuildSelection = true;
                var rec = this.PluginTree.SelectedRecord;
                if (rec == null)
                {
                    this.SubrecordList.Record = null;
                    this.Selection.Record = null;
                    this.UpdateMainText(string.Empty);
                    return;
                }

                bool hasClipboard = HasClipboardData();

                if (rec is Plugin)
                {
                    this.SubrecordList.Record = null;
                    this.Selection.Record = null;
                    this.cutToolStripMenuItem.Enabled = false;
                    this.copyToolStripMenuItem.Enabled = false;
                    this.deleteToolStripMenuItem.Enabled = false;
                    this.pasteToolStripMenuItem.Enabled = hasClipboard;
                    this.pasteNewToolStripMenuItem.Enabled = hasClipboard;
                    this.insertGroupToolStripMenuItem.Enabled = true;
                    this.insertRecordToolStripMenuItem.Enabled = true;
                    this.insertSubrecordToolStripMenuItem.Enabled = false;
                }
                else if (rec is Record)
                {
                    this.cutToolStripMenuItem.Enabled = true;
                    this.copyToolStripMenuItem.Enabled = true;
                    this.deleteToolStripMenuItem.Enabled = true;
                    this.pasteToolStripMenuItem.Enabled = hasClipboard;
                    this.pasteNewToolStripMenuItem.Enabled = hasClipboard;
                    this.insertGroupToolStripMenuItem.Enabled = false;
                    this.insertRecordToolStripMenuItem.Enabled = true;
                    this.insertSubrecordToolStripMenuItem.Enabled = true;
                    this.Selection.Record = rec as Rec;
                    this.SubrecordList.Record = this.Selection.Record as Record;
                    this.MatchRecordStructureToRecord();
                }
                else if (rec is GroupRecord)
                {
                    if (!BoolMove)
                    {
                        foreach (var _rec in rec.Records)
                        {
                            if (_rec is Record)
                            {
                                Selection.Record = _rec as Rec;
                                SubrecordList.Record = Selection.Record as Record;
                                MatchRecordStructureToRecord();
                            }
                            else if (_rec is GroupRecord)
                            {
                                foreach (var _rrec in (_rec as GroupRecord).Records)
                                {
                                    if (_rrec is Record)
                                    {
                                        Selection.Record = _rrec as Rec;
                                        SubrecordList.Record = Selection.Record as Record;
                                        MatchRecordStructureToRecord();
                                    }
                                    else
                                        Console.WriteLine();
                                }
                            }
                        }
                    }

                    this.Selection.Record = null;
                    this.SubrecordList.Record = null;
                    this.cutToolStripMenuItem.Enabled = true;
                    this.copyToolStripMenuItem.Enabled = true;
                    this.deleteToolStripMenuItem.Enabled = true;
                    this.pasteToolStripMenuItem.Enabled = hasClipboard;
                    this.pasteNewToolStripMenuItem.Enabled = hasClipboard;
                    this.insertGroupToolStripMenuItem.Enabled = true;
                    this.insertRecordToolStripMenuItem.Enabled = true;
                    this.insertSubrecordToolStripMenuItem.Enabled = false;
                }
                else
                {
                    this.Selection.Record = null;
                    this.SubrecordList.Record = null;
                    this.cutToolStripMenuItem.Enabled = false;
                    this.copyToolStripMenuItem.Enabled = false;
                    this.deleteToolStripMenuItem.Enabled = false;
                    this.pasteToolStripMenuItem.Enabled = false;
                    this.pasteNewToolStripMenuItem.Enabled = false;
                    this.insertGroupToolStripMenuItem.Enabled = false;
                    this.insertRecordToolStripMenuItem.Enabled = false;
                    this.insertSubrecordToolStripMenuItem.Enabled = false;
                }

                this.Selection.SubRecord = this.GetSelectedSubrecord();
            }
            finally
            {
                this.inRebuildSelection = oldInRebuildSelection;
            }
        }

        private void ReferenceSearch(uint formid)
        {
            var search = this.CreateSearchWindow();
            search.ReferenceSearch(formid);
        }

        private void RegisterMessageFilter()
        {
            // Register message filter.
            try
            {
                var msgFilter = new MainViewMessageFilter(this);
                Application.AddMessageFilter(msgFilter);
            }
            catch
            {
            }
        }

        private void ReloadLanguageFiles()
        {
            foreach (Plugin p in PluginList.All.Records)
            {
                p.ReloadStrings();
            }
        }

        private void SaveDockingWindows()
        {
            string configFile = null;
            try
            {
                configFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), @"conf\DockPanel.config");
                this.dockPanel.SaveAsXml(configFile);
            }
            catch
            {
                if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
                {
                    try
                    {
                        File.Delete(configFile);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void ShowDockingWindows()
        {
            this.selectedTextContent.RtfInfo.LinkClicked += this.rtfInfo_LinkClicked;
            this.selectedTextContent.RtfInfo.PreviewKeyDown += this.tbInfo_PreviewKeyDown;
            this.pluginTreeContent.CloseButtonVisible = false;
            this.subrecordListContent.CloseButtonVisible = false;
            this.selectedTextContent.MdiParent = this;
            this.selectedTextContent.CloseButtonVisible = false;
            this.selectedTextContent.CloseButton = false;
            this.selectedTextContent.HideOnClose = true;
            this.LayoutDockingWindows(force: false);
        }

        private void TESsnip_FormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Default.UseWindowsClipboard = this.useWindowsClipboardToolStripMenuItem.Checked;
            PluginList.All.Clear();
            this.PluginTree.UpdateRoots();
            Clipboard = null;
            this.Selection.Record = null;
            this.RebuildSelection();
            this.CloseStringEditor();
            this.SaveDockingWindows();
            Domain.Services.Settings.SetWindowPosition("TESsnip", this);
        }

        private void UpdateMainText(BaseRecord rec)
        {
            if (rec == null)
            {
                this.UpdateMainText(string.Empty);
            }
            else
            {
                FontLangInfo defLang;
                if (!Framework.Services.Encoding.TryGetFontInfo(Settings.Default.LocalizationName, out defLang))
                {
                    defLang = new FontLangInfo(1252, 1033, 0);
                }

                var rb = new RTFBuilder(RTFFont.Arial, 16, defLang.lcid, defLang.charset);
                rec.GetFormattedHeader(rb);
                rec.GetFormattedData(rb);
                this.SelectedText.Rtf = rb.ToString();
                if (rec is Plugin)
                {
                    Plugin _plugin = rec as Plugin;

                    //look for 
                    if (BoolRemoveACHRFromWorld)//breaks game, don't use
                    {
                        Queue<BaseRecord> q = new Queue<BaseRecord>();
                        foreach (var g in _plugin.Records)
                        {
                            if (g is GroupRecord)
                                q.Enqueue(g as BaseRecord);
                        }

                        while (q.Count > 0)
                        {
                            var qq = q.Dequeue();
                            if (qq is GroupRecord)
                            {
                                bool bf = false;
                                foreach (var gg in qq.Records)
                                {
                                    if (gg is Record)
                                    {
                                        var r = gg as Record;
                                        if (r.DescriptiveName.Contains("ACHR"))
                                        {
                                            if (r.DescriptiveName.Contains("Guard") ||
                                            r.DescriptiveName.Contains("Soldier"))
                                            {
                                                bf = true;
                                                break;
                                            }
                                        }
                                    }
                                    else if (gg is GroupRecord)
                                        q.Enqueue(gg as BaseRecord);
                                }

                                if (bf)
                                {
                                    List<BaseRecord> rs = new List<BaseRecord>();

                                    foreach (var r in (qq as GroupRecord).Records)
                                    {
                                        if (r is GroupRecord)
                                            Console.WriteLine();

                                        if ((r as Record).DescriptiveName.Contains("ACHR"))
                                        {
                                            if ((r as Record).DescriptiveName.Contains("Guard") ||
                                            (r as Record).DescriptiveName.Contains("Soldier"))
                                            {
                                                Console.WriteLine();
                                            }
                                        }
                                        else
                                            rs.Add(r as Record);
                                    }

                                    while ((qq as GroupRecord).Records.Count > 0)
                                        (qq as GroupRecord).Records.RemoveAt((qq as GroupRecord).Records.Count - 1);

                                    foreach (var r in rs)
                                        (qq as GroupRecord).Records.Add(r as Record);
                                }
                            }
                        }
                    }

                    if (BoolUpdateXML)
                    {
                        var s = XDocument.Load(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_chineseO.xml");
                        var ds = s.Root.Descendants("String").Where(x => x.Descendants("REC").ToList()[0].Value == "CELL:FULL");

                        var xContent = new XElement("Content");
                        var xdDocument = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xContent);

                        foreach (var d in ds)
                        {
                            xContent.Add(d);
                        }

                        xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_chinese_DAO.xml");

                        BoolUpdateXML = false;
                    }

                    if (BoolDelete)
                    {
                        Plugin pSource = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                        foreach (var g in pSource.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType != "VTYP" &&
                                g is GroupRecord && (g as GroupRecord).ContentsType != "QUST" &&
                                g is GroupRecord && (g as GroupRecord).ContentsType != "NPC_" &&
                                g is GroupRecord && (g as GroupRecord).ContentsType != "CELL")
                            {
                                while ((g as GroupRecord).Records.Count > 0)
                                    (g as GroupRecord).Records.RemoveAt((g as GroupRecord).Records.Count - 1);
                            }
                        }

                        List<GroupRecord> grs = new List<GroupRecord>();

                        foreach (var g in pSource.Records)
                        {
                            /*g is GroupRecord && (g as GroupRecord).ContentsType == "DLBR" ||
                                g is GroupRecord && (g as GroupRecord).ContentsType == "DLVW" ||
                                g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL" ||*/

                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "VTYP" ||
                                g is GroupRecord && (g as GroupRecord).ContentsType == "QUST" ||
                                g is GroupRecord && (g as GroupRecord).ContentsType == "NPC_" ||
                                g is GroupRecord && (g as GroupRecord).ContentsType == "CELL")
                            {
                                grs.Add(g as GroupRecord);
                            }
                        }

                        while (pSource.Records.Count > 1)
                        {
                            pSource.Records.RemoveAt(pSource.Records.Count - 1);
                        }

                        foreach (var g in grs)
                        {
                            pSource.Records.Add(g);
                        }

                        var xdTalk = XDocument.Load(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english.xml");

                        var dStrings = xdTalk.Root.Descendants("String");

                        var xContent = new XElement("Content");
                        var xdDocument = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xContent);

                        foreach (var ds in dStrings)
                        {
                            if (!ds.Descendants("REC").ToList()[0].Value.Contains("VTYP") &&
                                !ds.Descendants("REC").ToList()[0].Value.Contains("QUST") &&
                                !ds.Descendants("REC").ToList()[0].Value.Contains("NPC_") &&
                                !ds.Descendants("REC").ToList()[0].Value.Contains("CELL"))
                            {
                                //ds.Remove();
                            }
                            else
                            {
                                xContent.Add(ds);
                            }
                        }

                        xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english_DAO.xml");

                        BoolDelete = false;

                        Console.WriteLine();
                    }

                    if (BoolCELL && Cells.Count > 0)
                    {
                        Cells[5].Records.Add(Cells[6]);
                        Cells[4].Records.Add(Cells[5]);
                        Cells[2].Records.Add(Cells[3]);
                        Cells[2].Records.Add(Cells[4]);
                        Cells[1].Records.Add(Cells[2]);
                        Cells[0].Records.Add(Cells[1]);

                        BaseRecord[] record = new BaseRecord[1];
                        record[0] = Cells[0];
                        object[] nodes = new object[2];
                        nodes[0] = record;
                        nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                        int res = CopyRecordsTo(nodes);

                        BoolCELL = false;
                    }

                    if (BoolVoiceType)
                    {
                        /*Dictionary<string, uint> Voices = new Dictionary<string, uint>();

                        string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\NPC_\",
                            "*.xml", SearchOption.TopDirectoryOnly);

                        foreach (string _file in files)
                        {
                            XDocument xd = XDocument.Load(_file);
                            string sFormID = xd.Root.Descendants("VTYP_FormID").ToList()[0].Value;
                            uint nFormID = uint.Parse(sFormID, System.Globalization.NumberStyles.HexNumber);
                            string sVoice = xd.Root.Descendants("VTYP_FormID").ToList()[0].Attribute("name").Value;

                            if (sVoice.ToLower().Contains("male"))
                                if (!Voices.ContainsKey(sVoice))
                                    Voices.Add(sVoice, nFormID);
                        }*/

                        CounterFormID = Convert.ToUInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[0]);
                        CounterTalk = Convert.ToInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[1]);

                        Record voice = null;

                        Plugin pSource = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;
                        Plugin pDest = PluginList.All.Records.OfType<BaseRecord>().ToList()[1] as Plugin;

                        foreach (var g in pSource.Records)
                        {
                            bool bFound = false;

                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "VTYP")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    bFound = true;
                                    voice = (r as Record).Clone() as Record;
                                    break;
                                }
                            }

                            if (bFound)
                                break;
                        }

                        if (voice == null)
                            Console.WriteLine();

                        //male
                        var voiceMale = voice.Clone() as Record;
                        (voiceMale as Record).FormID = (TopLevelControl as MainView).CounterFormID;
                        (TopLevelControl as MainView).CounterFormID++;

                        foreach (var sr in voiceMale.SubRecords)
                        {
                            if (sr.Name == "EDID")
                            {
                                string sEdid = "MaleUniqueHajduk";

                                byte[] plusNull = new byte[System.Text.Encoding.ASCII.GetBytes(sEdid).Length + 1];
                                Array.Copy(System.Text.Encoding.ASCII.GetBytes(sEdid), plusNull, System.Text.Encoding.ASCII.GetBytes(sEdid).Length);

                                sr.SetData(plusNull);
                            }
                        }

                        BaseRecord[] record = new BaseRecord[1];
                        record[0] = voiceMale;
                        object[] nodes = new object[2];
                        nodes[0] = record;
                        nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                        int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);

                        //female
                        var voiceFemale = voice.Clone() as Record;
                        (voiceFemale as Record).FormID = (TopLevelControl as MainView).CounterFormID;
                        (TopLevelControl as MainView).CounterFormID++;

                        foreach (var sr in voiceFemale.SubRecords)
                        {
                            if (sr.Name == "EDID")
                            {
                                string sEdid = "FemaleUniqueHajduk";

                                byte[] plusNull = new byte[System.Text.Encoding.ASCII.GetBytes(sEdid).Length + 1];
                                Array.Copy(System.Text.Encoding.ASCII.GetBytes(sEdid), plusNull, System.Text.Encoding.ASCII.GetBytes(sEdid).Length);

                                sr.SetData(plusNull);
                            }
                        }

                        record = new BaseRecord[1];
                        record[0] = voiceFemale;
                        nodes = new object[2];
                        nodes[0] = record;
                        nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                        res = (TopLevelControl as MainView).CopyRecordsTo(nodes);

                        string[] lines = new string[1];
                        uint counterFormID = CounterFormID++;
                        int counterTalk = CounterTalk++;
                        lines[0] = counterFormID.ToString() + ',' + counterTalk.ToString();

                        using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
                        {
                            newTask.WriteLine(lines[0].ToString());
                        }

                        BoolVoiceType = false;

                        Console.WriteLine();
                    }

                    if (BoolTCLT)
                    {
                        foreach (var g in _plugin.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    bool bIDAT = false;
                                    byte[] seq = new byte[] { 0x49, 0x44, 0x41, 0x54 };//IDAT

                                    if (r is Record)
                                    {
                                        foreach (var sr in (r as Record).SubRecords)
                                        {
                                            if (sr.Name == "SNAM")
                                            {
                                                byte[] data = sr.GetData();
                                                if (data.SequenceEqual(seq))
                                                    bIDAT = true;
                                            }
                                        }
                                    }

                                    if (r is GroupRecord)
                                    {
                                        if (bIDAT)
                                        {
                                            foreach (var info in (r as GroupRecord).Records)
                                            {
                                                foreach (var sr in (info as Record).SubRecords)
                                                {
                                                    if (sr.Name == "TCLT")
                                                        Console.WriteLine();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //get INFO Group and Generic VMAD for later
                    if (BoolMove)
                    {
                        foreach (var g in _plugin.Records)
                        {
                            bool bFound = false;

                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    if (r is GroupRecord)
                                    {
                                        GroupInfo = r as GroupRecord;
                                        bFound = true;
                                        break;
                                    }
                                }
                            }

                            if (bFound)
                                break;
                        }
                    }

                    if (BoolCopyDialog)
                    {
                        //EXPORT DIALOG IN CK FIRST!!!
                        List<string> lines = new List<string>();
                        using (var _fileReader = File.OpenText(@"d:\Program Files (x86)\Steam\steamapps\common\Skyrim\dialogueExport.txt"))
                        {
                            string _line;

                            while ((_line = _fileReader.ReadLine()) != null)
                            {
                                if (_line.Contains("HajdukAge") && !_line.Contains("PlaceHolder"))
                                {
                                    lines.Add(_line);
                                }
                            }

                            _fileReader.Close();
                        }

                        foreach (var _line in lines)
                        {
                            var splitTab = _line.Split('\t');
                            foreach (var sTab in splitTab)
                            {
                                if (sTab.Contains("HajdukAge.esp"))
                                {
                                    var splitBackslash = sTab.Split('\\');

                                    var splitUnderscore = splitBackslash[splitBackslash.Length - 1].Split('_');
                                    var sName = splitBackslash[splitBackslash.Length - 1].Split('.')[0];

                                    var sFormID = splitUnderscore[splitUnderscore.Length - 2];

                                    char[] arrFormID = sFormID.ToCharArray();
                                    arrFormID[1] = '3';

                                    sFormID = new string(arrFormID);

                                    var xdTalk = XDocument.Load(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english.xml");

                                    var dStrings = xdTalk.Root.
                                        Descendants("String").Where(x => x.Descendants("EDID").
                                        ToList()[0].Value.TrimStart('[').TrimEnd(']') == sFormID);

                                    if (dStrings.ToList().Count != 1)
                                        Console.WriteLine();

                                    //added npcStringID to keep track of duplicates while keeping sID unique
                                    uint nID = uint.Parse(dStrings.ToList()[0].Attribute("npcStringID").Value.ToString(), System.Globalization.NumberStyles.HexNumber);

                                    //string sID = nID.ToString() + "_m.xwm";
                                    string sID = nID.ToString() + "_m.wav";

                                    //sName += ".xwm";
                                    sName += ".wav";

                                    /*string[] filesXWM = Directory.GetFiles
                                        (@"d:\Work\Sounds\DAO Sounds\WAV2XWM\", sID,
                                        System.IO.SearchOption.TopDirectoryOnly);*/

                                    string[] filesXWM = Directory.GetFiles
                                        (@"d:\Work\Sounds\DAO Sounds\MP32WAV\", sID,
                                        System.IO.SearchOption.TopDirectoryOnly);

                                    if (filesXWM.Length != 1)
                                        Console.WriteLine();

                                    string sDest = @"d:\Program Files (x86)\Steam\steamapps\common\Skyrim\Data\Sound\Voice\HajdukAge.esp\";

                                    if (!File.Exists(sDest + "FemaleUniqueHajduk\\" + sName))
                                        System.IO.File.Copy(filesXWM[0], sDest + "FemaleUniqueHajduk\\" + sName, false);
                                    if (!File.Exists(sDest + "MaleUniqueHajduk\\" + sName))
                                        System.IO.File.Copy(filesXWM[0], sDest + "MaleUniqueHajduk\\" + sName, false);
                                }
                            }
                        }

                        string[] nLines = lines.ToArray();
                        File.WriteAllLines("dialog.txt", nLines);

                        BoolCopyDialog = false;
                    }

                    if (BoolGetHELO)
                    {
                        Plugin pDIAL = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                        foreach (var g in pDIAL.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL")
                            {
                                bool bHelo = false;
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    if (r is Record)
                                    {
                                        foreach (var sr in (r as Record).SubRecords)
                                        {
                                            if (sr.Name == "SNAM")
                                            {
                                                //HE
                                                if (sr.GetData()[0] == Convert.ToByte(0x48) &&
                                                    sr.GetData()[1] == Convert.ToByte(0x45))
                                                    bHelo = true;
                                            }
                                        }
                                    }
                                    if (r is GroupRecord)
                                    {
                                        if (bHelo)
                                        {
                                            foreach (var rr in (r as GroupRecord).Records)
                                            {
                                                int nTrdt = 0;

                                                foreach (var srr in (rr as Record).SubRecords)
                                                {
                                                    if (srr.Name == "TRDT")
                                                        nTrdt++;
                                                }

                                                if (nTrdt > 1)
                                                    Console.WriteLine();
                                            }

                                            bHelo = false;
                                        }
                                    }
                                }
                            }
                        }

                        BoolGetHELO = false;
                    }

                    if (BoolUpdateDIAL)
                    {
                        /*Plugin pDIAL = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                        foreach (var g in pDIAL.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    if (r is Record)
                                    {
                                    }
                                }
                            }
                        }*/

                        /*CounterFormID = Convert.ToUInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[0]);
                        CounterTalk = Convert.ToInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[1]);

                        XElement xContent = new XElement("Content");
                        XDocument xdDocument = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xContent);

                        Plugin pDIAL = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;
                        SubRecord fullTemplate = null;

                        foreach (var g in pDIAL.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var sr in ((g as GroupRecord).Records[0] as Record).SubRecords)
                                {
                                    if (sr.Name == "FULL")
                                        fullTemplate = sr.Clone() as SubRecord;
                                }
                            }
                        }

                        if (fullTemplate == null)
                            Console.WriteLine();

                        foreach (var g in pDIAL.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    if (r is Record)
                                    {
                                        var srs = new List<SubRecord>();

                                        foreach (var sr in (r as Record).SubRecords)
                                        {
                                            srs.Add(sr.Clone() as SubRecord);
                                        }

                                        while ((r as Record).SubRecords.Count > 0)
                                            (r as Record).SubRecords.RemoveAt((r as Record).SubRecords.Count - 1);

                                        var localFull = fullTemplate.Clone() as SubRecord;

                                        string hexName = CounterTalk.ToString("X8");
                                        string hexName6 = CounterTalk.ToString("X6");

                                        byte[] arrNameFull = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
                                        arrNameFull = arrNameFull.Reverse().ToArray();

                                        localFull.SetData(arrNameFull);

                                        string sEdid = System.Text.Encoding.Default.GetString(srs[0].GetData());
                                        sEdid = sEdid.TrimEnd('\0');

                                        XElement xString = new XElement("String");
                                        xString.Add(new XAttribute("List", 0));
                                        xString.Add(new XAttribute("sID", hexName6));

                                        XElement xEdid = new XElement("EDID", sEdid);
                                        xString.Add(xEdid);

                                        XElement xRec = new XElement("REC", "DIAL:FULL");
                                        xString.Add(xRec);

                                        XElement xSource = new XElement("Source", "(Invisible continue)");
                                        xString.Add(xSource);

                                        XElement xDest = new XElement("Dest", "(Invisible continue)");
                                        xString.Add(xDest);

                                        xContent.Add(xString);

                                        srs.Insert(1, localFull);

                                        CounterTalk++;

                                        foreach (var sr in srs)
                                        {
                                            (r as Record).SubRecords.Add(sr.Clone() as SubRecord);
                                        }
                                    }
                                }
                            }
                        }

                        xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english_DAO.xml");

                        string[] lines = new string[1];
                        lines[0] = CounterFormID.ToString() + ',' + CounterTalk.ToString();

                        using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
                        {
                            newTask.WriteLine(lines[0].ToString());
                        }

                        BoolUpdateDIAL = false;*/
                    }

                    //for Start/2 and End/4
                    //TODO Automate Quests need the Stage 0 flag set to 2/Start
                    //and the Last Stage set to 4/End
                    //TODO check relative to Complete Quest flag
                    if (BoolUpdateQUST)
                    {
                        Plugin pQUST = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                        foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    string sEdid = "";
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        int nQSDT = 0;
                                        if (sr.Name == "EDID")
                                        {
                                            sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                                            sEdid = sEdid.TrimEnd('\0');
                                        }

                                        if (sEdid.ToCharArray()[0] == 'c' &&
                                                sEdid.ToCharArray()[1] == 'o' &&
                                                sEdid.ToCharArray()[2] == 'd' &&
                                                sEdid.ToCharArray()[3] == '_')
                                        {

                                            if (sr.Name == "DNAM")
                                            {
                                                var b = sr.GetData();
                                                b[0] = Convert.ToByte(0x00);
                                                sr.SetData(b);
                                            }

                                            if (sr.Name == "QSDT")
                                            {
                                                var b = sr.GetData();
                                                b[0] = Convert.ToByte(0x01);
                                                sr.SetData(b);

                                                nQSDT++;
                                            }

                                            if (nQSDT > 1)
                                                Console.WriteLine();
                                        }
                                    }
                                }
                            }
                        }

                        /*foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    List<SubRecord> indx = new List<SubRecord>();
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "INDX")
                                            indx.Add(sr);
                                    }

                                    //for now only Start Stage
                                    if (indx.Count > 0 / *1* /)//skip 0/none, or 1 Stage
                                    {
                                        var bs = indx[0].GetData();
                                        bs[2] = Convert.ToByte(0x02);
                                        indx[0].SetData(bs);

                                        / *var be = indx[indx.Count - 1].GetData();
                                        be[2] = Convert.ToByte(0x04);
                                        indx[indx.Count - 1].SetData(be);* /
                                    }
                                }
                            }
                        }*/

                        /*foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "DNAM")
                                        {
                                            byte[] bDNAM = sr.GetData();

                                            //no start flags
                                            bDNAM[0] = Convert.ToByte(0x00);
                                            bDNAM[1] = Convert.ToByte(0x00);

                                            //side quest type
                                            bDNAM[8] = Convert.ToByte(0x08);

                                            sr.SetData(bDNAM);
                                        }

                                        if (sr.Name == "FLTR")
                                        {
                                            string sFLTR = "Hajduk Age\\Side Quest";
                                            byte[] plusNull = new byte[System.Text.Encoding.ASCII.GetBytes(sFLTR).Length + 1];
                                            Array.Copy(System.Text.Encoding.ASCII.GetBytes(sFLTR), plusNull, System.Text.Encoding.ASCII.GetBytes(sFLTR).Length);

                                            sr.SetData(plusNull);
                                        }
                                    }
                                }
                            }
                        }*/

                        /*foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    List<SubRecord> srs = new List<SubRecord>();

                                    bool bDone = false;
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (!bDone && sr.Name!="QSTA")//remove Target for now
                                            srs.Add(sr);

                                        if (sr.Name == "ANAM")
                                            bDone = true;
                                    }

                                    while ((r as Record).SubRecords.Count > 0)
                                        (r as Record).SubRecords.RemoveAt((r as Record).SubRecords.Count - 1);

                                    foreach (var sr in srs)
                                        (r as Record).SubRecords.Add(sr);
                                }
                            }
                        }*/

                        //set manually QUST as Start Enabled
                        /*foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    string sEdid = "";

                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "EDID")
                                        {
                                            sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                                            sEdid = sEdid.TrimEnd('\0');
                                        }

                                        if (sr.Name == "DNAM")
                                        {
                                            var b = sr.GetData();
                                            b[2] = Convert.ToByte(0x32);//priority 50

                                            if (sEdid.Contains("Quest_") || sEdid.Contains("_talked_to"))
                                            {
                                                b[0] = Convert.ToByte(0x11);//START Enabled
                                            }

                                            sr.SetData(b);
                                        }
                                    }
                                }
                            }
                        }*/

                        /*foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "DNAM")
                                        {
                                            var b = sr.GetData();
                                            b[0] = Convert.ToByte(0x11);//START Enabled
                                            b[2] = Convert.ToByte(0x32);//priority 50

                                            sr.SetData(b);
                                        }
                                    }
                                }
                            }
                        }*/

                        /*SubRecord i1 = null;
                        SubRecord q1 = null;
                        SubRecord i2 = null;
                        SubRecord q2 = null;

                        foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                bool bFound = false;

                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "INDX")
                                        {
                                            if (i1 == null)
                                                i1 = sr.Clone() as SubRecord;
                                            else if (i2 == null)
                                                i2 = sr.Clone() as SubRecord;
                                            else
                                            {

                                            }
                                        }

                                        if (sr.Name == "QSDT")
                                        {
                                            if (q1 == null)
                                                q1 = sr.Clone() as SubRecord;
                                            else if (q2 == null)
                                                q2 = sr.Clone() as SubRecord;
                                            else
                                            {

                                            }
                                        }

                                        if (i1 != null && i2 != null && q1 != null && q2 != null)
                                        {
                                            bFound = true;
                                            break;
                                        }
                                    }

                                    if (bFound)
                                        break;
                                }

                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    string sEdid = "";

                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "EDID")
                                        {
                                            sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                                            sEdid = sEdid.TrimEnd('\0');
                                            break;
                                        }
                                    }

                                    if (sEdid.Contains("Quest_"))
                                    {
                                        (r as Record).SubRecords.Insert(5, q2);
                                        (r as Record).SubRecords.Insert(5, i2);
                                        (r as Record).SubRecords.Insert(5, q1);
                                        (r as Record).SubRecords.Insert(5, i1);
                                    }
                                }
                            }
                        }*/

                        foreach (var g in pQUST.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    string sEdid = "";

                                    SubRecord srINDX = null;
                                    SubRecord srQSDT = null;
                                    SubRecord srCNAM = null;

                                    int nFirstINDX = 0;
                                    /*int nLessThan2560 = 0;
                                    int nIndex = -1;*/

                                    for (int i = 0; i < (r as Record).SubRecords.Count; i++)
                                    {
                                        var sr = (r as Record).SubRecords[i];

                                        if (sr.Name == "EDID")
                                        {
                                            sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                                            sEdid = sEdid.TrimEnd('\0');
                                        }

                                        if (sr.Name == "INDX")
                                        {
                                            if (srINDX == null)
                                            {
                                                srINDX = sr.Clone() as SubRecord;
                                                nFirstINDX = i;
                                            }
                                            else
                                            {
                                                /*byte b0 = sr.GetData()[0];
                                                byte b1 = sr.GetData()[1];
                                                byte[] b = new byte[2];
                                                b[0] = b0;
                                                b[1] = b1;
                                                int n = BitConverter.ToInt16(b, 0);

                                                if (n < 2560)
                                                {
                                                    if (n + 10 >= 2560)
                                                        Console.WriteLine();

                                                    nIndex = Math.Max(nIndex, n + 10);
                                                }

                                                if (n >= 2560)
                                                    nLessThan2560 = i - 1;*/
                                            }
                                        }

                                        if (sr.Name == "QSDT")
                                        {
                                            if (srQSDT == null)
                                                srQSDT = sr.Clone() as SubRecord;
                                            else
                                            {

                                            }
                                        }

                                        if (sr.Name == "CNAM")
                                        {
                                            if (srCNAM == null)
                                                srCNAM = sr.Clone() as SubRecord;
                                            else
                                            {

                                            }
                                        }

                                        /*if (srINDX != null && srQSDT != null)
                                            break;*/
                                    }

                                    if (sEdid.ToCharArray()[0] == 'c' &&
                                                sEdid.ToCharArray()[1] == 'o' &&
                                                sEdid.ToCharArray()[2] == 'd' &&
                                                sEdid.ToCharArray()[3] == '_')
                                    {
                                        //skip if codex entry
                                    }
                                    else if (srINDX != null && srQSDT != null &&
                                                 Convert.ToByte(0x01) == srQSDT.GetData()[0])
                                    {
                                        /*bool bHasCNAM = false;

                                        if (srCNAM != null)
                                            bHasCNAM = true;

                                        if (bHasCNAM &&
                                            (r as Record).SubRecords[nFirstINDX + 2].Name != "CNAM")
                                            Console.WriteLine();

                                        if (bHasCNAM)
                                        {
                                            byte[] bytes = BitConverter.GetBytes(2580);
                                            var indx = srINDX.Clone() as SubRecord;
                                            var bIndx = indx.GetData();
                                            bIndx[0] = bytes[0];
                                            bIndx[1] = bytes[1];
                                            indx.SetData(bIndx);
                                            var qsdt = srQSDT.Clone() as SubRecord;
                                            var cnam = srCNAM.Clone() as SubRecord;

                                            (r as Record).SubRecords.Insert(nLessThan2560, cnam);
                                            (r as Record).SubRecords.Insert(nLessThan2560, qsdt);
                                            (r as Record).SubRecords.Insert(nLessThan2560, indx);
                                        }
                                        else
                                        {
                                            byte[] bytes = BitConverter.GetBytes(nIndex);
                                            var indx = srINDX.Clone() as SubRecord;
                                            var qsdt = srQSDT.Clone() as SubRecord;

                                            (r as Record).SubRecords.Insert(nLessThan2560, qsdt);
                                            (r as Record).SubRecords.Insert(nLessThan2560, indx);
                                        }*/

                                        if ((r as Record).SubRecords[nFirstINDX].Name != "INDX")
                                            Console.WriteLine();

                                        if ((r as Record).SubRecords[nFirstINDX + 1].Name != "QSDT")
                                            Console.WriteLine();

                                        var bi = (r as Record).SubRecords[nFirstINDX].GetData();
                                        bi[0] = Convert.ToByte(0x01);
                                        bi[2] = Convert.ToByte(0x00);
                                        (r as Record).SubRecords[nFirstINDX].SetData(bi);

                                        var qsdt = srQSDT.Clone() as SubRecord;
                                        var bq = qsdt.GetData();
                                        bq[0] = Convert.ToByte(0x00);
                                        qsdt.SetData(bq);

                                        (r as Record).SubRecords.Insert(nFirstINDX, qsdt);
                                        (r as Record).SubRecords.Insert(nFirstINDX, srINDX.Clone() as SubRecord);

                                        Console.WriteLine();
                                    }
                                }
                            }
                        }

                        BoolUpdateQUST = false;

                        Console.WriteLine();
                    }

                    if (BoolUpdateDLBR)
                    {
                        Plugin pDLBR = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                        foreach (var g in pDLBR.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "DLBR")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "DNAM")
                                        {
                                            var b = sr.GetData();
                                            b[0] = Convert.ToByte(0x01);
                                            sr.SetData(b);
                                        }
                                    }
                                }
                            }
                        }

                        BoolUpdateDLBR = false;

                        Console.WriteLine();
                    }

                    if (BoolUpdateNPC_)
                    {
                        var pSource = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;
                        var pDest = PluginList.All.Records.OfType<BaseRecord>().ToList()[1] as Plugin;

                        int nSource = 0x0001C19E;
                        int nDest = 0x03010696;

                        Record rSource = null;
                        Record rDest = null;

                        foreach (var g in pSource.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "NPC_")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    if ((r as Record).FormID == nSource)
                                        rSource = (r as Record).Clone() as Record;
                                }
                            }
                        }

                        foreach (var g in pDest.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "NPC_")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    if ((r as Record).FormID == nDest)
                                        rDest = (r as Record);//NO CLONE
                                }
                            }
                        }

                        if (pSource == null || pDest == null)
                            Console.WriteLine();

                        List<SubRecord> srs = new List<SubRecord>();
                        foreach (var sr in rSource.SubRecords)
                        {
                            if (sr.Name != "VMAD" &&
                                sr.Name != "SHRT"/* &&
                                sr.Name != "PKID"*/)//skip VMAD and SHRT name and Packages for now
                                srs.Add(sr.Clone() as SubRecord);
                        }

                        foreach (var sr in srs)
                        {
                            if (sr.Name == "EDID" || sr.Name == "FULL" || sr.Name == "VTCK")
                            {
                                foreach (var srd in (rDest.Clone() as Record).SubRecords)
                                {
                                    if (srd.Name == sr.Name)
                                        sr.SetData(srd.GetData());
                                }
                            }
                        }

                        /*bool bVMAD = true;//if true, keep it
                        if (bVMAD)
                            srs.Insert(1, rDest.SubRecords[1].Clone() as SubRecord);*/

                        while (rDest.SubRecords.Count > 0)
                            rDest.SubRecords.RemoveAt(rDest.SubRecords.Count - 1);

                        foreach (var sr in srs)
                        {
                            rDest.SubRecords.Add(sr.Clone() as SubRecord);
                        }

                        //update Voice Type to Hajduk Unique
                        /*uint nMale = 0x0301370B;
                        uint nFemale = 0x0301370C;

                        string hexMale = nMale.ToString("X8");

                        byte[] arrMale = Enumerable.Range(0, hexMale.Length / 2).Select(x => Convert.ToByte(hexMale.Substring(x * 2, 2), 16)).ToArray();
                        arrMale = arrMale.Reverse().ToArray();

                        string hexFemale = nFemale.ToString("X8");

                        byte[] arrFemale = Enumerable.Range(0, hexFemale.Length / 2).Select(x => Convert.ToByte(hexFemale.Substring(x * 2, 2), 16)).ToArray();
                        arrFemale = arrFemale.Reverse().ToArray();

                        Plugin pNPC_ = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                        foreach (var g in pNPC_.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "NPC_")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\NPC_\",
                                      "*_" + (r as Record).FormID.ToString("X8") + "_*.xml",
                                      System.IO.SearchOption.AllDirectories);

                                    if (filesDAO.Length != 1)
                                        Console.WriteLine();

                                    var xd = XDocument.Load(filesDAO[0]);

                                    bool bMale = true;
                                    if (xd.Root.Descendants("ACBS").ToList().Count > 0)
                                    {
                                        if (xd.Root.Descendants("ACBS").ToList()[0].
                                            Descendants().ToList()[0].Value != "0")
                                            if (xd.Root.Descendants("ACBS").ToList()[0].
                                                Descendants().ToList()[0].
                                                Attribute("flags").Value.Contains("Female"))
                                                bMale = false;
                                    }

                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "VTCK")
                                        {
                                            if (bMale)
                                                sr.SetData(arrMale);
                                            else
                                                sr.SetData(arrFemale);
                                        }
                                    }
                                }
                            }
                        }*/

                        BoolUpdateNPC_ = false;
                    }

                    if (BoolUpdateVMAD)
                    {
                        Plugin pVMAD = PluginList.All.Records.OfType<BaseRecord>().ToList()[2] as Plugin;

                        foreach (var g in pVMAD.Records)
                        {
                            bool bFound = false;

                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    if ((r as Record).DescriptiveName.Contains("Something"))
                                    {
                                        foreach (var sr in (r as Record).SubRecords)
                                        {
                                            if (sr.Name == "VMAD")
                                            {
                                                GenericVMAD = sr.Clone() as SubRecord;
                                                bFound = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            if (bFound)
                                break;
                        }
                    }

                    if (BoolUpdateVMAD && GenericVMAD != null)
                    {
                        Plugin pVMAD = PluginList.All.Records.OfType<BaseRecord>().ToList()[1] as Plugin;

                        foreach (var g in pVMAD.Records)
                        {
                            if (g is GroupRecord && (g as GroupRecord).ContentsType == "QUST")
                            {
                                foreach (var r in (g as GroupRecord).Records)
                                {
                                    foreach (var sr in (r as Record).SubRecords)
                                    {
                                        if (sr.Name == "VMAD")
                                        {
                                            sr.SetData(GenericVMAD.GetData());
                                        }
                                    }
                                }
                            }
                        }
                    }


                    /*uint nMax = 0;
                    foreach (var g in _plugin.Records)
                    {
                        if (g is GroupRecord)
                        {
                            foreach (var r in (g as GroupRecord).Records)
                            {
                                if (r is GroupRecord)
                                {
                                    GroupInfo = r as GroupRecord;
                                }

                                nMax = Math.Max(nMax, (r as Record).FormID);
                            }
                        }
                    }*/

                    string xmlPath = Path.Combine(Options.Value.SettingsDirectory, @"RecordStructure.xml");
                    var xs = new XmlSerializer(typeof(Records));

                    using (FileStream fs = File.OpenRead(xmlPath))
                    {
                        var baseRec = xs.Deserialize(fs) as Records;
                        _plugin.groups = baseRec.Items.OfType<Domain.Data.RecordStructure.Group>().ToDictionary(x => x.id, StringComparer.InvariantCultureIgnoreCase);
                    }

                    bool bWriteFormID = false;
                    if (!bWriteFormID)
                    {
                        string[] files = Directory.GetFiles(@"d:\Work\C#\Snip\dev-nogardeht\!Work\CSV\",
                          "*.csv",
                          SearchOption.TopDirectoryOnly);

                        foreach (string _file in files)
                        {
                            using (var _fileReader = File.OpenText(_file))
                            {
                                string _line;
                                while ((_line = _fileReader.ReadLine()) != null)
                                {
                                    var aLine = _line.Split(',');

                                    if (!_plugin.dForms.ContainsKey(aLine[0]))
                                    {
                                        _plugin.dForms.Add(aLine[0], new ValuePair { sName = aLine[1], sGroup = aLine[2], uFormID = Convert.ToUInt32(aLine[3]) });
                                    }
                                    else
                                    {
                                        _plugin.dForms[aLine[0]] = new ValuePair { sName = aLine[1], sGroup = aLine[2], uFormID = Convert.ToUInt32(aLine[3]) };
                                    }
                                }

                                _fileReader.Close();
                            }
                        }
                    }
                    else //write
                    {
                        var queue = new Queue<GroupRecord>();

                        foreach (var _r in _plugin.Records)
                        {
                            if (_r is GroupRecord)
                            {
                                queue.Enqueue(_r as GroupRecord);
                            }
                        }

                        while (queue.Count > 0)
                        {
                            // Take the next node from the front of the queue
                            var node = queue.Dequeue();

                            // Process the node 'node'
                            /*if (match(node))
                                return node;*/

                            // Add the node’s children to the back of the queue
                            foreach (var child in node.Records)
                            {
                                if (child is GroupRecord)
                                    queue.Enqueue(child as GroupRecord);
                                else if (child is Record)
                                {
                                    string _group = ((child as Record).Parent as GroupRecord).ContentsType;
                                    string _formID = ConvertFormIdToString((child as Record).FormID);

                                    string _name = "";

                                    if (_group.Length == 0)
                                    {
                                        if ((child as Record).DescriptiveName.Contains("CELL"))
                                        {
                                            _group = "CELL";
                                        }
                                        else if ((child as Record).DescriptiveName.Contains("REFR"))
                                        {
                                            _group = "REFR";
                                        }
                                    }

                                    if (!(child as Record).DescriptiveName.Contains(' '))
                                        _name = (child as Record).DescriptiveName;
                                    else
                                    {
                                        char bracket = '\t';

                                        if ((child as Record).DescriptiveName.Contains(bracket))
                                        {
                                            if ((child as Record).DescriptiveName.Split(bracket)[1].Length > 0)
                                                _name = (child as Record).DescriptiveName.Split(bracket)[1].Split(' ')[1].Replace("(", "").Replace(")", "");
                                            else _name = "CELL_Temporary";
                                        }
                                        else
                                            _name = (child as Record).DescriptiveName.Split(' ')[1].Replace("(", "").Replace(")", "");
                                    }

                                    if (_name.Length == 4 && _group.Length == 0)
                                        _group = _name;

                                    if (!_plugin.dForms.ContainsKey(_formID))
                                    {
                                        _plugin.dForms.Add(_formID, new ValuePair { sName = _name, sGroup = _group, uFormID = (child as Record).FormID });
                                    }
                                    else
                                    {
                                        _plugin.dForms[_formID] = new ValuePair { sName = _name, sGroup = _group, uFormID = (child as Record).FormID };
                                    }
                                }
                            }
                        }

                        /*foreach (var _r in _plugin.Records)
                        {
                            if (_r is GroupRecord)
                            {
                                foreach (var _rr in (_r as GroupRecord).Records)
                                {
                                    if (_rr is Record)
                                    {
                                        string _group = ((_rr as Record).Parent as GroupRecord).ContentsType;//.DescriptiveName.Split(' ')[0];
                                        string _formID = ConvertFormIdToString((_rr as Record).FormID);

                                        string _name = "";
                                        if (!(_rr as Record).DescriptiveName.Contains(' '))
                                            _name = (_rr as Record).DescriptiveName;
                                        else _name = (_rr as Record).DescriptiveName.Split(' ')[1].Replace("(", "").Replace(")", "");

                                        if (!_plugin.dForms.ContainsKey(_formID))
                                        {
                                            _plugin.dForms.Add(_formID, new ValuePair { sName = _name, sGroup = _group, uFormID = (_rr as Record).FormID });
                                        }
                                        else
                                        {
                                            _plugin.dForms[_formID] = new ValuePair { sName = _name, sGroup = _group, uFormID = (_rr as Record).FormID };
                                        }
                                    }
                                }
                            }
                        }*/

                        var listString = new List<string>();
                        foreach (var a in _plugin.dForms)
                        {
                            string l = a.Key + "," + a.Value.sName + "," + a.Value.sGroup + "," + a.Value.uFormID.ToString();
                            listString.Add(l);
                        }

                        string[] nLines = listString.ToArray();
                        File.WriteAllLines(_plugin.Name + ".csv", nLines);
                    }

                    //upload string DAO
                    var _fileReaderDAO = File.OpenText(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\DAOTlk.txt");
                    string _lineDAO;
                    int nIDDao = int.MaxValue;
                    string sText = string.Empty;

                    while ((_lineDAO = _fileReaderDAO.ReadLine()) != null)
                    {
                        string[] aLine = _lineDAO.Split('|');

                        if (aLine[0] == "I_D")
                        {
                            if (nIDDao != int.MaxValue)
                                Console.WriteLine();

                            nIDDao = Convert.ToInt32(aLine[1]);
                        }
                        else if (aLine[0] == "T_E_X_T")
                        {
                            if (nIDDao == int.MaxValue)
                                Console.WriteLine();

                            sText = aLine[1];

                            if (!_plugin.tlkDao.ContainsKey(nIDDao))
                                _plugin.tlkDao.Add(nIDDao, sText);
                            else
                                Console.WriteLine();

                            sText = string.Empty;
                            nIDDao = int.MaxValue;
                        }
                        else
                            Console.WriteLine();
                    }

                    _fileReaderDAO.Close();

                    //upload strings TESV
                    if (BoolExport)
                        _plugin.docStringsSkyrim = XDocument.Load(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Z\Skyrim_english_english.xml");

                    if (BoolMove)
                        _plugin.docStringsSkyrim = XDocument.Load(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english.xml");

                    var dString = _plugin.docStringsSkyrim.Root.Descendants("String");

                    foreach (var s in dString)
                    {
                        int nID = int.Parse(s.Attribute("sID").Value.ToString(), System.Globalization.NumberStyles.HexNumber);
                        if (!_plugin.tlk.ContainsKey(nID))
                        {
                            _plugin.tlk.Add(nID, s.Descendants("Source").ToList()[0].Value.ToString());
                        }
                    }

                    //again the new one
                    if (BoolExport)
                    {
                        _plugin.docStringsSkyrim = XDocument.Load(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english.xml");

                        dString = _plugin.docStringsSkyrim.Root.Descendants("String");

                        foreach (var s in dString)
                        {
                            int nID = int.Parse(s.Attribute("sID").Value.ToString(), System.Globalization.NumberStyles.HexNumber);
                            if (!_plugin.tlk.ContainsKey(nID))
                            {
                                _plugin.tlk.Add(nID, s.Descendants("Source").ToList()[0].Value.ToString());
                            }
                        }
                    }

                    if (BoolUpdateCELL)
                    {
                        //remove ACHRs
                        var pSource = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                        foreach (var g in pSource.Records)
                        {
                            bool bFound = false;
                            if (!bFound &&
                                g is GroupRecord &&
                                (g as GroupRecord).DescriptiveName.Contains("CELL"))
                            {
                                bFound = true;

                                foreach (var bb in (g as GroupRecord).Records)//block
                                {
                                    foreach (var sb in (bb as GroupRecord).Records)//sub-block
                                    {
                                        foreach (var sbb in (sb as GroupRecord).Records)
                                        {
                                            if (sbb is GroupRecord)//skip CELL Record
                                            {
                                                foreach (var pt in (sbb as GroupRecord).Records)//perm/temp
                                                {
                                                    List<BaseRecord> srs = new List<BaseRecord>();

                                                    if (!(pt is GroupRecord))
                                                        Console.WriteLine();

                                                    foreach (var sr in (pt as GroupRecord).Records)
                                                    {
                                                        if (!(sr as Record).DescriptiveName.Contains("ACHR"))
                                                            srs.Add(sr as Record);
                                                        else
                                                            Console.WriteLine();
                                                    }

                                                    while ((pt as GroupRecord).Records.Count > 0)
                                                        (pt as GroupRecord).Records.RemoveAt((pt as GroupRecord).Records.Count - 1);

                                                    foreach (var sr in srs)
                                                        (pt as GroupRecord).Records.Add(sr as Record);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //copy CELLs 
                        /*var pSource = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;
                                                var pDest = PluginList.All.Records.OfType<BaseRecord>().ToList()[1] as Plugin;

                                                pDest.Records.RemoveAt(pDest.Records.Count - 1);

                                                foreach (var g in pSource.Records)
                                                {
                                                    bool bFound = false;
                                                    if (!bFound &&
                                                        g is GroupRecord &&
                                                        (g as GroupRecord).DescriptiveName.Contains("CELL"))
                                                    {
                                                        bFound = true;
                                                        BaseRecord[] record = new BaseRecord[1];
                                                        record[0] = g as GroupRecord;
                                                        object[] nodes = new object[2];
                                                        nodes[0] = record;
                                                        nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                                                        int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
                                                    }
                                                }

                                                foreach (var g in pDest.Records)
                                                {
                                                    bool bFound = false;
                                                    if (!bFound &&
                                                        g is GroupRecord &&
                                                        (g as GroupRecord).DescriptiveName.Contains("CELL"))
                                                    {
                                                        bFound = true;

                                                        foreach (var bb in (g as GroupRecord).Records)//block
                                                        {
                                                            foreach (var sb in (bb as GroupRecord).Records)//sub-block
                                                            {
                                                                foreach (var sbb in (sb as GroupRecord).Records)
                                                                {
                                                                    if (sbb is GroupRecord)//skip CELL Record
                                                                    {
                                                                        foreach (var pt in (sbb as GroupRecord).Records)//perm/temp
                                                                        {
                                                                            if (!(pt is GroupRecord))
                                                                                Console.WriteLine();

                                                                            while ((pt as GroupRecord).Records.Count > 0)
                                                                                (pt as GroupRecord).Records.RemoveAt((pt as GroupRecord).Records.Count - 1);
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }*/

                        BoolUpdateCELL = false;
                    }

                    /*var csv = new StringBuilder();

                    foreach (var d in strings)
                    {
                        var newLine = Convert.ToString(d);
                        csv.AppendLine(newLine);
                    }

                    File.WriteAllText("strings.csv", csv.ToString());*/
                }
                if (rec is GroupRecord)
                {
                    if (BoolUpdateACHR)
                    {
                        if (ACharacter == null || ArrData == null)
                            Console.WriteLine();

                        Record achr = ACharacter.Clone() as Record;

                        string sFormID = "03010696";
                        //uint nFormID = uint.Parse(sFormID, System.Globalization.NumberStyles.HexNumber);

                        CounterFormID = Convert.ToUInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[0]);
                        CounterTalk = Convert.ToInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[1]);
                        achr.FormID = CounterFormID;
                        CounterFormID++;

                        string[] lines = new string[1];
                        lines[0] = CounterFormID.ToString() + ',' + CounterTalk.ToString();

                        using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
                        {
                            newTask.WriteLine(lines[0].ToString());
                        }

                        string[] files = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\NPC_\",
                            "NPC__" + sFormID + "*.xml", SearchOption.TopDirectoryOnly);

                        if (files.Length != 1)
                            Console.WriteLine();

                        XDocument xdDoc = XDocument.Load(files[0]);
                        string sName = xdDoc.Root.Descendants("EDID").ToList()[0].Descendants("Name").ToList()[0].Value;

                        string sEdid = sName + "REF";
                        byte[] plusNull = new byte[System.Text.Encoding.ASCII.GetBytes(sEdid).Length + 1];
                        Array.Copy(System.Text.Encoding.ASCII.GetBytes(sEdid), plusNull, System.Text.Encoding.ASCII.GetBytes(sEdid).Length);

                        byte[] arrNameFull = Enumerable.Range(0, sFormID.Length / 2).Select(x => Convert.ToByte(sFormID.Substring(x * 2, 2), 16)).ToArray();
                        arrNameFull = arrNameFull.Reverse().ToArray();

                        achr.SubRecords[0].SetData(plusNull);//EDID
                        achr.SubRecords[1].SetData(arrNameFull);//Name FormID
                        achr.SubRecords[achr.SubRecords.Count - 1].SetData(ArrData);//Loc/Rot

                        (rec as GroupRecord).Records.Add(achr);

                        BoolUpdateACHR = false;
                    }

                    if (BoolCELL)
                    {
                        var c = rec.Clone();

                        while (c.Records.Count > 0)
                            c.Records.RemoveAt(c.Records.Count - 1);

                        /*if (Cells.Count > 0)
                            Cells[Cells.Count - 1].Records.Add(c);*/

                        Cells.Add(c);
                    }

                    if (BoolExport)
                    {
                        if ((rec as GroupRecord).ContentsType == "DIAL")
                        {
                            foreach (var _rec in rec.Records)
                            {
                                if (_rec is Record)
                                {
                                    if ((_rec as Record).Name != "DIAL")
                                        Console.WriteLine();

                                    (PluginTree.TopRecord as Plugin).lElements = new List<Element>();

                                    (PluginTree.TopRecord as Plugin).rRec = _rec as Record;

                                    (PluginTree.TopRecord as Plugin).sCurrentFormID = (_rec as Record).FormID.ToString("X8");
                                    (PluginTree.TopRecord as Plugin).sDialogOwner = "DIAL_" + (PluginTree.TopRecord as Plugin).sCurrentFormID;

                                    var _rb = new RTFBuilder(RTFFont.Arial, 16, defLang.lcid, defLang.charset);
                                    (_rec as Record).GetFormattedHeader(_rb);
                                    (_rec as Record).GetFormattedData(_rb);

                                    ConvertToXml((PluginTree.TopRecord as Plugin).lElements);
                                }
                                else if (_rec is GroupRecord)
                                {
                                    foreach (var _rrec in (_rec as GroupRecord).Records)
                                    {
                                        if (_rrec is Record)
                                        {
                                            if ((_rrec as Record).Name != "INFO")
                                                Console.WriteLine();

                                            (PluginTree.TopRecord as Plugin).lElements = new List<Element>();

                                            (PluginTree.TopRecord as Plugin).rRec = _rrec as Record;

                                            (PluginTree.TopRecord as Plugin).sCurrentFormID = (_rrec as Record).FormID.ToString("X8");

                                            var _rrb = new RTFBuilder(RTFFont.Arial, 16, defLang.lcid, defLang.charset);
                                            (_rrec as Record).GetFormattedHeader(_rrb);
                                            (_rrec as Record).GetFormattedData(_rrb);

                                            ConvertToXml((PluginTree.TopRecord as Plugin).lElements);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (var _rec in rec.Records)
                            {
                                (PluginTree.TopRecord as Plugin).lElements = new List<Element>();

                                (PluginTree.TopRecord as Plugin).rRec = _rec as Record;

                                (PluginTree.TopRecord as Plugin).sCurrentFormID = (_rec as Record).FormID.ToString("X8");

                                var _rb = new RTFBuilder(RTFFont.Arial, 16, defLang.lcid, defLang.charset);
                                (_rec as Record).GetFormattedHeader(_rb);
                                (_rec as Record).GetFormattedData(_rb);

                                ConvertToXml((PluginTree.TopRecord as Plugin).lElements);
                            }
                        }
                    }
                }
                if (rec is Record)
                {
                    if (BoolFToM)//female to male
                    {
                        foreach (var sr in (rec as Record).SubRecords)
                        {
                            if (sr.Name == "ACBS")
                            {
                                var b = sr.GetData();
                                b[0] = 0x18;
                                sr.SetData(b);
                            }
                        }
                    }

                    if (BoolCELL)
                    {
                        //STEP 1
                        Cells.Add(rec.Clone());

                        //step 2
                        //update REF
                        /*Cells.Add(rec);
                        if (Cells.Count == 2)
                        {
                            byte[] b = null;
                            foreach (var sr in (Cells[0] as Record).SubRecords)
                            {
                                if (sr.Name == "DATA")
                                    b = sr.GetData();
                            }

                            List<string> keeps = new List<string>();
                            keeps.Add("EDID");
                            keeps.Add("NAME");
                            keeps.Add("DATA");

                            List<SubRecord> srkeeps = new List<SubRecord>();

                            foreach (var sr in (Cells[1] as Record).SubRecords)
                            {
                                if (keeps.Contains(sr.Name))
                                    srkeeps.Add(sr.Clone() as SubRecord);
                            }

                            while ((Cells[1] as Record).SubRecords.Count > 0)
                                (Cells[1] as Record).SubRecords.RemoveAt((Cells[1] as Record).SubRecords.Count - 1);

                            foreach (var sr in srkeeps)
                            {
                                if (sr.Name == "EDID")
                                {
                                    string sEdid = "arl100cr_despmerchREF";

                                    byte[] plusNull = new byte[System.Text.Encoding.ASCII.GetBytes(sEdid).Length + 1];
                                    Array.Copy(System.Text.Encoding.ASCII.GetBytes(sEdid), plusNull, System.Text.Encoding.ASCII.GetBytes(sEdid).Length);

                                    sr.SetData(plusNull);

                                    (Cells[1] as Record).SubRecords.Add(sr.Clone() as SubRecord);
                                }

                                if (sr.Name == "NAME")
                                {
                                    string hexName = 0x030104FE.ToString("X8");

                                    byte[] arrName = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
                                    arrName = arrName.Reverse().ToArray();

                                    sr.SetData(arrName);

                                    (Cells[1] as Record).SubRecords.Add(sr.Clone() as SubRecord);
                                }

                                if (sr.Name == "DATA")
                                {
                                    sr.SetData(b);

                                    (Cells[1] as Record).SubRecords.Add(sr.Clone() as SubRecord);
                                }
                            }

                            (Cells[1] as Record).FormID = 50411277;

                            BoolCELL = false;
                        }*/
                    }

                    if (BoolUpdateACHR)
                    {
                        if (ArrData == null)
                        {
                            foreach (var sr in (rec as Record).SubRecords)
                            {
                                if (sr.Name == "DATA")
                                    ArrData = sr.GetData();
                            }
                        }
                        else
                            ACharacter = rec.Clone() as Record;

                        /*List<SubRecord> srs = new List<SubRecord>();
                        foreach (var sr in (rec as Record).SubRecords)
                        {
                            if (sr.Name == "EDID" ||
                                sr.Name == "NAME" ||
                                sr.Name == "DATA")
                                srs.Add(sr.Clone() as SubRecord);
                        }

                        string sFormID = "030106AB";
                        uint nFormID = uint.Parse(sFormID, System.Globalization.NumberStyles.HexNumber);
                        (rec as Record).FormID = nFormID;

                        while ((rec as Record).SubRecords.Count > 0)
                            (rec as Record).SubRecords.RemoveAt((rec as Record).SubRecords.Count - 1);

                        foreach (var sr in srs)
                        {
                            (rec as Record).SubRecords.Add(sr.Clone() as SubRecord);
                        }*/
                    }

                    if (BoolUpdateNPC_)
                    {
                        List<SubRecord> srs = new List<SubRecord>();
                        foreach (var sr in (rec as Record).SubRecords)
                        {
                            if (sr.Name == "PKID")
                            {

                            }
                            else
                            {
                                if (sr.Name == "SNAM")
                                {
                                    if (sr.GetData()[0] != 0x8D)
                                        srs.Add(sr.Clone() as SubRecord);
                                }
                                else
                                    srs.Add(sr.Clone() as SubRecord);
                            }
                        }

                        while ((rec as Record).SubRecords.Count > 0)
                            (rec as Record).SubRecords.RemoveAt((rec as Record).SubRecords.Count - 1);

                        foreach (var sr in srs)
                            (rec as Record).SubRecords.Add(sr.Clone() as SubRecord);

                        BoolUpdateNPC_ = false;

                        /*if (ACharacter == null)
                        {
                            //ACharacter = rec.Clone() as Record;

                            / *uint nSource = 0x030109D5;//Leliana
                            var pSource = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;

                            foreach (var g in pSource.Records)
                            {
                                if (g is GroupRecord && (g as GroupRecord).ContentsType == "NPC_")
                                {
                                    foreach (var r in (g as GroupRecord).Records)
                                    {
                                        if ((r as Record).FormID == nSource)
                                            ACharacter = (r as Record).Clone() as Record;
                                    }
                                }
                            }

                            if (ACharacter == null)
                                Console.WriteLine();* /
                        }*/
                        /*else
                        {
                            List<string> records = new List<string>();
                            records.Add("EDID");
                            records.Add("FULL");
                            records.Add("SHRT");
                            records.Add("VTCK");

                            Dictionary<string, SubRecord> arrDict = new Dictionary<string, SubRecord>();
                            foreach (var sr in ACharacter.SubRecords)
                            {
                                if (records.Contains(sr.Name))
                                    arrDict.Add(sr.Name, sr.Clone() as SubRecord);
                            }

                            List<SubRecord> srs = new List<SubRecord>();
                            foreach (var sr in (rec as Record).SubRecords)
                            {
                                if (sr.Name == "VMAD" || sr.Name == "PKID")
                                {
                                    //skip
                                }
                                else if (records.Contains(sr.Name))
                                    srs.Add(arrDict[sr.Name].Clone() as SubRecord);
                                else
                                    srs.Add(sr.Clone() as SubRecord);
                            }

                            while ((rec as Record).SubRecords.Count > 0)
                                (rec as Record).SubRecords.RemoveAt((rec as Record).SubRecords.Count - 1);

                            foreach (var sr in srs)
                                (rec as Record).SubRecords.Add(sr.Clone() as SubRecord);

                            (rec as Record).FormID = ACharacter.FormID + 0x2000000;
                            (rec as Record).Flags1 = ACharacter.Flags1;
                            (rec as Record).Flags2 = ACharacter.Flags2;
                            (rec as Record).Flags3 = ACharacter.Flags3;
                        }*/

                        /*else
                        {
                            int nInt = 0x00971D;//Greagoire - int to String

                            //copy record, update Name
                            List<SubRecord> srs = new List<SubRecord>();
                            foreach (var sr in ACharacter.SubRecords)
                                srs.Add(sr.Clone() as SubRecord);

                            //save EDID data
                            byte[] bEdid = null;

                            while ((rec as Record).SubRecords.Count > 0)
                            {
                                if ((rec as Record).SubRecords[(rec as Record).SubRecords.Count - 1].Name == "EDID")
                                    bEdid = (rec as Record).SubRecords[(rec as Record).SubRecords.Count - 1].GetData();

                                (rec as Record).SubRecords.RemoveAt((rec as Record).SubRecords.Count - 1);
                            }

                            foreach (var sr in srs)
                            {
                                if (sr.Name == "EDID")
                                    sr.SetData(bEdid);

                                if (sr.Name == "FULL")
                                {
                                    string hexName = nInt.ToString("X8");

                                    byte[] arrNameFull = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
                                    arrNameFull = arrNameFull.Reverse().ToArray();

                                    sr.SetData(arrNameFull);
                                }

                                (rec as Record).SubRecords.Add(sr.Clone() as SubRecord);
                            }

                            (rec as Record).Flags1 = ACharacter.Flags1;
                            (rec as Record).Flags2 = ACharacter.Flags2;
                            (rec as Record).Flags3 = ACharacter.Flags3;
                        }*/

                        //if (ACharacter.FormID + 0x2000000 == (rec as Record).FormID)
                    }

                    (PluginTree.TopRecord as Plugin).lElements = new List<Element>();

                    (PluginTree.TopRecord as Plugin).rRec = rec as Record;

                    (PluginTree.TopRecord as Plugin).sCurrentFormID = (rec as Record).FormID.ToString("X8");

                    if ((rec as Record).Name == "DIAL")
                        (PluginTree.TopRecord as Plugin).sDialogOwner = "DIAL_" + (rec as Record).FormID.ToString("X8");

                    var _rb = new RTFBuilder(RTFFont.Arial, 16, defLang.lcid, defLang.charset);
                    (rec as Record).GetFormattedHeader(_rb);
                    (rec as Record).GetFormattedData(_rb);

                    if (!BoolMove)
                        ConvertToXml((PluginTree.TopRecord as Plugin).lElements);
                }
            }
        }

        private string ConvertFormIdToString(uint _id)
        {
            return string.Format("{0:X8}", _id);
        }

        private void GetOptions(Element _e, ref XElement nElem)
        {
            string strDesc = "";

            int nn = 0;
            bool bInt = int.TryParse(_e.Value.ToString(), out nn);

            int intVal = int.MinValue;

            if (!bInt && Convert.ToUInt32(_e.Value) == uint.MaxValue)
                intVal = -1;
            else intVal = Convert.ToInt32(_e.Value);

            for (int k = 0; k < _e.Structure.options.Length; k += 2)
            {
                if (int.TryParse(_e.Structure.options[k + 1], out int intValOption) && intVal == intValOption)
                {
                    strDesc = _e.Structure.options[k];
                }
            }

            if (strDesc != "")
                nElem.Add(new XAttribute("options", strDesc));
        }

        private void GetFlags(Element _e, ref XElement nElem)
        {
            uint intVal = Convert.ToUInt32(_e.Value);
            var sTemp = new StringBuilder();
            for (int k = 0; k < _e.Structure.flags.Length; k++)
            {
                if ((intVal & (1 << k)) != 0)
                {
                    if (sTemp.Length > 0)
                    {
                        sTemp.Append("|");
                    }

                    sTemp.Append(_e.Structure.flags[k]);
                }
            }

            string strDesc = sTemp.ToString();

            if (strDesc != "")
            {
                nElem.Add(new XAttribute("flags", strDesc));
                nElem.Value = string.Format("{0:X8}", Convert.ToUInt32(nElem.Value));
            }
        }

        private Domain.Data.RecordStructure.Group GetSectionGroup(string sSection)
        {
            Domain.Data.RecordStructure.Group gg = null;

            foreach (var g in (PluginTree.TopRecord as Plugin).groups)
            {
                bool bFound = false;

                foreach (var it in g.Value.Items)
                {
                    if (it is Subrecord)
                    {
                        if ((it as Subrecord).name == sSection)
                        {
                            bFound = true;
                            gg = g.Value as Domain.Data.RecordStructure.Group;
                            break;
                        }

                    }
                }

                if (bFound)
                    break;
            }

            return gg;
        }

        private Dictionary<string, string> GetAttached(string sGroupName)
        {
            var ggg = (PluginTree.TopRecord as Plugin).groups;
            Dictionary<string, string> attached = new Dictionary<string, string>();

            var queue = new Queue<Domain.Data.RecordStructure.Group>();

            foreach (var _r in ggg.Values)
            {
                if (_r is Domain.Data.RecordStructure.Group)
                {
                    queue.Enqueue(_r as Domain.Data.RecordStructure.Group);
                }
            }

            while (queue.Count > 0)
            {
                // Take the next node from the front of the queue
                var node = queue.Dequeue();

                // Add the node’s children to the back of the queue
                foreach (var child in node.Items)
                {
                    if (child is Domain.Data.RecordStructure.Group)
                        queue.Enqueue(child as Domain.Data.RecordStructure.Group);
                    else if (child is Subrecord)
                    {
                        if (node.attachTo != "")
                        {
                            (child as Subrecord).attachTo = node.attachTo;
                            if (attached.ContainsKey((child as Subrecord).name))
                                attached[(child as Subrecord).name] = "multiple";
                            else
                                attached.Add((child as Subrecord).name, (child as Subrecord).attachTo);
                        }
                    }
                }
            }

            //update dictionary based on group type
            //UGLY ugly
            if (sGroupName == "NPC_")
            {
                if (attached.ContainsKey("SNAM"))
                    attached["SNAM"] = "Faction";
                attached.Remove("VTCK");
                attached.Remove("COCT");
                attached.Remove("CNAM");
                attached.Remove("QNAM");
                attached.Remove("ECOR");
                attached.Remove("SPOR");
                attached.Remove("KSIZ");
            }
            else if (sGroupName == "DLVW" ||
                sGroupName == "DLBR")
            {
                attached.Remove("QNAM");
                if (attached.ContainsKey("SNAM"))
                    attached["SNAM"] = "StartingTopic";
            }
            else if (sGroupName == "DIAL")
            {
                attached.Remove("QNAM");
                attached.Remove("PNAM");
                attached.Remove("SNAM");
            }
            else if (sGroupName == "INFO")
            {
                attached.Remove("CNAM");
                if (attached.ContainsKey("QNAM"))
                    attached["QNAM"] = "Unknown";
                if (attached.ContainsKey("SCHR"))
                    attached["SCHR"] = "Unknown";
            }

            return attached;
        }

        private string GetArrayName(string sAttachTo)
        {
            string sArrayName = "";

            if (sAttachTo != "")
            {
                if (sAttachTo.ToCharArray()[sAttachTo.Length - 1] == (char)'s' ||
                     sAttachTo.ToCharArray()[sAttachTo.Length - 1] == (char)'h')
                {
                    sArrayName = sAttachTo + "es";
                }
                else if (sAttachTo.ToCharArray()[sAttachTo.Length - 1] == (char)'y')
                {
                    sArrayName = sAttachTo.TrimEnd('y');
                    sArrayName = sArrayName + "ies";
                }
                else
                {
                    sArrayName = sAttachTo + "s";
                }
            }

            return sArrayName;
        }

        private string GetAlias(XDocument xDoc, uint nNotFound)
        {
            var aliases = xDoc.Root.Descendants("Alias").ToList();
            Dictionary<uint, string> dAliases = new Dictionary<uint, string>();
            if (aliases.Count > 0)
            {
                foreach (XElement a in aliases)
                {
                    var alst = a.Descendants("ALST").ToList();
                    var alls = a.Descendants("ALLS").ToList();
                    var alid = a.Descendants("ALID").ToList()[0];

                    if (alst.Count == 0 && alls.Count == 0)
                        Console.WriteLine();
                    if ((alst.Count > 1 || alls.Count > 1))
                        Console.WriteLine();

                    if (alst.Count == 1)
                        dAliases.Add(Convert.ToUInt32(alst.Descendants().ToList()[0].Value),
                            alid.Descendants().ToList()[0].Value.ToString());
                    else if (alls.Count == 1)
                        dAliases.Add(Convert.ToUInt32(alls.Descendants().ToList()[0].Value),
                            alid.Descendants().ToList()[0].Value.ToString());
                    else
                        Console.WriteLine();
                }

                if (dAliases.ContainsKey(nNotFound))
                    return dAliases[nNotFound];
                else
                    Console.WriteLine();
            }
            else
                Console.WriteLine();

            return "";
        }

        private void ConvertToXml(List<Element> lElements)
        {
            if (BoolMove)
                return;

            int nArrayAlwaysOne = 0;
            int nArrayCount = 0;
            int nArrayIterator = 0;
            int nArrayType = 0;
            int scriptCount = 0;
            int nModulo = 0;
            bool bArrayDone = false;
            int propertyCount = 0;
            int propertyIterator = 0;
            int nFragmentCount = 0;
            int aliasCount = 0;
            char[] cStr4 = new char[4];
            List<string> arrays = new List<string>();

            GroupRecord _groupRecord = PluginTree.SelectedRecord is Record ? PluginTree.SelectedRecord.Parent as GroupRecord : PluginTree.SelectedRecord as GroupRecord;

            XElement Root = null;

            if ((PluginTree.TopRecord as Plugin).rRec.Name == "INFO")
                Root = new XElement("INFO");
            else
                Root = new XElement(_groupRecord.ContentsType.ToString() != "" ? _groupRecord.ContentsType.ToString() : _groupRecord.DescriptiveName.ToString());

            if ((PluginTree.TopRecord as Plugin).rRec.Name.Length != 4)
                Console.WriteLine();

            string sRootName = "";
            if ((PluginTree.TopRecord as Plugin).rRec.Name == "INFO")
                sRootName = "INFO";
            else sRootName = _groupRecord.ContentsType.ToString() != "" ? _groupRecord.ContentsType.ToString() : _groupRecord.DescriptiveName.ToString();

            XDocument xdDocument = new XDocument(new XDeclaration("1.0", "utf-16", "yes"), Root);
            Root.Add(new XAttribute("type", _groupRecord.Name.ToString()));

            string sFormID = (PluginTree.TopRecord as Plugin).sCurrentFormID;
            Root.Add(new XAttribute("formID", sFormID));

            string nf = "";
            if (sRootName == "INFO")
                nf = (PluginTree.TopRecord as Plugin).sDialogOwner + "_" + sRootName + "_" + sFormID + ".xml";
            else if (lElements[0].Structure.name != "EDID")
                nf = sRootName + "_" + sFormID + ".xml";
            else
                nf = sRootName + "_" + sFormID + "_" + lElements[1].Value.ToString().Replace('%', '_') + ".xml";

            string sFunctionName = "";
            string sSection = "";
            XElement currentGroup = Root;
            XElement previousGroup = null;
            Element ePreviousGroup = null;
            XElement xPreviousGroup = null;
            List<XElement> lProcessed = new List<XElement>();

            (PluginTree.TopRecord as Plugin).sGroupName = sRootName;
            Dictionary<string, string> attached = GetAttached((PluginTree.TopRecord as Plugin).sGroupName);

            for (int i = 0; i < lElements.Count; i++)
            {
                (PluginTree.TopRecord as Plugin).nIndex = i;

                Element _e = lElements[i];

                bool hasOptions = _e.Structure.options != null && _e.Structure.options.Length > 0;
                bool hasFlags = _e.Structure.flags != null && _e.Structure.flags.Length > 1;

                string _name = _e.Structure.name.Replace(" ", "_");
                _name = _name.Replace("#", "");
                _name = _name.Replace("(", "");
                _name = _name.Replace(")", "");
                _name = _name.Replace("/", "-");
                _name = _name.Replace(@"\", "-");
                _name = _name.Replace("%", "_percent");
                _name = _name.Replace(".", "_dot_");
                _name = _name.Replace("+", "_plus_");

                if (_name == "") _name = "WHATTHEWHAT";

                if (_e.Type == ElementValueType.Group)
                {
                    sFunctionName = "";//reset

                    sSection = _name;

                    string sCurrentElement = "";
                    string sArrayName = "";
                    string sAttachTo = "";

                    if (attached.ContainsKey(sSection))
                    {
                        if (attached[sSection] != "")/* &&
                            attached[sSection] != "multiple")*/
                        {
                            sCurrentElement = attached[sSection];
                        }

                        if (attached[sSection] == "multiple")
                        {
                            sArrayName = GetArrayName(sCurrentElement);
                            sAttachTo = sCurrentElement;
                        }
                        else
                        {
                            sArrayName = GetArrayName(attached[sSection]);
                            sAttachTo = attached[sSection];
                        }
                    }

                    if (ePreviousGroup != null && xPreviousGroup != null)
                    {
                        if (ePreviousGroup.Structure.name != xPreviousGroup.Name)
                            Console.WriteLine();
                    }

                    ENestingType eType = ENestingType.NoNesting;
                    string sParent = "";

                    //start
                    if (ePreviousGroup != null && xPreviousGroup != null)
                        if (attached.ContainsKey(sSection) && attached[sSection] != "multiple")
                            eType = GetElementParent(sSection, xPreviousGroup.Name.ToString(), ref sParent);

                    //ugly cheats
                    if (sAttachTo == "Unknown" && sRootName == "INFO")
                    {
                        if (Root.Descendants(sAttachTo).ToList().Count == 0)
                        {
                            eType = ENestingType.CurrentNestedInPreviousParentElement;
                        }
                    }
                    else if (attached.ContainsKey(sSection) &&
                                previousGroup.Ancestors(sAttachTo).ToList().Count > 0 &&
                                eType == ENestingType.NewNesting)
                        eType = ENestingType.PreviousNestedInCurrent;

                    switch (eType)
                    {
                        case ENestingType.PreviousNestedInCurrent:
                            {
                                //check if parent is element or array
                                bool bResetCurrent = false;

                                if (previousGroup.Ancestors(sAttachTo).ToList().Count == 0)
                                    Console.WriteLine();

                                IEnumerable<string> blobs =
                                      from blob in previousGroup.Ancestors(sAttachTo).ToList()[0].Descendants()
                                      where (int)blob.Name.ToString().Length == 4
                                      select blob.Name.ToString();

                                List<string> list = blobs.Reverse().ToList();
                                foreach (var l in list)
                                {
                                    if (attached.ContainsKey(l) &&
                                        sAttachTo == attached[l])
                                    {
                                        eType = GetElementParent(sSection, l, ref sParent);
                                        if (eType == ENestingType.NoNesting)
                                            bResetCurrent = false;
                                        else if (eType == ENestingType.ResetCurrent)
                                            bResetCurrent = true;
                                        else
                                            Console.WriteLine();
                                        break;
                                    }
                                }

                                if (!bResetCurrent) //continue on currentGroup
                                {
                                    if (previousGroup.Ancestors(sAttachTo).ToList().Count == 0)
                                        Console.WriteLine();

                                    currentGroup = previousGroup.Ancestors(sAttachTo).ToList()[0];
                                    previousGroup = currentGroup;
                                    currentGroup = new XElement(sSection);
                                    previousGroup.Add(currentGroup);
                                    currentGroup.Add(new XAttribute("type", _e.Structure.type));
                                }
                                else //same as ResetCurrent
                                {
                                    if (previousGroup.Ancestors(sArrayName).ToList().Count == 0)
                                        Console.WriteLine();

                                    currentGroup = previousGroup.Ancestors(sArrayName).ToList()[0];
                                    previousGroup = currentGroup;
                                    currentGroup = new XElement(sAttachTo);
                                    previousGroup.Add(currentGroup);

                                    previousGroup = currentGroup;
                                    currentGroup = new XElement(sSection);
                                    previousGroup.Add(currentGroup);
                                    currentGroup.Add(new XAttribute("type", _e.Structure.type));
                                }

                                break;
                            }
                        case ENestingType.CurrentNestedInPrevious:
                            {
                                if (previousGroup != xPreviousGroup.Parent)
                                    Console.WriteLine();

                                currentGroup = new XElement(sArrayName);
                                currentGroup.Add(new XAttribute("type", "array"));
                                previousGroup.Add(currentGroup);

                                //new array element
                                previousGroup = currentGroup;
                                currentGroup = new XElement(sAttachTo);
                                previousGroup.Add(currentGroup);

                                //attach node
                                previousGroup = currentGroup;
                                currentGroup = new XElement(sSection);
                                previousGroup.Add(currentGroup);
                                currentGroup.Add(new XAttribute("type", _e.Structure.type));

                                break;
                            }
                        case ENestingType.ResetCurrent:
                            {
                                if (previousGroup.Ancestors(sArrayName).ToList().Count == 0)
                                    Console.WriteLine();

                                currentGroup = previousGroup.Ancestors(sArrayName).ToList()[0];
                                previousGroup = currentGroup;
                                currentGroup = new XElement(sAttachTo);
                                previousGroup.Add(currentGroup);

                                previousGroup = currentGroup;
                                currentGroup = new XElement(sSection);
                                previousGroup.Add(currentGroup);
                                currentGroup.Add(new XAttribute("type", _e.Structure.type));

                                break;
                            }
                        case ENestingType.CurrentNestedInPreviousParentElement:
                            {
                                if (sParent != "" && previousGroup.Ancestors(sParent).ToList().Count == 0)
                                    Console.WriteLine();

                                //new nesting
                                previousGroup = sParent != "" ? previousGroup.Ancestors(sParent).ToList()[0] : Root;
                                currentGroup = new XElement(sArrayName);
                                currentGroup.Add(new XAttribute("type", "array"));
                                previousGroup.Add(currentGroup);

                                //new array element
                                previousGroup = currentGroup;
                                currentGroup = new XElement(sAttachTo);
                                previousGroup.Add(currentGroup);

                                //attach node
                                previousGroup = currentGroup;
                                currentGroup = new XElement(sSection);
                                previousGroup.Add(currentGroup);
                                currentGroup.Add(new XAttribute("type", _e.Structure.type));

                                break;
                            }
                        case ENestingType.NewNesting:
                            {
                                previousGroup = Root;
                                currentGroup = new XElement(sArrayName);
                                currentGroup.Add(new XAttribute("type", "array"));
                                previousGroup.Add(currentGroup);

                                //new array element
                                previousGroup = currentGroup;
                                currentGroup = new XElement(sAttachTo);
                                previousGroup.Add(currentGroup);

                                //attach node
                                previousGroup = currentGroup;
                                currentGroup = new XElement(sSection);
                                previousGroup.Add(currentGroup);
                                currentGroup.Add(new XAttribute("type", _e.Structure.type));

                                break;
                            }
                        case ENestingType.NoNesting:
                            {
                                if (sAttachTo == "")
                                {
                                    previousGroup = Root;
                                    currentGroup = new XElement(_name);
                                    previousGroup.Add(currentGroup);
                                    currentGroup.Add(new XAttribute("type", _e.Structure.type));
                                }
                                else
                                {
                                    if (previousGroup != currentGroup.Parent)
                                        Console.WriteLine();

                                    previousGroup = currentGroup.Parent;
                                    currentGroup = new XElement(sSection);
                                    previousGroup.Add(currentGroup);
                                    currentGroup.Add(new XAttribute("type", _e.Structure.type));
                                }

                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }

                    if (attached.ContainsKey(currentGroup.Name.ToString()) &&
                        attached[currentGroup.Name.ToString()] == "multiple")
                        Console.WriteLine();
                    else
                    {
                        xPreviousGroup = currentGroup;
                        ePreviousGroup = _e;
                        lProcessed.Add(xPreviousGroup);
                    }
                }
                else
                {
                    if (sSection == "VMAD")
                    {
                        if (_name == "propertyArrayAlwaysOne")
                        {
                            nArrayAlwaysOne = Convert.ToInt32(_e.Value);
                            nArrayType = Convert.ToInt32(lElements[i - 1].Value);
                        }

                        if (nArrayIterator > 0 && nArrayIterator == nArrayCount)
                        {
                            nModulo = nArrayIterator = nArrayCount = 0;
                            bArrayDone = true;
                        }

                        if (_name == "fragmentHeaderUnknown")
                        {
                            if (Convert.ToInt32(_e.Value) != 0x02000000 &&
                                Convert.ToInt32(_e.Value) != 0x02 &&
                                Convert.ToInt32(_e.Value) != 0)
                                Console.WriteLine();

                            if (Convert.ToInt32(_e.Value) != 0)
                            {
                                previousGroup = currentGroup.Ancestors("VMAD").ToList()[0];
                                currentGroup = new XElement("Fragments");
                                previousGroup.Add(currentGroup);
                            }
                        }

                        if (_name.Contains("Count"))
                        {
                            if (_name == "scriptCount")
                            {
                                scriptCount = Convert.ToInt32(_e.Value);
                                if (scriptCount != 0)
                                {
                                    propertyCount = 0;
                                    propertyIterator = 0;

                                    previousGroup = currentGroup;
                                    currentGroup = new XElement("Scripts");
                                    currentGroup.Add(new XAttribute("type", "bundle"));
                                    previousGroup.Add(currentGroup);

                                    previousGroup = currentGroup;
                                    currentGroup = new XElement("Script");
                                    previousGroup.Add(currentGroup);
                                }
                            }
                            if (_name == "propertyCount")
                            {
                                propertyCount = Convert.ToInt32(_e.Value);
                                if (propertyCount != 0)
                                {
                                    previousGroup = currentGroup;
                                    currentGroup = new XElement("Properties");
                                    previousGroup.Add(currentGroup);

                                    previousGroup = currentGroup;
                                    currentGroup = new XElement("Property");
                                    previousGroup.Add(currentGroup);
                                }
                                else
                                {
                                    if (lElements[i + 1].Structure.name.Contains("object"))
                                    {
                                        propertyCount = 0;
                                        propertyIterator = 0;

                                        previousGroup = currentGroup.Ancestors("VMAD_Aliases").ToList()[0];
                                        currentGroup = new XElement("VMAD_Alias");
                                        previousGroup.Add(currentGroup);
                                    }
                                }
                            }
                            if (_name == "fragmentCount")
                            {
                                nFragmentCount = Convert.ToInt32(_e.Value);

                                if (nFragmentCount != 0)
                                {
                                    previousGroup = currentGroup;
                                    currentGroup = new XElement("Fragment");
                                    previousGroup.Add(currentGroup);
                                }
                                else
                                    Console.WriteLine();
                            }
                            if (_name == "aliasCount")
                            {
                                aliasCount = Convert.ToInt32(_e.Value);

                                if (aliasCount > 0)
                                {
                                    if (lElements[i - 1].Structure.name == "fragmentName" ||
                                        lElements[i - 1].Structure.name == "fragmentFileName")
                                        previousGroup = currentGroup.Ancestors("VMAD").ToList()[0];
                                    else
                                        previousGroup = currentGroup;

                                    currentGroup = new XElement("VMAD_Aliases");
                                    previousGroup.Add(currentGroup);

                                    previousGroup = currentGroup;
                                    currentGroup = new XElement("VMAD_Alias");
                                    previousGroup.Add(currentGroup);
                                }
                            }
                            if (_name == "propertyArrayCount")
                            {
                                nArrayCount = Convert.ToInt32(_e.Value);

                                if (nArrayAlwaysOne != 1)
                                    Console.WriteLine();
                            }
                        }

                        if (_e.Type == ElementValueType.BString)
                        {
                            if (_e.Value is byte[])
                            {
                                //skip string length
                                byte[] _skipped = (_e.Value as byte[]).Skip(2).ToArray();
                                string result = System.Text.Encoding.UTF8.GetString(_skipped);

                                var nElem = new XElement(_name, result);
                                nElem.Add(new XAttribute("type", _e.Structure.type));

                                if (hasOptions)
                                    GetOptions(_e, ref nElem);

                                if (hasFlags)
                                    GetFlags(_e, ref nElem);

                                if (_name == "fragmentFileName")
                                {
                                    //currentGroup.AncestorsAndSelf("Fragments").ToList()[0].Add(nElem);
                                    Root.Descendants("Fragments").ToList()[0].Add(nElem);
                                }
                                else
                                    currentGroup.Add(nElem);

                                if (_name == "propertyName")
                                    propertyIterator++;
                            }
                        }
                        else if (!_name.Contains("Count"))
                        {
                            if (_name.Contains("property"))
                            {
                                if (nArrayAlwaysOne == 1 && nArrayCount != 0)
                                {
                                    switch (nArrayType)
                                    {
                                        case 11: //Object Array
                                            {
                                                if (nArrayIterator == 0 && nModulo % 3 == 0)
                                                {
                                                    previousGroup = currentGroup;
                                                    currentGroup = new XElement("Array");
                                                    previousGroup.Add(currentGroup);

                                                    previousGroup = currentGroup;
                                                    currentGroup = new XElement("ArrayElement");
                                                    previousGroup.Add(currentGroup);
                                                    nArrayIterator++;
                                                }
                                                else
                                                {
                                                    if (nModulo % 3 == 0)
                                                    {
                                                        previousGroup = currentGroup.Ancestors("Array").ToList()[0];
                                                        currentGroup = new XElement("ArrayElement");
                                                        previousGroup.Add(currentGroup);
                                                        nArrayIterator++;
                                                    }
                                                }
                                                nModulo++;

                                                break;
                                            }
                                        case 15: //Bool Array
                                            {
                                                if (nArrayIterator == 0)
                                                {
                                                    previousGroup = currentGroup;
                                                    currentGroup = new XElement("Array");
                                                    previousGroup.Add(currentGroup);

                                                    previousGroup = currentGroup;
                                                    currentGroup = new XElement("ArrayElement");
                                                    previousGroup.Add(currentGroup);
                                                    nArrayIterator++;
                                                }
                                                else
                                                {
                                                    previousGroup = currentGroup.Ancestors("Array").ToList()[0];
                                                    currentGroup = new XElement("ArrayElement");
                                                    previousGroup.Add(currentGroup);
                                                    nArrayIterator++;
                                                }

                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                }
                            }

                            if (_name.Contains("fragment"))
                            {
                                if (sRootName == "INFO" && _name == "fragmentUnknown")
                                {
                                    previousGroup = Root.Descendants("Fragments").ToList()[0];
                                    currentGroup = new XElement("Fragment");
                                    previousGroup.Add(currentGroup);
                                }
                                else if (_name == "fragmentIndex")
                                {
                                    previousGroup = currentGroup.Ancestors("Fragments").ToList()[0];
                                    currentGroup = new XElement("Fragment");
                                    previousGroup.Add(currentGroup);
                                }
                            }

                            XElement nElem = null;

                            if (_e.Structure.type == ElementValueType.FormID)
                            {
                                string sFormIDElement = ConvertFormIdToString(Convert.ToUInt32(_e.Value));

                                nElem = new XElement(_name, sFormIDElement);
                                nElem.Add(new XAttribute("type", _e.Structure.type));

                                if (hasOptions)
                                    GetOptions(_e, ref nElem);

                                if (hasFlags)
                                    GetFlags(_e, ref nElem);

                                if ((PluginTree.TopRecord as Plugin).dForms.ContainsKey(sFormIDElement))
                                {
                                    nElem.Add(new XAttribute("name", (PluginTree.TopRecord as Plugin).dForms[sFormIDElement].sName));
                                    if ((PluginTree.TopRecord as Plugin).dForms[sFormIDElement].sGroup == "LVLN")
                                        nElem.Add(new XAttribute("LeveledTemplate", 1));
                                }
                                else
                                {
                                    bool result = UInt32.TryParse(_e.Value.ToString(), out uint nNotFound);

                                    if (nNotFound != 0)
                                    {
                                        //ugly
                                        if (_e.Structure.name == "propertyFormID" && nNotFound == 20 /*0x14*/)
                                            nElem.Add(new XAttribute("name", "Player"));
                                        else
                                            nElem.Add(new XAttribute("name", "FormID_NOTFOUND"));
                                    }
                                    else
                                        nElem.Add(new XAttribute("name", "FormID_ZERO"));
                                }
                                currentGroup.Add(nElem);
                            }
                            else
                            {
                                if (_e.Structure.type == ElementValueType.Int && _name == "fragmentHeaderUnknown")
                                {
                                    if (Convert.ToInt32(_e.Value) == 0x0)
                                        nElem = new XElement("NoFragments", 0);
                                    else
                                        nElem = new XElement(_name, 2);
                                }
                                else
                                    nElem = new XElement(_name, _e.Value);

                                nElem.Add(new XAttribute("type", _e.Structure.type));

                                if (hasOptions)
                                    GetOptions(_e, ref nElem);

                                if (hasFlags)
                                    GetFlags(_e, ref nElem);

                                if (_e.Structure.type == ElementValueType.Byte && _name == "propertyType")
                                {
                                    string sOption = "";

                                    switch (Convert.ToUInt32(_e.Value))
                                    {
                                        case 1: sOption = "Object"; break;
                                        case 2: sOption = "String"; break;
                                        case 3: sOption = "Int"; break;
                                        case 4: sOption = "Float"; break;
                                        case 5: sOption = "Bool"; break;

                                        case 11: sOption = "Array Object"; break;
                                        case 12: sOption = "Array String"; break;
                                        case 13: sOption = "Array Int"; break;
                                        case 14: sOption = "Array Float"; break;
                                        case 15: sOption = "Array Bool"; break;

                                        default:
                                            break;
                                    }

                                    nElem.Add(new XAttribute("options", sOption));
                                }
                                else if (_e.Structure.type == ElementValueType.Byte && _name == "fragmentFlag")
                                {
                                    string sOption = "";

                                    switch (Convert.ToUInt32(_e.Value))
                                    {
                                        case 0x01: sOption = "Begin"; break;
                                        case 0x02: sOption = "End"; break;
                                        case 0x03: sOption = "BothBeginEnd"; break;

                                        default:
                                            break;
                                    }

                                    nElem.Add(new XAttribute("options", sOption));
                                }
                                else if (_e.Structure.type == ElementValueType.Int && _name == "fragmentHeaderUnknown")
                                {
                                    string sOption = "";

                                    switch (Convert.ToUInt32(_e.Value))
                                    {
                                        case 0x02000000: sOption = "00000002"; break;
                                        case 0x02: sOption = "02"; break;
                                        case 0x0: break;//No Fragments

                                        default:
                                            break;
                                    }

                                    nElem.Add(new XAttribute("options", sOption));
                                }

                                currentGroup.Add(nElem);
                            }

                            if (_name.Contains("property"))
                            {
                                if (lElements[i + 1].Structure.name == "propertyName")
                                {
                                    if (propertyIterator <= propertyCount - 1)
                                    {
                                        if (bArrayDone)
                                            bArrayDone = false;

                                        previousGroup = currentGroup.Ancestors("Properties").ToList()[0];
                                        currentGroup = new XElement("Property");
                                        previousGroup.Add(currentGroup);
                                    }
                                }
                                /*no else*/
                                if (lElements[i + 1].Structure.name.Contains("script"))
                                {
                                    propertyCount = 0;
                                    propertyIterator = 0;

                                    previousGroup = currentGroup.Ancestors("Scripts").ToList()[0];
                                    currentGroup = new XElement("Script");
                                    previousGroup.Add(currentGroup);
                                }
                                else if (lElements[i + 1].Structure.name.Contains("object"))
                                {
                                    propertyCount = 0;
                                    propertyIterator = 0;

                                    previousGroup = currentGroup.Ancestors("VMAD_Aliases").ToList()[0];
                                    currentGroup = new XElement("VMAD_Alias");
                                    previousGroup.Add(currentGroup);
                                }
                            }
                        }
                    }
                    else if (sSection == "ALFE" ||
                        (sRootName == "QUST" && sSection == "ENAM") ||
                        (sRootName == "DIAL" && sSection == "SNAM"))
                    {
                        bool bStr4Completed = false;

                        for (int c = 0; c < cStr4.Length; c++)
                        {
                            if (cStr4[c] == 0)
                            {
                                cStr4[c] = (char)(Convert.ToUInt32(_e.Value));

                                if (c == cStr4.Length - 1)
                                    bStr4Completed = true;

                                break;
                            }
                        }

                        if (bStr4Completed)
                        {
                            XElement nElem = null;

                            if (sSection == "SNAM")
                                nElem = new XElement("Subtype_Name", new string(cStr4));
                            else
                                nElem = new XElement("From_Event", new string(cStr4));
                            nElem.Add(new XAttribute("type", "Str4"));
                            currentGroup.Add(nElem);

                            cStr4 = new char[4];
                        }
                    }
                    else
                    {
                        if (_e.Structure.type == ElementValueType.FormID)
                        {
                            string sFormIDElement = ConvertFormIdToString(Convert.ToUInt32(_e.Value));

                            var nElem = new XElement(_name, sFormIDElement);
                            nElem.Add(new XAttribute("type", _e.Structure.type));

                            if (hasOptions)
                                GetOptions(_e, ref nElem);

                            if (hasFlags)
                                GetFlags(_e, ref nElem);

                            if ((PluginTree.TopRecord as Plugin).dForms.ContainsKey(sFormIDElement))
                            {
                                nElem.Add(new XAttribute("name", (PluginTree.TopRecord as Plugin).dForms[sFormIDElement].sName));
                                if ((PluginTree.TopRecord as Plugin).dForms[sFormIDElement].sGroup == "LVLN")
                                    nElem.Add(new XAttribute("LeveledTemplate", 1));
                            }
                            else
                            {
                                bool result = UInt32.TryParse(_e.Value.ToString(), out uint nNotFound);

                                if (nNotFound != 0)
                                {
                                    if (sFunctionName != "")
                                    {
                                        if (_e.Structure.name == "Reference")
                                        {
                                            if (nNotFound == 20 /*0x14*/)
                                                nElem.Add(new XAttribute("name", "Player"));
                                            else
                                                Console.WriteLine();
                                        }
                                        else if (sFunctionName == "GetVMQuestVariable" ||
                                             sFunctionName == "GetVMScriptVariable" ||
                                             sFunctionName == "GetQuestVariable")
                                        {
                                            if (_name == "Parameter_2")
                                                nElem.Add(new XAttribute("name", "Unused"));
                                        }
                                        else if (sFunctionName == "GetStageDone")
                                        {
                                            if (_name == "Parameter_2")
                                                nElem.Add(new XAttribute("name", "Stage Index"));
                                        }
                                        else if (sFunctionName == "GetActorValue" ||
                                            sFunctionName == "GetActorValuePercent" ||
                                            sFunctionName.Contains("GetBaseActorValue"))
                                        {
                                            nElem.Add(new XAttribute("name", "Actor " +
                                                GetActorValue(Convert.ToInt32(nNotFound))));
                                        }
                                        else if (sFunctionName == "GetIsObjectType")
                                        {
                                            nElem.Add(new XAttribute("name", "Object " +
                                                GetObjectValue(nNotFound)));
                                        }
                                        else if (sFunctionName == "GetEventData")
                                        {
                                            //create 2 strings 0-3,4-7
                                            char[] c = nNotFound.ToString("X8").ToCharArray();
                                            if (c.Length != 8)
                                                Console.WriteLine();

                                            string sMember = c[0].ToString() + c[1].ToString() + c[2].ToString() + c[3].ToString();
                                            string sFunction = c[4].ToString() + c[5].ToString() + c[6].ToString() + c[7].ToString();

                                            string sNameMember = "";
                                            string sNameFunction = "";

                                            if (sMember == "314F")
                                                sNameMember = "CreatedObject";
                                            else if (sMember == "314C")
                                                sNameMember = "Old_Location";
                                            else if (sMember == "324C")
                                                sNameMember = "New_Location";
                                            else if (sMember == "314B")
                                                sNameMember = "Keyword";
                                            else if (sMember == "3146")
                                                sNameMember = "Form";
                                            else if (sMember == "3156")
                                                sNameMember = "Value1";
                                            else if (sMember == "3256")
                                                sNameMember = "Value2";
                                            else
                                                Console.WriteLine();

                                            if (sFunction == "0000")
                                                sNameFunction = "GetIsID";
                                            else if (sFunction == "0001")
                                                sNameFunction = "IsInList";
                                            else if (sFunction == "0002")
                                                sNameFunction = "GetValue";
                                            else if (sFunction == "0003")
                                                sNameFunction = "HasKeyword";
                                            else if (sFunction == "0004")
                                                sNameFunction = "GetItemValue";
                                            else
                                                Console.WriteLine();

                                            nElem.Add(new XAttribute("name", sNameFunction + '|' + sNameMember));
                                        }
                                        else if (_e.Structure.name.Contains("Event"))
                                        {
                                            nElem.Add(new XAttribute("name", "Event"));//has event data, not really FormID
                                        }
                                        else if (currentGroup.Ancestors("Objective").ToList().Count > 0 ||
                                            sFunctionName == "GetIsAliasRef")
                                        {
                                            if (sRootName == "INFO")
                                            {
                                                //get external alias from QUST via DIAL
                                                XDocument dialog = (PluginTree.TopRecord as Plugin).dDialogOwner;

                                                string sQuest = "QUST_" +
                                                    dialog.Root.Descendants("QUST_FormID").ToList()[0].Value.ToString() +
                                                    "_" +
                                                    dialog.Root.Descendants("QUST_FormID").ToList()[0].Attribute("name").Value.ToString();

                                                XDocument quest = XDocument.Load(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\Z\Quests\" + sQuest + ".xml");

                                                nElem.Add(new XAttribute("name", sQuest + '|' + GetAlias(quest, nNotFound)));
                                            }
                                            else //later, with aliases
                                                nElem.Add(new XAttribute("name", "FormID_NOTFOUND"));
                                        }
                                        else if (currentGroup.Ancestors("Alias").ToList().Count == 0)
                                        {
                                            if (nNotFound == 20 /*0x14*/)
                                                nElem.Add(new XAttribute("name", "Player"));
                                            else if (sRootName == "INFO")
                                            {
                                                //get external alias from QUST via DIAL
                                                XDocument dialog = (PluginTree.TopRecord as Plugin).dDialogOwner;

                                                string sQuest = "QUST_" +
                                                    dialog.Root.Descendants("QUST_FormID").ToList()[0].Value.ToString() +
                                                    "_" +
                                                    dialog.Root.Descendants("QUST_FormID").ToList()[0].Attribute("name").Value.ToString();

                                                XDocument quest = XDocument.Load(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\Z\Quests\" + sQuest + ".xml");

                                                nElem.Add(new XAttribute("name", sQuest + '|' + GetAlias(quest, nNotFound)));
                                            }
                                            else
                                                Console.WriteLine();
                                        }
                                        else
                                        {
                                            nElem.Add(new XAttribute("name", "FormID_NOTFOUND"));
                                            Console.WriteLine();
                                        }
                                    }
                                    else
                                    {
                                        if (_e.Structure.name.Contains("Event"))
                                            nElem.Add(new XAttribute("name", "Event"));//has event data, not really FormID
                                        else if (sSection == "ALFR")
                                        {
                                            if (nNotFound == 20 /*0x14*/)
                                                nElem.Add(new XAttribute("name", "Player"));
                                            else
                                                Console.WriteLine();
                                        }
                                        else
                                        {
                                            if (sSection == "TINC")
                                                nElem.Add(new XAttribute("name", "AlphaXX_BlueXX_GreenXX_RedXX"));
                                            else
                                                nElem.Add(new XAttribute("name", "FormID_NOTFOUND"));
                                        }
                                    }
                                }
                                else
                                {
                                    if (sFunctionName != "")
                                    {
                                        if (sRootName == "INFO" &&
                                            sFunctionName == "GetIsAliasRef")
                                        {
                                            if (_name == "Parameter_1")
                                            {
                                                //get external alias from QUST via DIAL
                                                XDocument dialog = (PluginTree.TopRecord as Plugin).dDialogOwner;

                                                string sQuest = "QUST_" +
                                                    dialog.Root.Descendants("QUST_FormID").ToList()[0].Value.ToString() +
                                                    "_" +
                                                    dialog.Root.Descendants("QUST_FormID").ToList()[0].Attribute("name").Value.ToString();

                                                XDocument quest = XDocument.Load(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\Z\Quests\" + sQuest + ".xml");

                                                nElem.Add(new XAttribute("name", sQuest + '|' + GetAlias(quest, nNotFound)));
                                            }
                                        }
                                    }
                                    else
                                        nElem.Add(new XAttribute("name", "FormID_ZERO"));
                                }
                            }
                            currentGroup.Add(nElem);
                        }
                        else if (_e.Structure.type == ElementValueType.LString)
                        {
                            if (_e.Value is byte[])
                            {
                                //remove null character
                                string result = System.Text.Encoding.UTF8.GetString(_e.Value as byte[]).Trim('\0');

                                var nElem = new XElement(_name, result);
                                nElem.Add(new XAttribute("type", _e.Structure.type));

                                if (hasOptions)
                                    GetOptions(_e, ref nElem);

                                if (hasFlags)
                                    GetFlags(_e, ref nElem);

                                currentGroup.Add(nElem);
                            }
                            else
                            {
                                bool bIsUInt = false;
                                int uResult = 0;

                                if (_e.Structure.type == ElementValueType.LString &&
                                    Int32.TryParse(_e.Value.ToString(), out uResult) &&
                                    _e.Type == ElementValueType.UInt)
                                    bIsUInt = true;

                                XElement nElem = null;
                                if (bIsUInt)
                                {
                                    nElem = new XElement(_name, _e.Value);
                                    nElem.Add(new XAttribute("type", ElementValueType.UInt));

                                    //add name attribute
                                    if (uResult > 0)
                                    {
                                        if ((PluginTree.TopRecord as Plugin).tlk.ContainsKey(uResult))
                                            nElem.Add(new XAttribute("name", (PluginTree.TopRecord as Plugin).tlk[uResult]));
                                        else
                                            nElem.Add(new XAttribute("name", "UnknownText_" + uResult));
                                    }
                                }
                                else
                                    Console.WriteLine();//later

                                if (hasOptions)
                                    GetOptions(_e, ref nElem);

                                if (hasFlags)
                                    GetFlags(_e, ref nElem);

                                currentGroup.Add(nElem);
                            }
                        }
                        else if (_e.Structure.type == ElementValueType.BString)
                        {
                            if (_e.Value is byte[])
                            {
                                //skip string length
                                byte[] _skipped = (_e.Value as byte[]).Skip(2).ToArray();
                                string result = System.Text.Encoding.UTF8.GetString(_skipped);

                                var nElem = new XElement(_name, result);
                                nElem.Add(new XAttribute("type", _e.Structure.type));

                                if (hasOptions)
                                    GetOptions(_e, ref nElem);

                                if (hasFlags)
                                    GetFlags(_e, ref nElem);

                                currentGroup.Add(nElem);
                            }
                        }
                        else if (!_name.Contains("Count"))
                        {
                            if (_name.Contains("_Offset"))
                            {
                                int nOffset = 0;
                                if (Convert.ToInt32(_e.Value) > 60000)
                                    nOffset = Convert.ToInt32(_e.Value) - 0xFFFF - 1;
                                else nOffset = Convert.ToInt32(_e.Value);

                                var nElem = new XElement(_name, nOffset);
                                nElem.Add(new XAttribute("type", _e.Structure.type));
                                currentGroup.Add(nElem);
                            }
                            else if (_name.Contains("mayLocalize"))
                            {
                                //should never hit here
                                Console.WriteLine();
                            }
                            else
                            {
                                var nElem = new XElement(_name, _e.Value);
                                nElem.Add(new XAttribute("type", _e.Structure.type));

                                if (hasOptions)
                                    GetOptions(_e, ref nElem);

                                if (_name.Contains("Function_Name")) //ugly
                                    sFunctionName = nElem.LastAttribute.Value.ToString();

                                if (hasFlags)
                                    GetFlags(_e, ref nElem);

                                currentGroup.Add(nElem);
                            }
                        }
                        else //if Count
                        {
                            var nElem = new XElement(_name, Convert.ToInt32(_e.Value));
                            nElem.Add(new XAttribute("type", _e.Structure.type));
                            currentGroup.Add(nElem);
                        }

                        if (_name.Contains("Always0"))
                        {
                            if (Convert.ToInt32(_e.Value) != 0)
                                Console.WriteLine();
                        }
                    }
                }
            }

            #region Update Alias ID with Name
            if (sRootName == "QUST")
            {
                //can be method GetAlias?
                var aliases = Root.Descendants("Alias").ToList();
                Dictionary<uint, string> dAliases = new Dictionary<uint, string>();
                if (aliases.Count > 0)
                {
                    foreach (XElement a in aliases)
                    {
                        var alst = a.Descendants("ALST").ToList();
                        var alls = a.Descendants("ALLS").ToList();
                        var alid = a.Descendants("ALID").ToList()[0];

                        if (alst.Count == 0 && alls.Count == 0)
                            Console.WriteLine();
                        if ((alst.Count > 1 || alls.Count > 1))
                            Console.WriteLine();

                        if (alst.Count == 1)
                            dAliases.Add(Convert.ToUInt32(alst.Descendants().ToList()[0].Value),
                                alid.Descendants().ToList()[0].Value.ToString());
                        else if (alls.Count == 1)
                            dAliases.Add(Convert.ToUInt32(alls.Descendants().ToList()[0].Value),
                                alid.Descendants().ToList()[0].Value.ToString());
                        else
                            Console.WriteLine();
                    }
                }

                IEnumerable<XElement> getGroup =
                                          from blob in Root.Descendants()
                                          where blob.Name.ToString().All(char.IsUpper)
                                          select blob;

                foreach (XElement x in getGroup)
                {
                    currentGroup = x;
                    IEnumerable<XElement> getElement = null;

                    //update VMAD objectAlias
                    if (x.Name == "VMAD")
                    {
                        getElement = from blob in x.Descendants()
                                     where !blob.Name.ToString().Contains("VMAD") &&
                                      blob.Name.ToString().Contains("Alias") &&
                                       blob.Value != "-1"

                                     select blob;
                    }
                    else
                    {
                        getElement = from blob in x.Descendants()
                                     where blob.HasAttributes &&
                                            blob.Attributes("type").Any() &&
                                             blob.Attributes("name").Any() &&
                                              blob.Attribute("type").Value == "FormID" &&
                                               blob.Attribute("name").Value == "FormID_NOTFOUND"
                                     select blob;
                    }

                    if (getElement.ToList().Count > 0)
                    {
                        foreach (var ee in getElement)
                        {
                            bool bLocal = false;

                            IEnumerable<XElement> getElementFormID = null;

                            if (x.Name == "CTDA")
                                bLocal = true;
                            else
                            {
                                getElementFormID = from blob in ee.Parent.Descendants()
                                                   where blob.Name.ToString().Contains("FormID")
                                                   select blob;

                                if (getElementFormID.ToList().Count == 0)
                                {
                                }
                                else if (getElementFormID.ToList().Count == 1)
                                {
                                    if (getElementFormID.ToList()[0].Value.ToString() == sFormID)
                                        bLocal = true;
                                }
                                else
                                {
                                    if (getElementFormID.ToList()[0].Value.ToString() == sFormID)
                                        bLocal = true;
                                    else
                                        Console.WriteLine();
                                }
                            }

                            uint nNotFound = 0;

                            if (x.Name == "VMAD") //decimal
                                nNotFound = Convert.ToUInt32(ee.Value);
                            else //hex
                                nNotFound = uint.Parse(ee.Value.ToString(), System.Globalization.NumberStyles.HexNumber);

                            if (bLocal)
                            {
                                if (!dAliases.ContainsKey(nNotFound))
                                {
                                    if (x.Name == "CTDA")
                                    {
                                        if (nNotFound == 20) //0x14
                                        {
                                            if (ee.Attribute("name") == null)
                                            {
                                                ee.Add(new XAttribute("name", "Player"));
                                            }
                                            else
                                                ee.Attributes("name").ToList()[0].Value = "Player";
                                        }
                                        else
                                            Console.WriteLine();
                                    }
                                    else
                                        Console.WriteLine();
                                }
                                else
                                {
                                    if (ee.Attribute("name") == null)
                                    {
                                        ee.Add(new XAttribute("name", dAliases[nNotFound]));
                                    }
                                    else
                                        ee.Attributes("name").ToList()[0].Value = dAliases[nNotFound];
                                }
                            }
                            else
                            {
                                var getElementName = from blob in ee.Parent.Descendants()
                                                     where blob.Name.ToString().Contains("Name")
                                                     select blob;

                                if (getElementName.ToList().Count == 0)
                                {
                                    Console.WriteLine();
                                }
                                else if (getElementName.ToList().Count == 1)
                                {
                                    if (ee.Attribute("name") == null)
                                    {
                                        ee.Add(new XAttribute("name", "External_Alias_Index"));
                                    }
                                    else
                                        ee.Attributes("name").ToList()[0].Value = "External_Alias_Index";
                                }
                                else
                                    Console.WriteLine();
                            }
                        }
                    }

                    previousGroup = currentGroup;
                }
            }
            #endregion

            //ugly Remove empty Fragmen XElement
            IEnumerable<XElement> fragments = from blob in Root.Descendants("Fragment")
                                              where blob.Descendants().ToList().Count == 0
                                              select blob;

            if (fragments.ToList().Count > 1)
                Console.WriteLine();
            else if (fragments.ToList().Count == 1)
                fragments.ToList()[0].Remove();

            if (sRootName == "DIAL")
                (PluginTree.TopRecord as Plugin).dDialogOwner = xdDocument;

            xdDocument.Save(nf);
        }

        private string GetObjectValue(uint nNotFound)
        {
            switch (nNotFound)
            {
                case 0: return "Activator";
                case 1: return "Armor";
                case 2: return "Book";
                case 3: return "Container";
                case 4: return "Door";
                case 5: return "Ingredient";
                case 6: return "Light";
                case 7: return "MiscItem";
                case 8: return "Static";
                case 9: return "Grass";
                case 10: return "Tree";
                case 12: return "Weapon";
                case 13: return "Actor";
                case 14: return "LeveledCharacter";
                case 15: return "Spell";
                case 16: return "Enchantment";
                case 17: return "Potion";
                case 18: return "LeveledItem";
                case 19: return "Key";
                case 20: return "Ammo";
                case 21: return "Flora";
                case 22: return "Furniture";
                case 23: return "SoundMarker";
                case 24: return "LandTexture";
                case 25: return "CombatStyle";
                case 26: return "LoadScreen";
                case 27: return "LeveledSpell";
                case 28: return "AnimObject";
                case 29: return "WaterType";
                case 30: return "IdleMarker";
                case 31: return "EffectShader";
                case 32: return "Projectile";
                case 33: return "TalkingActivator";
                case 34: return "Explosion";
                case 35: return "TextureSet";
                case 36: return "Debris";
                case 37: return "MenuIcon";
                case 38: return "FormList";
                case 39: return "Perk";
                case 40: return "BodyPartData";
                case 41: return "AddOnNode";
                case 42: return "MovableStatic";
                case 43: return "CameraShot";
                case 44: return "ImpactData";
                case 45: return "ImpactDataSet";
                case 46: return "Quest";
                case 47: return "Package";
                case 48: return "VoiceType";
                case 49: return "Class";
                case 50: return "Race";
                case 51: return "Eyes";
                case 52: return "HeadPart";
                case 53: return "Faction";
                case 54: return "Note";
                case 55: return "Weather";
                case 56: return "Climate";
                case 57: return "ArmorAddon";
                case 58: return "Global";
                case 59: return "Imagespace";
                case 60: return "ImagespaceModifier";
                case 61: return "EncounterZone";
                case 62: return "Message";
                case 63: return "ConstructibleObject";
                case 64: return "AcousticSpace";
                case 65: return "Ragdoll";
                case 66: return "Script";
                case 67: return "MagicEffect";
                case 68: return "MusicType";
                case 69: return "StaticCollection";
                case 70: return "Keyword";
                case 71: return "Location";
                case 72: return "LocationRefType";
                case 73: return "Footstep";
                case 74: return "FootstepSet";
                case 75: return "MaterialType";
                case 76: return "ActorAction";
                case 77: return "MusicTrack";
                case 78: return "WordofPower";
                case 79: return "Shout";
                case 80: return "Relationship";
                case 81: return "EquipSlot";
                case 82: return "AssociationType";
                case 83: return "Outfit";
                case 84: return "ArtObject";
                case 85: return "MaterialObject";
                case 87: return "LightingTemplate";
                case 88: return "ShaderParticleGeometry";
                case 89: return "VisualEffect";
                case 90: return "Apparatus";
                case 91: return "MovementType";
                case 92: return "Hazard";
                case 93: return "SMEventNode";
                case 94: return "SoundDescriptor";
                case 95: return "DualCastData";
                case 96: return "SoundCategory";
                case 97: return "SoulGem";
                case 98: return "SoundOutputModel";
                case 99: return "CollisionLayer";
                case 100: return "Scroll";
                case 101: return "ColorForm";
                case 102: return "ReverbParameters";
                default:
                    {
                        Console.WriteLine();
                        break;
                    }
            }

            return string.Empty;
        }

        private string GetActorValue(int nNotFound)
        {
            switch (nNotFound)
            {
                case 00: return "Aggresion";
                case 01: return "Confidence";
                case 02: return "Energy";
                case 03: return "Morality";
                case 04: return "Mood";
                case 05: return "Assistance";
                case 06: return "One-Handed";
                case 07: return "Two-Handed";
                case 08: return "Archery";
                case 09: return "Block";
                case 10: return "Smithing";
                case 11: return "Heavy Armor";
                case 12: return "Light Armor";
                case 13: return "Pickpocket";
                case 14: return "Lockpicking";
                case 15: return "Sneak";
                case 16: return "Alchemy";
                case 17: return "Speech";
                case 18: return "Alteration";
                case 19: return "Conjuration";
                case 20: return "Destruction";
                case 21: return "Illusion";
                case 22: return "Restoration";
                case 23: return "Enchanting";
                case 24: return "Health";
                case 25: return "Magicka";
                case 26: return "Stamina";
                case 27: return "Heal Rate";
                case 28: return "Magicka Rate";
                case 29: return "Stamina Rate";
                case 30: return "Speed Mult";
                case 31: return "Inventory Weight";
                case 32: return "Carry Weight";
                case 33: return "Critical Chance";
                case 34: return "Melee Damage";
                case 35: return "Unarmed Damage";
                case 36: return "Mass";
                case 37: return "Voice Points";
                case 38: return "Voice Rate";
                case 39: return "Damage Resist";
                case 40: return "Poison Resist";
                case 41: return "Resist Fire";
                case 42: return "Resist Shock";
                case 43: return "Resist Frost";
                case 44: return "Resist Magic";
                case 45: return "Resist Disease";
                case 46: return "Unknown 46";
                case 47: return "Unknown 47";
                case 48: return "Unknown 48";
                case 49: return "Unknown 49";
                case 50: return "Unknown 50";
                case 51: return "Unknown 51";
                case 52: return "Unknown 52";
                case 53: return "Paralysis";
                case 54: return "Invisibility";
                case 55: return "Night Eye";
                case 56: return "Detect Life Range";
                case 57: return "Water Breathing";
                case 58: return "Water Walking";
                case 59: return "Unknown 59";
                case 60: return "Fame";
                case 61: return "Infamy";
                case 62: return "Jumping Bonus";
                case 63: return "Ward Power";
                case 64: return "Right Item Charge";
                case 65: return "Armor Perks";
                case 66: return "Shield Perks";
                case 67: return "Ward Deflection";
                case 68: return "Variable01";
                case 69: return "Variable02";
                case 70: return "Variable03";
                case 71: return "Variable04";
                case 72: return "Variable05";
                case 73: return "Variable06";
                case 74: return "Variable07";
                case 75: return "Variable08";
                case 76: return "Variable09";
                case 77: return "Variable10";
                case 78: return "Bow Speed Bonus";
                case 79: return "Favor Active";
                case 80: return "Favors Per Day";
                case 81: return "Favors Per Day Timer";
                case 82: return "Left Item Charge";
                case 83: return "Absorb Chance";
                case 84: return "Blindness";
                case 85: return "Weapon Speed Mult";
                case 86: return "Shout Recovery Mult";
                case 87: return "Bow Stagger Bonus";
                case 88: return "Telekinesis";
                case 89: return "Favor Points Bonus";
                case 90: return "Last Bribed Intimidated";
                case 91: return "Last Flattered";
                case 92: return "Movement Noise Mult";
                case 93: return "Bypass Vendor Stolen Check";
                case 94: return "Bypass Vendor Keyword Check";
                case 95: return "Waiting For Player";
                case 96: return "One-Handed Modifier";
                case 97: return "Two-Handed Modifier";
                case 98: return "Marksman Modifier";
                case 99: return "Block Modifier";
                case 100: return "Smithing Modifier";
                case 101: return "Heavy Armor Modifier";
                case 102: return "Light Armor Modifier";
                case 103: return "Pickpocket Modifier";
                case 104: return "Lockpicking Modifier";
                case 105: return "Sneaking Modifier";
                case 106: return "Alchemy Modifier";
                case 107: return "Speechcraft Modifier";
                case 108: return "Alteration Modifier";
                case 109: return "Conjuration Modifier";
                case 110: return "Destruction Modifier";
                case 111: return "Illusion Modifier";
                case 112: return "Restoration Modifier";
                case 113: return "Enchanting Modifier";
                case 114: return "One-Handed Skill Advance";
                case 115: return "Two-Handed Skill Advance";
                case 116: return "Marksman Skill Advance";
                case 117: return "Block Skill Advance";
                case 118: return "Smithing Skill Advance";
                case 119: return "Heavy Armor Skill Advance";
                case 120: return "Light Armor Skill Advance";
                case 121: return "Pickpocket Skill Advance";
                case 122: return "Lockpicking Skill Advance";
                case 123: return "Sneaking Skill Advance";
                case 124: return "Alchemy Skill Advance";
                case 125: return "Speechcraft Skill Advance";
                case 126: return "Alteration Skill Advance";
                case 127: return "Conjuration Skill Advance";
                case 128: return "Destruction Skill Advance";
                case 129: return "Illusion Skill Advance";
                case 130: return "Restoration Skill Advance";
                case 131: return "Enchanting Skill Advance";
                case 132: return "Left Weapon Speed Multiply";
                case 133: return "Dragon Souls";
                case 134: return "Combat Health Regen Multiply";
                case 135: return "One-Handed Power Modifier";
                case 136: return "Two-Handed Power Modifier";
                case 137: return "Marksman Power Modifier";
                case 138: return "Block Power Modifier";
                case 139: return "Smithing Power Modifier";
                case 140: return "Heavy Armor Power Modifier";
                case 141: return "Light Armor Power Modifier";
                case 142: return "Pickpocket Power Modifier";
                case 143: return "Lockpicking Power Modifier";
                case 144: return "Sneaking Power Modifier";
                case 145: return "Alchemy Power Modifier";
                case 146: return "Speechcraft Power Modifier";
                case 147: return "Alteration Power Modifier";
                case 148: return "Conjuration Power Modifier";
                case 149: return "Destruction Power Modifier";
                case 150: return "Illusion Power Modifier";
                case 151: return "Restoration Power Modifier";
                case 152: return "Enchanting Power Modifier";
                case 153: return "Dragon Rend";
                case 154: return "Attack Damage Mult";
                case 155: return "Heal Rate Mult";
                case 156: return "Magicka Rate Mult";
                case 157: return "Stamina Rate Mult";
                case 158: return "Werewolf Perks";
                case 159: return "Vampire Perks";
                case 160: return "Grab Actor Offset";
                case 161: return "Grabbed";
                case 162: return "Unknown 162";
                case 163: return "Reflect Damage";
                case -1:
                default:
                    return "None";
            }
        }

        private string GetChildOf(string name)
        {
            var ggg = (PluginTree.TopRecord as Plugin).groups;
            var queue = new Queue<Domain.Data.RecordStructure.Group>();
            Domain.Data.RecordStructure.Group _group = null;

            foreach (var _r in ggg.Values)
            {
                if (_r is Domain.Data.RecordStructure.Group)
                {
                    queue.Enqueue(_r as Domain.Data.RecordStructure.Group);
                }
            }

            while (queue.Count > 0)
            {
                // Take the next node from the front of the queue
                var node = queue.Dequeue();

                // Add the node’s children to the back of the queue
                foreach (var child in node.Items)
                {
                    if (child is Domain.Data.RecordStructure.Group)
                        queue.Enqueue(child as Domain.Data.RecordStructure.Group);
                    else if (child is Subrecord)
                    {
                        if ((child as Subrecord).name == name)
                        {
                            _group = node as Domain.Data.RecordStructure.Group;
                            break;
                        }
                    }
                }
            }

            foreach (var _r in ggg.Values)
            {
                if (_r is Domain.Data.RecordStructure.Group)
                {
                    queue.Enqueue(_r as Domain.Data.RecordStructure.Group);
                }
            }

            while (queue.Count > 0)
            {
                // Take the next node from the front of the queue
                var node = queue.Dequeue();

                // Add the node’s children to the back of the queue
                foreach (var child in node.Items)
                {
                    if (child is Domain.Data.RecordStructure.Group)
                    {
                        if ((child as Domain.Data.RecordStructure.Group) == _group)
                        {

                        }
                        else queue.Enqueue(child as Domain.Data.RecordStructure.Group);
                    }
                }
            }

            return string.Empty;
        }

        private void GetAttachTo(ref Element pre)
        {
            var ggg = (PluginTree.TopRecord as Plugin).groups;

            foreach (var g in ggg.Values)
            {
                bool bFound = false;

                if (g is Domain.Data.RecordStructure.Group)
                {
                    foreach (var r in g.Items)
                    {
                        if (r is Subrecord)
                        {
                            if ((r as Subrecord).name == pre.Structure.name)
                            {
                                if ((r as Subrecord).attachTo != "")
                                {
                                    pre.Structure.attachTo = (r as Subrecord).attachTo;
                                    bFound = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (bFound)
                    break;
            }
        }

        private bool IsRelated(string sRelative1, string sRelative2)
        {
            if (sRelative1 == sRelative2) return false;

            var ggg = (PluginTree.TopRecord as Plugin).groups;
            Dictionary<string, string> nested = new Dictionary<string, string>();
            Dictionary<string, string> attached = GetAttached((PluginTree.TopRecord as Plugin).sGroupName);

            string aRelative1 = attached.ContainsKey(sRelative1) ? attached[sRelative1] : "";
            string aRelative2 = attached.ContainsKey(sRelative2) ? attached[sRelative2] : "";

            Domain.Data.RecordStructure.Group gRelative1 = null;
            Domain.Data.RecordStructure.Group gRelative2 = null;

            foreach (var g in ggg.Values)
            {
                if (g.attachTo == aRelative1)
                {
                    gRelative1 = g;
                    foreach (var r in g.Items)
                    {
                        if (r is Domain.Data.RecordStructure.Group)
                        {
                            foreach (var _gg in ggg.Values)
                            {
                                if (_gg.id == (r as Domain.Data.RecordStructure.Group).id)
                                    (r as Domain.Data.RecordStructure.Group).attachTo = _gg.attachTo;
                            }
                        }
                    }
                    break;
                }
            }

            foreach (var g in ggg.Values)
            {
                if (g.attachTo == aRelative2)
                {
                    gRelative2 = g;
                    foreach (var r in g.Items)
                    {
                        if (r is Domain.Data.RecordStructure.Group)
                        {
                            foreach (var _gg in ggg.Values)
                            {
                                if (_gg.id == (r as Domain.Data.RecordStructure.Group).id)
                                    (r as Domain.Data.RecordStructure.Group).attachTo = _gg.attachTo;
                            }
                        }
                    }
                    break;
                }
            }

            foreach (var g in gRelative1.Items)
            {
                if (g is Domain.Data.RecordStructure.Group)
                {
                    if ((g as Domain.Data.RecordStructure.Group).attachTo == aRelative2)
                        return true;
                }
            }

            /*foreach (var g in gRelative2.Items)
            {
                if (g is Domain.Data.RecordStructure.Group)
                {
                    if ((g as Domain.Data.RecordStructure.Group).attachTo == aRelative1)
                        return true;
                }
            }*/

            return false;
        }

        private ENestingType GetElementParent(string sSection, string sPreviousSection, ref string sParent)
        {
            if (sSection != "" && sSection == sPreviousSection)
                return ENestingType.ResetCurrent;

            Plugin _p = PluginTree.TopRecord as Plugin;

            Domain.Data.RecordStructure.Group groupCurrent = GetSectionGroup(sSection);
            Domain.Data.RecordStructure.Group groupPrevious = GetSectionGroup(sPreviousSection);

            bool bIsShareParent = IsShareParent(groupCurrent, groupPrevious, ref sParent);

            if (bIsShareParent)
                return ENestingType.CurrentNestedInPreviousParentElement;

            //TEMP
            bool bPreviousNestedInCurrent = false;
            bool bCurrentNestedInPrevious = false;

            int nPrevious = -1;
            int nCurrent = -1;

            if (groupCurrent != null)
            {
                if (groupCurrent.attachTo != "")
                {
                    for (int c = 0; c < groupCurrent.Items.Count; c++)
                    {
                        var g = groupCurrent.Items[c];
                        if (g is Subrecord)
                        {
                            if ((g as Subrecord).name == sPreviousSection)
                            {
                                nPrevious = c;
                                break;
                            }
                        }
                    }

                    for (int c = 0; c < groupCurrent.Items.Count; c++)
                    {
                        var g = groupCurrent.Items[c];
                        if (g is Subrecord)
                        {
                            if ((g as Subrecord).name == sSection)
                            {
                                nCurrent = c;
                                break;
                            }
                        }
                    }
                }
            }

            if (groupCurrent != null && groupPrevious != null && groupPrevious != groupCurrent)
            {
                Domain.Data.RecordStructure.Group gGroup = null;
                for (int c = 0; c < groupPrevious.Items.Count; c++)
                {
                    var g = groupPrevious.Items[c];

                    if (g is Domain.Data.RecordStructure.Group)
                    {
                        foreach (var _g in _p.groups)
                        {
                            if (_g.Value.id == (g as Domain.Data.RecordStructure.Group).id)
                            {
                                gGroup = _g.Value;
                                break;
                            }
                        }

                        foreach (var _it in gGroup.Items)
                        {
                            if (_it is Subrecord)
                            {
                                if ((_it as Subrecord).name == sSection)
                                {
                                    bCurrentNestedInPrevious = true;
                                    return ENestingType.CurrentNestedInPrevious;
                                }
                            }
                        }
                    }
                }

                for (int c = 0; c < groupCurrent.Items.Count; c++)
                {
                    var g = groupCurrent.Items[c];

                    if (g is Domain.Data.RecordStructure.Group)
                    {
                        foreach (var _g in _p.groups)
                        {
                            if (_g.Value.id == (g as Domain.Data.RecordStructure.Group).id)
                            {
                                gGroup = _g.Value;
                                break;
                            }
                        }

                        foreach (var _it in gGroup.Items)
                        {
                            if (_it is Subrecord)
                            {
                                if ((_it as Subrecord).name == sPreviousSection)
                                {
                                    bPreviousNestedInCurrent = true;
                                    return ENestingType.PreviousNestedInCurrent;
                                }
                            }
                        }
                    }
                }
            }

            if (bPreviousNestedInCurrent && bCurrentNestedInPrevious)
            {
                Console.WriteLine();
            }
            /*else if (bPreviousNestedInCurrent)
            {
                Console.WriteLine();
            }
            else if (bCurrentNestedInPrevious)
            {
                Console.WriteLine();
            }*/
            else
            {
                if (groupCurrent != null && nPrevious > nCurrent)
                    return ENestingType.ResetCurrent;

                if (groupCurrent != null &&
                        nPrevious == -1 /*groupPrevious == null*/ &&
                            groupCurrent.attachTo != "")
                    return ENestingType.NewNesting;
            }

            return ENestingType.NoNesting;
        }

        private bool IsShareParent(Domain.Data.RecordStructure.Group groupCurrent, Domain.Data.RecordStructure.Group groupPrevious, ref string sParent)
        {
            if (groupCurrent == null || groupPrevious == null)
                return false;

            if (groupCurrent == groupPrevious)
                return false;

            Domain.Data.RecordStructure.Group groupCurrentParent = null;
            Domain.Data.RecordStructure.Group groupPreviousParent = null;

            var ggg = (PluginTree.TopRecord as Plugin).groups;

            var queue = new Queue<Domain.Data.RecordStructure.Group>();

            foreach (var _r in ggg.Values)
            {
                if (_r is Domain.Data.RecordStructure.Group)
                {
                    queue.Enqueue(_r as Domain.Data.RecordStructure.Group);
                }
            }

            while (queue.Count > 0)
            {
                // Take the next node from the front of the queue
                var node = queue.Dequeue();

                // Add the node’s children to the back of the queue
                foreach (var child in node.Items)
                {
                    if (child is Domain.Data.RecordStructure.Group)
                    {
                        if ((child as Domain.Data.RecordStructure.Group).id == groupCurrent.id)
                        {
                            groupCurrentParent = node as Domain.Data.RecordStructure.Group;
                            break;
                        }
                        else
                            queue.Enqueue(child as Domain.Data.RecordStructure.Group);
                    }
                }
            }

            queue = new Queue<Domain.Data.RecordStructure.Group>();

            foreach (var _r in ggg.Values)
            {
                if (_r is Domain.Data.RecordStructure.Group)
                {
                    queue.Enqueue(_r as Domain.Data.RecordStructure.Group);
                }
            }

            while (queue.Count > 0)
            {
                // Take the next node from the front of the queue
                var node = queue.Dequeue();

                // Add the node’s children to the back of the queue
                foreach (var child in node.Items)
                {
                    if (child is Domain.Data.RecordStructure.Group)
                    {
                        if ((child as Domain.Data.RecordStructure.Group).id == groupPrevious.id)
                        {
                            groupPreviousParent = node as Domain.Data.RecordStructure.Group;
                            break;
                        }
                        else
                            queue.Enqueue(child as Domain.Data.RecordStructure.Group);
                    }
                }
            }

            if (groupCurrentParent != null && groupCurrentParent == groupPreviousParent)
            {
                sParent = groupCurrentParent.attachTo;
                return true;
            }

            return false;
        }

        private void UpdateMainText(string text)
        {
            // tbInfo.Text = text;
            this.SelectedText.Text = text;
        }

        private void UpdateStringEditor()
        {
            if (this.stringEditor != null)
            {
                var plugins = PluginList.All.Records.OfType<Plugin>().ToList();
                if (plugins.Count == 0)
                {
                    this.CloseStringEditor();
                }
                else
                {
                    this.stringEditor.Reload(plugins.ToArray());
                }
            }
        }

        private void addMasterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var amfNewMaster = new AddMasterForm())
            {
                if (amfNewMaster.ShowDialog(this) == DialogResult.OK)
                {
                    Plugin plugin = this.GetPluginFromNode(this.PluginTree.SelectedRecord);
                    if (plugin == null)
                    {
                        MessageBox.Show(this, "No plugin selected. Cannot continue.", "Missing Plugin",
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    try
                    {
                        if (plugin.AddMaster(amfNewMaster.MasterName))
                        {
                            this.FixMasters();
                            this.RebuildSelection();
                        }
                    }
                    catch (ApplicationException ex)
                    {
                        MessageBox.Show(this, ex.Message, "Missing Record", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var actions = e.Argument as Action[];
            if (actions.Length > 0)
            {
                actions[0]();
            }

            if (actions.Length > 1)
            {
                e.Result = actions[1];
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.toolStripStatusProgressBar.Value = e.ProgressPercentage % this.toolStripStatusProgressBar.Maximum;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.EnableUserInterface(true);
            this.toolStripStatusProgressBar.Visible = false;
            this.toolStripStopProgress.Visible = false;
            if (e.Cancelled || e.Error != null)
            {
                return;
            }

            var completedAction = e.Result as Action;
            if (completedAction != null)
            {
                completedAction();
            }
        }

        private void closeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(Resources.CloseAllLoseChangesInquiry, Resources.WarningText, MessageBoxButtons.YesNo) !=
                DialogResult.Yes)
            {
                return;
            }

            PluginList.All.Records.Clear();
            this.PluginTree.UpdateRoots();
            this.SubrecordList.Record = null;
            Clipboard = null;
            this.CloseStringEditor();
            this.UpdateMainText(string.Empty);
            this.RebuildSelection();
            this.PluginTree.UpdateRoots();
            GC.Collect();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.SelectedRecord == null)
            {
                MessageBox.Show(Resources.NoPluginSelectedToSave, Resources.ErrorText);
                return;
            }

            if (MessageBox.Show(Resources.CloseActivePluginInquiry, Resources.WarningText, MessageBoxButtons.YesNo) !=
                DialogResult.Yes)
            {
                return;
            }

            var p = this.GetPluginFromNode(this.PluginTree.SelectedRecord);
            PluginList.All.DeleteRecord(p);
            this.UpdateStringEditor();
            this.UpdateMainText(string.Empty);
            this.FixMasters();
            this.PluginTree.UpdateRoots();
            this.RebuildSelection();
            GC.Collect();
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.PluginTree.CollapseAll();
        }

        private void collapseBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.PluginTree.CollapseAll(this.PluginTree.SelectedRecord);
        }

        private void compressionSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new CompressSettings())
            {
                if (DialogResult.OK == dlg.ShowDialog(this))
                {
                    // nothing of interest
                }
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.CopySelection();
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.Selection.SelectedSubrecord && this.PluginTree.SelectedRecord != null &&
                this.PluginTree.SelectedRecord is Plugin)
            {
                MessageBox.Show(Resources.Cannot_cut_a_plugin, Resources.ErrorText);
                return;
            }

            this.copyToolStripMenuItem_Click(null, null);
            this.deleteToolStripMenuItem_Click(null, null);
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.ContainsFocus)
            {
                this.PluginTree.DeleteSelection();
            }
            else if (this.SubrecordList.ContainsFocus)
            {
                this.SubrecordList.DeleteSelection();
            }
        }

        private void disableHyperlinksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.DisableHyperlinks =
                this.disableHyperlinksToolStripMenuItem.Checked = !this.disableHyperlinksToolStripMenuItem.Checked;
            this.SelectedText.DetectUrls = !Settings.Default.DisableHyperlinks;
        }

        private void eSMFilterSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Update the global list
            bool modified = false;
            var groups = Settings.Default.AllESMRecords != null
                             ? Settings.Default.AllESMRecords.Trim().Split(new[] { ';', ',' },
                                                                           StringSplitOptions.RemoveEmptyEntries).ToList
                                   ()
                             : new List<string>();
            groups.Sort();
            foreach (var plugin in PluginList.All.Records.OfType<Plugin>())
            {
                plugin.ForEach(
                    r =>
                    {
                        if (r is GroupRecord)
                        {
                            var g = (GroupRecord)r;
                            var s = g.ContentsType;
                            if (!string.IsNullOrEmpty(s))
                            {
                                int idx = groups.BinarySearch(s);
                                if (idx < 0)
                                {
                                    groups.Insert(~idx, s);
                                    modified = true;
                                }
                            }
                        }
                    });
            }

            RecordStructure.Load();
            var allRecords = RecordStructure.Records.Select(kvp => kvp.Key).ToList();
            foreach (var str in allRecords)
            {
                int idx = groups.BinarySearch(str);
                if (idx < 0)
                {
                    groups.Insert(~idx, str);
                    modified = true;
                }
            }

            if (modified)
            {
                Settings.Default.AllESMRecords = string.Join(";", groups.ToArray());
            }

            using (var settings = new LoadSettings())
            {
                settings.ShowDialog(this);
            }
        }

        private void editHeaderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.ContainsFocus)
            {
                this.PluginTree.EditSelectedHeader();
            }
            else if (this.SubrecordList.ContainsFocus)
            {
                this.SubrecordList.EditSelectedSubrecordHex();
            }
        }

        private void editSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.ContainsFocus)
            {
                this.PluginTree.EditSelectedRecord();
            }
            else if (this.SubrecordList.ContainsFocus)
            {
                this.SubrecordList.EditSelectedSubrecord();
            }
        }

        private void editStringsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.stringEditor == null)
            {
                var plugins = PluginList.All.Records.OfType<Plugin>().ToList();
                if (plugins.Count == 0)
                {
                    MessageBox.Show(this, "No plugins available to edit", Resources.ErrorText, MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    return;
                }

                this.stringEditor = new StringsEditor();
                this.stringEditor.FormClosed += delegate { this.CloseStringEditor(); };
                this.stringEditor.Plugins = plugins.ToArray();
                this.stringEditor.Show(this); // modeless. Close if the tree is modified.
            }
        }

        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            this.pasteToolStripMenuItem.Enabled = HasClipboardData();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.PluginTree.ExpandAll();
        }

        private void expandBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.PluginTree.ExpandAll(this.PluginTree.SelectedRecord);
        }

        private void findInRecordsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.CreateSearchWindow();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.toolStripIncrFind.Visible)
            {
                this.toolStripIncrFind.Enabled = true;
                this.toolStripIncrFind.Visible = true;
                this.toolStripIncrFind.Focus();
                this.toolStripIncrFindText.Select();
                this.toolStripIncrFindText.SelectAll();
                this.toolStripIncrFindText.Focus();
            }
            else
            {
                this.toolStripIncrFind.Visible = false;
                this.toolStripIncrFind.Enabled = false;
            }
        }

        private void hexModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.UseHexSubRecordEditor = this.hexModeToolStripMenuItem.Checked;
        }

        private void insertGroupToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            var node = this.PluginTree.SelectedRecord;
            var p = new GroupRecord("NEW_");
            node.AddRecord(p);
            this.GetPluginFromNode(this.PluginTree.SelectedRecord).InvalidateCache();
            this.PluginTree.RefreshObject(node);
        }

        private void insertRecordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = this.PluginTree.SelectedRecord;

            if (node is Record && (node.Parent is GroupRecord || node.Parent is Plugin))
            {
                node = node.Parent;
            }

            var record = new Record();
            if (node is GroupRecord)
            {
                var g = (GroupRecord)node;
                if (g.groupType == 0)
                {
                    record.Name = g.ContentsType;
                }
            }

            node.AddRecord(record);
            Spells.giveRecordNewFormID(record, false);
            this.GetPluginFromNode(this.PluginTree.SelectedRecord).InvalidateCache();
            this.PluginTree.RefreshObject(node);
        }

        private void insertSubrecordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BaseRecord node = this.PluginTree.SelectedRecord;
            var p = new SubRecord();
            node.AddRecord(p);
            this.GetPluginFromNode(this.PluginTree.SelectedRecord).InvalidateCache();
            this.PluginTree.RefreshObject(node);
            this.RebuildSelection();
        }

        private void languageToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            foreach (var kvp in this.languageToolBarItems)
            {
                if (e.ClickedItem == kvp.Value)
                {
                    if (Settings.Default.LocalizationName != kvp.Key)
                    {
                        Settings.Default.LocalizationName = kvp.Key;
                        this.ReloadLanguageFiles();
                    }

                    break;
                }
            }
        }

        private void languageToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            foreach (var kvp in this.languageToolBarItems)
            {
                kvp.Value.Checked =
                    string.Compare(kvp.Key, Settings.Default.LocalizationName, StringComparison.OrdinalIgnoreCase) == 0;
            }
        }

        private void lookupFormidsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.lookupFormidsToolStripMenuItem.Checked)
            {
                this.Selection.formIDLookup = this.LookupFormIDI;
                this.Selection.strLookup = this.LookupFormStrings;
                this.Selection.formIDLookupR = this.GetRecordByID;
            }
            else
            {
                this.Selection.formIDLookup = null;
                this.Selection.strLookup = null;
                this.Selection.formIDLookupR = null;
            }
        }

        private void mergeRecordsXMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Records baseRecords;
            Records updateRecords;

            var xs = new XmlSerializer(typeof(Records));
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Base Record Structure";
                dlg.InitialDirectory = Options.Value.SettingsDirectory;
                dlg.FileName = "RecordStructure.xml";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                using (FileStream fs = File.OpenRead(dlg.FileName))
                {
                    baseRecords = xs.Deserialize(fs) as Records;
                }
            }

            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Record Structure XML To Merge";
                dlg.InitialDirectory = Path.GetTempPath();
                dlg.FileName = "RecordStructure.xml";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                using (FileStream fs = File.OpenRead(dlg.FileName))
                {
                    updateRecords = xs.Deserialize(fs) as Records;
                }
            }

            if (updateRecords != null && baseRecords != null)
            {
                var builder = new RecordBuilder();
                builder.MergeRecords(baseRecords.Items.OfType<RecordsRecord>(),
                                     updateRecords.Items.OfType<RecordsRecord>());

                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "Select Record Structure To Save";
                    dlg.InitialDirectory = Path.GetTempPath();
                    dlg.FileName = "RecordStructure.xml";
                    dlg.OverwritePrompt = false;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        using (StreamWriter fs = File.CreateText(dlg.FileName))
                        {
                            xs.Serialize(fs, updateRecords);
                        }
                    }
                }
            }
        }

        private void newFormIDNoReferenceUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.giveSelectionNewFormID(false);
        }

        private void newFormIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.giveSelectionNewFormID(true);
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var p = new Plugin();
            PluginList.All.AddRecord(p);
            var r = new Record();
            r.Name = "TES4";
            var sr = new SubRecord();
            sr.Name = "HEDR";
            sr.SetData(new byte[] { 0xD7, 0xA3, 0x70, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1D, 0x00, 0x01 });
            r.AddRecord(sr);
            sr = new SubRecord();
            sr.Name = "CNAM";
            sr.SetData(Framework.Services.Encoding.Instance.GetBytes("Default\0"));
            r.AddRecord(sr);
            p.AddRecord(r);

            this.RebuildSelection();
            this.UpdateStringEditor();
            this.FixMasters();
            this.PluginTree.UpdateRoots();
        }

        private void noWindowsSoundsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.NoWindowsSounds =
                this.noWindowsSoundsToolStripMenuItem.Checked = !this.noWindowsSoundsToolStripMenuItem.Checked;
        }

        private void openNewPluginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.OpenModDialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadPluginFromListOfFileNames(this.OpenModDialog.FileNames);
            }
        }

        private void LoadPluginFromListOfFileNames(string[] fileNames)
        {
            try
            {
                Stopwatch sw = Stopwatch.StartNew();

                foreach (string s in fileNames)
                {
                    this.LoadPlugin(s);
                    mruMenu.AddFileAndSaveToRegistry(s);
                }

                this.FixMasters();
                this.PluginTree.UpdateRoots();

                sw.Stop();
                TimeSpan t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
                toolStripStatusLabel.Text =
                    string.Format(TranslateUI.TranslateUiGlobalization.ResManager.GetString("MSG_LoadPluginIn"), t.ToString());
            }
            catch (Exception ex)
            {
                string errMsg =
                    "Message: " + ex.Message +
                    Environment.NewLine +
                    Environment.NewLine +
                    "StackTrace: " + ex.StackTrace +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Source: " + ex.Source +
                    Environment.NewLine +
                    Environment.NewLine +
                    "GetType: " + ex.GetType().ToString();

                System.Windows.Forms.Clipboard.SetDataObject(errMsg, true);

                // Create an EventLog instance and assign its source.
                EventLog myLog = new EventLog();
                myLog.Source = "ThreadException";
                myLog.WriteEntry(errMsg);

                MessageBox.Show(errMsg, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
        }

        private void pasteNewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.PasteFromClipboard(false, true);
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.PasteFromClipboard(false, false);
        }

        private void reloadStringsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ReloadLanguageFiles();
        }

        private void reloadXmlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                RecordStructure.Load();
                foreach (var rec in PluginList.All.Enumerate(x => x is Record).OfType<Record>())
                {
                    rec.MatchRecordStructureToRecord();
                }

                this.RebuildSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Resources.CannotParseRecordStructure + ex.Message, Resources.WarningText);
            }
        }

        private void resetDockingWindowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.LayoutDockingWindows(force: true);
        }

        private void rtfInfo_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            {
                var m = linkRegex.Match(e.LinkText);
                if (m.Success)
                {
                    BaseRecord startNode = null;
                    var pluginName = m.Groups["plugin"].Value;
                    if (!string.IsNullOrEmpty(pluginName))
                    {
                        startNode = PluginList.All.Records.OfType<BaseRecord>().FirstOrDefault(x => x.Name == pluginName);
                    }

                    startNode = startNode ?? this.PluginTree.SelectedRecord ?? this.PluginTree.TopRecord;

                    // System.Windows.Forms.Application.
                    // Search current plugin and then wrap around.  
                    // Should do it based on master plugin list first.
                    var type = m.Groups["type"].Value;
                    var searchContext = new SearchSettings();
                    searchContext.rectype = type == "XXXX" ? null : type;
                    searchContext.text = m.Groups["id"].Value;
                    searchContext.type = SearchType.FormID;
                    searchContext.startNode = startNode;
                    searchContext.wrapAround = true;
                    searchContext.partial = false;
                    searchContext.forward = true;
                    searchContext.first = true;
                    uint formID = 0;
                    uint.TryParse(m.Groups["id"].Value, NumberStyles.HexNumber, null, out formID);

                    if (ModifierKeys == Keys.Control)
                    {
                        // Cursor.Position
                        var contextMenu = new ContextMenu();
                        contextMenu.MenuItems.Add(
                            "&Find In Tree",
                            (o, args) =>
                            {
                                var node = this.PerformSearch(searchContext);
                                if (node != null)
                                {
                                    this.PluginTree.SelectedRecord = node;
                                }
                            });
                        contextMenu.MenuItems.Add("Find &References", (o, args) => this.ReferenceSearch(formID));
                        contextMenu.Show(this, PointToClient(MousePosition));
                    }
                    else
                    {
                        var node = this.PerformSearch(searchContext);
                        if (node != null)
                        {
                            this.PluginTree.SelectedRecord = node;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.SelectedRecord == null)
            {
                MessageBox.Show(Resources.NoPluginSelectedToSave, Resources.ErrorText);
                return;
            }

            var p = this.GetPluginFromNode(this.PluginTree.SelectedRecord);
            if (p.Filtered)
            {
                DialogResult result = MessageBox.Show(
                    this, Resources.SavePluginWithFilterAppliedInquiry, Resources.WarningText, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No)
                {
                    return;
                }
            }

            this.SaveModDialog.FileName = p.Name;
            if (this.SaveModDialog.ShowDialog(this) == DialogResult.OK)
            {
                Stopwatch sw = Stopwatch.StartNew();
                p.Save(this.SaveModDialog.FileName);
                mruMenu.AddFileAndSaveToRegistry(this.SaveModDialog.FileName);
                this.FixMasters();
                sw.Stop();
                TimeSpan t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
                toolStripStatusLabel.Text =
                    string.Format(TranslateUI.TranslateUiGlobalization.ResManager.GetString("MSG_SavePluginIn"), t.ToString());
            }
        }

        private void saveStringsFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.SaveStringsFiles =
                this.saveStringsFilesToolStripMenuItem.Checked = !this.saveStringsFilesToolStripMenuItem.Checked;
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.SelectedRecord == null)
            {
                MessageBox.Show(Resources.NoPluginSelectedToSave, Resources.ErrorText);
                return;
            }

            var p = this.GetPluginFromNode(this.PluginTree.SelectedRecord);
            if (p.Filtered)
            {
                DialogResult result = MessageBox.Show(
                    this, Resources.SavePluginWithFilterAppliedInquiry, Resources.WarningText, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No)
                {
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(p.PluginPath)) p.PluginPath = Options.Value.GameDataDirectory;
            string pluginFilPath = Path.Combine(p.PluginPath, p.Name);
            p.Save(pluginFilPath);
            mruMenu.AddFileAndSaveToRegistry(Path.Combine(p.PluginPath, p.Name));
            this.FixMasters();
        }

        private void searchAdvancedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RecordStructure recStruct = null;
            var rec = this.PluginTree.SelectedRecord;
            if (rec is Record)
            {
                RecordStructure.Records.TryGetValue(rec.Name, out recStruct);
            }

            if (recStruct == null)
            {
                recStruct = RecordStructure.Records.Values.Random(RecordStructure.Records.Count).First();
            }

            using (var dlg = new SearchFilterAdvanced(recStruct))
            {
                dlg.ShowDialog(this);
            }
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var settings = this.searchToolStripMenuItem.Tag as SearchCriteriaSettings;
            using (var dlg = new SearchFilterBasic())
            {
                RecordStructure recStruct = null;
                if (settings != null)
                {
                    if (RecordStructure.Records.TryGetValue(settings.Type, out recStruct))
                    {
                        dlg.SetRecordStructure(recStruct);
                        dlg.Criteria = settings;
                    }
                }

                if (recStruct == null)
                {
                    var rec = this.PluginTree.SelectedRecord;
                    if (rec is GroupRecord)
                    {
                        var gr = rec as GroupRecord;
                        var ct = gr.ContentsType;
                        if (!string.IsNullOrEmpty(ct))
                        {
                            RecordStructure.Records.TryGetValue(ct, out recStruct);
                        }
                    }
                    else if (rec is Record)
                    {
                        RecordStructure.Records.TryGetValue(rec.Name, out recStruct);
                    }

                    dlg.SetRecordStructure(recStruct);
                }

                dlg.EnableFindAll(false); // hide final all since we will open 
                if (DialogResult.Cancel != dlg.ShowDialog(this))
                {
                    this.searchToolStripMenuItem.Tag = dlg.Criteria;
                    var window = this.CreateSearchWindow();
                    window.SetSearchCriteria(dlg.Criteria, doSearch: true);
                }
            }
        }

        private void subrecordPanel_DataChanged(object sender, EventArgs e)
        {
            var sr = this.SubrecordList.GetSelectedSubrecord();
            if (sr != null)
            {
                this.UpdateMainText(sr);
            }
        }

        private void subrecordPanel_OnSubrecordChanged(object sender, RecordChangeEventArgs e)
        {
            if (e.Record is SubRecord)
            {
                if (e.Record.Parent is Record)
                {
                    this.PluginTree.RefreshObject(e.Record.Parent);
                }

                this.SubrecordList.RefreshObject(e.Record);
            }
        }

        private void subrecordPanel_SelectionChanged(object sender, EventArgs e)
        {
            this.UpdateMainText(this.SubrecordList.SubRecord);
        }

        private void tbInfo_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            var tbBase = sender as TextBoxBase;
            if (tbBase != null)
            {
                if (e.Control)
                {
                    if (e.KeyCode == Keys.A)
                    {
                        tbBase.SelectAll();
                    }
                    else if (e.KeyCode == Keys.C)
                    {
                        tbBase.Copy();
                    }
                }
            }
        }

        private void toolStripCheck_CheckStateChanged(object sender, EventArgs e)
        {
            var button = sender as ToolStripButton;
            button.Image = button.Checked ? Resources.checkedbox : Resources.emptybox;
        }

        private void toolStripIncrInvalidRecCancel_Click(object sender, EventArgs e)
        {
            this.toolStripIncrInvalidRec.Visible = false;
            this.toolStripIncrInvalidRec.Enabled = false;
        }

        private void toolStripIncrInvalidRecNext_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.SelectedRecord == null)
            {
                return;
            }

            this.BackgroundNonConformingRecordIncrementalSearch(this.PluginTree.SelectedRecord, true,
                                                                this.toolStripIncrInvalidRecWrapAround.Checked);
        }

        private void toolStripIncrInvalidRecPrev_Click(object sender, EventArgs e)
        {
            if (this.PluginTree.SelectedRecord == null)
            {
                return;
            }

            this.BackgroundNonConformingRecordIncrementalSearch(this.PluginTree.SelectedRecord, false,
                                                                this.toolStripIncrInvalidRecWrapAround.Checked);
        }

        private void toolStripIncrInvalidRecRestart_Click(object sender, EventArgs e)
        {
            var rec = PluginList.All.Records.OfType<BaseRecord>().FirstOrDefault();
            if (rec == null)
            {
                return;
            }

            this.BackgroundNonConformingRecordIncrementalSearch(rec, true, false);
        }

        private void toolStripIncrInvalidRec_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
        }

        private void toolStripIncrInvalidRec_VisibleChanged(object sender, EventArgs e)
        {
            this.findNonconformingRecordToolStripMenuItem.Checked = this.toolStripIncrInvalidRec.Visible;
            this.toolStripIncrInvalidRecStatus.Text = "Select Next or Prev to start search.";
            this.toolStripIncrInvalidRecStatus.ForeColor = Color.DarkGray;
        }

        private void toolStripStopProgress_Click(object sender, EventArgs e)
        {
            this.CancelBackgroundProcess();
        }

        private void toolsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
        }

        private void uTF8ModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.UseUTF8 = this.uTF8ModeToolStripMenuItem.Checked;
            if (MessageBox.Show(Resources.RestartText, Resources.InfoText, MessageBoxButtons.YesNoCancel) ==
                DialogResult.Yes)
            {
                Application.Restart();
            }
        }

        private void useNewSubrecordEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.UseOldSubRecordEditor = !this.useNewSubrecordEditorToolStripMenuItem.Checked;
        }

        private void useWindowsClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.UseWindowsClipboard =
                this.useWindowsClipboardToolStripMenuItem.Checked = !this.useWindowsClipboardToolStripMenuItem.Checked;
        }

        public class MainViewMessageFilter : IMessageFilter
        {
            public const int WM_CHAR = 0x102;

            public const int WM_KEYDOWN = 0x100;

            public const int WM_KEYUP = 0x101;

            private const ushort KEY_PRESSED = 0x8000;

            private readonly MainView owner = null;

            public MainViewMessageFilter(MainView owner)
            {
                this.owner = owner;
            }

            internal enum VirtualKeyStates : int
            {
                VK_LBUTTON = 0x01,

                VK_RBUTTON = 0x02,

                VK_CANCEL = 0x03,

                VK_MBUTTON = 0x04,

                VK_LSHIFT = 0xA0,

                VK_RSHIFT = 0xA1,

                VK_LCONTROL = 0xA2,

                VK_RCONTROL = 0xA3,

                VK_LMENU = 0xA4,

                VK_RMENU = 0xA5,

                VK_LEFT = 0x25,

                VK_UP = 0x26,

                VK_RIGHT = 0x27,

                VK_DOWN = 0x28,

                VK_SHIFT = 0x10,

                VK_CONTROL = 0x11,

                VK_MENU = 0x12,
            }

            [DllImport("user32.dll")]
            public static extern ushort GetAsyncKeyState(VirtualKeyStates nVirtKey);

            [DllImport("user32.dll")]
            public static extern ushort GetKeyState(VirtualKeyStates nVirtKey);

            public static bool IsAltDown()
            {
                return 1 == GetKeyState(VirtualKeyStates.VK_MENU);
            }

            public static bool IsControlDown()
            {
                return 1 == GetKeyState(VirtualKeyStates.VK_CONTROL);
            }

            public static bool IsShiftDown()
            {
                return 1 == GetKeyState(VirtualKeyStates.VK_SHIFT);
            }

            public bool PreFilterMessage(ref Message m)
            {
                try
                {
                    return this.owner.PreFilterMessage(ref m);
                }
                catch
                {
                }

                return true;
            }
        }

        /// <summary>
        /// Event for MRU List
        /// </summary>
        /// <param name="number"></param>
        /// <param name="filename"></param>
        private void OnMruFile(int number, String filename)
        {
            if (System.IO.File.Exists(filename))
            {
                string[] fileNames = new string[] { filename };
                mruMenu.SetFirstFile(number);
                this.Update();
                LoadPluginFromListOfFileNames(fileNames);
            }
            else
            {
                string msg = string.Format(TranslateUI.TranslateUiGlobalization.ResManager.GetString("UI_MRU_FileNotExist"),
                                           filename);
                MessageBox.Show(msg, "Tesvsnip", MessageBoxButtons.OK, MessageBoxIcon.Error);
                mruMenu.RemoveFile(number);
            }
        }

        private void resetSettingsToDefaultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration conf = System.Configuration.ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            string pathUserConfig = conf.FilePath;
            try
            {
                if (File.Exists(pathUserConfig))
                {
                    File.Delete(pathUserConfig);
                    Application.Restart();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Tesvsnip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class DialGroupInfo
    {
        public Record Dial { get; set; }
        public GroupRecord GroupInfo { get; set; }
        public Record Info { get; set; }
        public uint BNAMFormID { get; set; }
        public uint QNAMFormID { get; set; }
        public string QNAMName { get; set; }
    }
}
