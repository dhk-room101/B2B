namespace TESVSnip.UI.ObjectControls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Media;
    using System.Text;
    using System.Timers;
    using System.Windows.Forms;
    using System.Xml.Linq;
    using BrightIdeasSoftware;
    using Microsoft.VisualBasic.FileIO;
    using TESVSnip.Domain.Data.RecordStructure;
    using TESVSnip.Domain.Model;
    using TESVSnip.Framework.Collections;
    using TESVSnip.Properties;
    using TESVSnip.UI.Docking;
    using TESVSnip.UI.Forms;

    using WeifenLuo.WinFormsUI.Docking;

    using Settings = TESVSnip.Properties.Settings;
    using TypeConverter = TESVSnip.Framework.TypeConverter;

    public partial class RecordSearch : UserControl, ISupportInitialize
    {
        private static readonly string[] groupingColumns = new[] { "Type", "Plugin" };

        private volatile bool backgroundWorkCanceled;

        private OLVColumn[] baseColumns = null;

        private static System.Timers.Timer aTimer;

        private bool bUpdated = false;
        private int nCounter = 0;

        private MRUComboHelper<SearchType, string> searchTypeItem = null;

        private XElement xContent = null;
        private XDocument xdDocument = null;

        private Plugin pSource = null;
        private Plugin pDest = null;

        /*if current conversation has more than 1 start branch w/o conditions*/
        private bool BoolBug = false;

        private bool BoolNPC_ = false;
        private bool BoolINFO = false;
        private bool BoolQUST = false;
        private bool BoolQUSTDialog = false;
        private bool BoolDialog = true;

        private bool BoolBranchAlready = false;

        private FConversation CurrentConversation = null;
        //private DialogConnector CurrentLine = new DialogConnector();

        private List<FConvNode> ArrConvProcessedNodes = new List<FConvNode>();
        private Dictionary<string, Record> ArrDictConversationRecords = new Dictionary<string, Record>();

        private string CurrentQuestFormID = string.Empty;
        private string CurrentQuestName = string.Empty;
        private string CurrentConditionFlag = string.Empty;

        List<DialogConnector> StartLines = new List<DialogConnector>();
        List<DialogConnector> NPCLines = new List<DialogConnector>();
        List<DialogConnector> PlayerLines = new List<DialogConnector>();

        //TODO save to file and load later
        private Dictionary<string, string> ArrDictFNV64 = new Dictionary<string, string>();

        private Dictionary<string, uint> ArrDictViews = new Dictionary<string, uint>();
        private Dictionary<string, uint> ArrDictBranches = new Dictionary<string, uint>();
        private Dictionary<string, uint> ArrDictTopics = new Dictionary<string, uint>();
        //the topic that owns the group: INFO FormID->DIAL Descriptive Name
        private Dictionary<uint, string> ArrDictInfoToDial = new Dictionary<uint, string>();
        //the info owned by the group
        private Dictionary<string, uint> ArrDictGroupToInfo = new Dictionary<string, uint>();

        private Dictionary<int, DialGroupInfo> ArrDictStartBranches = new Dictionary<int, DialGroupInfo>();
        private List<Record> ArrConversationViews = new List<Record>();
        private List<Record> ArrConversationBranches = new List<Record>();
        private List<DialGroupInfo> ArrConversationBranchesTopic = new List<DialGroupInfo>();
        private List<DialGroupInfo> ArrConversationTopicHellos = new List<DialGroupInfo>();

        private Dictionary<string, string> ArrConversationSpeakers = new Dictionary<string, string>();

        private Dictionary<string, SubRecord> ArrDictSubRecords = new Dictionary<string, SubRecord>();
        private Dictionary<string, List<SubRecord>> ArrDictSubRecordsSpecial = new Dictionary<string, List<SubRecord>>();

        private List<BaseRecord> ArrDialogViews = new List<BaseRecord>();
        private List<BaseRecord> ArrDialogBranches = new List<BaseRecord>();
        private List<BaseRecord> ArrDialogTopics = new List<BaseRecord>();
        private List<BaseRecord> DialogInfos = new List<BaseRecord>();

        private Dictionary<int, string> ArrDictQuestDAO = new Dictionary<int, string>();
        //converted from DAO to SKY
        private Dictionary<int, string> ArrDictQuestSkyrim = new Dictionary<int, string>();

        private Dictionary<uint, int> ArrDictDialogIDSkyToDao = new Dictionary<uint, int>();

        private Dictionary<string, int> ArrDictQuestCounter = new Dictionary<string, int>();
        private Dictionary<string, uint> ArrDictQuestAndViewDone = new Dictionary<string, uint>();
        private Dictionary<string, uint> ArrDictBranchesDone = new Dictionary<string, uint>();

        //reset every conversation to save mem
        private List<BaseRecord> ArrViewsForQuests = new List<BaseRecord>();
        private List<Record> ArrBranchesForView = new List<Record>();
        private List<BaseRecord> ArrDialAndInfo = new List<BaseRecord>();

        private List<string> ArrDialogsWithDefaults = new List<string>();

        private Dictionary<int, FConversation> ArrConversations = new Dictionary<int, FConversation>();
        private Dictionary<int, ConversationOwners> ArrConversationOwners = new Dictionary<int, ConversationOwners>();

        public RecordSearch()
        {
            this.InitializeComponent();
        }

        private enum SearchType
        {
            EditorID,

            FormID,

            FullSearch,

            TypeEditorIdSearch,

            TypeFullSearch,

            FormIDRef,

            BasicCriteriaRef,
        }

        public void BeginInit()
        {
        }

        public void EndInit()
        {
            if (!DesignMode)
            {
                this.InitializeToolStripFind();
            }
        }

        public void FocusText()
        {
            this.toolStripIncrFindText.Focus();
        }

        [Browsable(false)]
        public void StartBackgroundWork(Action workAction, Action completedAction)
        {
            if (this.backgroundWorker1.IsBusy)
                return;

            // EnableUserInterface(false);
            this.backgroundWorkCanceled = false;

            if (!(TopLevelControl as MainView).BoolMove)
                this.toolStripIncrFindCancel.Visible = true;

            this.backgroundWorker1.RunWorkerAsync(new[] { workAction, completedAction });
        }

        internal void ReferenceSearch(uint formid)
        {
            var cboItem = this.toolStripIncrFindType.Items.OfType<MRUComboHelper<SearchType, string>>().FirstOrDefault(x => x.Key == SearchType.FormIDRef);
            this.toolStripIncrFindType.SelectedItem = cboItem;
            this.toolStripIncrFindText.Text = formid.ToString("X8");
            this.BackgroundIncrementalSearch();
        }

        internal void SetSearchCriteria(SearchCriteriaSettings settings, bool doSearch)
        {
            this.toolStripIncrSelectCriteria.Tag = settings;

            var cboItem = this.toolStripIncrFindType.Items.OfType<MRUComboHelper<SearchType, string>>().FirstOrDefault(x => x.Key == SearchType.BasicCriteriaRef);
            this.toolStripIncrFindType.SelectedItem = cboItem;
            if (doSearch)
            {
                this.BackgroundIncrementalSearch();
            }
        }

        private void ApplyColumnSettings(ColumnSettings columnSettings, bool rebuild)
        {
            // remove all of the old columns
            bool changed = false;
            foreach (var oldColumn in this.listSearchView.AllColumns.Where(x => (x.Tag is ColumnElement)).ToList())
            {
                this.listSearchView.AllColumns.Remove(oldColumn);
                changed = true;
            }

            if (columnSettings != null)
            {
                foreach (var setting in columnSettings.Items.OfType<ColumnElement>())
                {
                    string type = setting.Parent.Record.name;
                    string name = setting.Name;
                    string colName = type + "." + name;
                    string dispName = type + ": " + name;
                    if (Enumerable.OfType<OLVColumn>(this.baseColumns).Any(x => x.Name == colName))
                    {
                        continue;
                    }

                    var column = new OLVColumn
                    {
                        Text = dispName,
                        Name = colName,
                        AspectName = setting.Name,
                        Width = 80,
                        IsVisible = true,
                        Groupable = true,
                        Tag = setting,
                        AspectGetter = x =>
                        {
                            var rec = x as Record;
                            var sr = rec != null ? rec.SubRecords.FirstOrDefault(r => r.Name == type) : null;
                            var se = sr != null ? sr.EnumerateElements().FirstOrDefault(e => e.Structure.name == name) : null;
                            return se != null ? sr.GetDisplayValue(se) : null;
                        }
                    };
                    this.listSearchView.AllColumns.Add(column);
                    changed = true;
                }
            }

            if (changed && rebuild)
            {
                this.listSearchView.RebuildColumns();
            }
        }

        private void BackgroundIncrementalSearch()
        {
            if (this.backgroundWorker1.IsBusy)
                return;

            if (!PluginList.All.Enumerate(x => x != null).Any())
            {
                this.toolStripIncrFindStatus.Text = Resources.No_Plugins_Loaded;
                this.toolStripIncrFindStatus.ForeColor = Color.Maroon;
                if (!Settings.Default.NoWindowsSounds)
                {
                    SystemSounds.Beep.Play();
                }

                return;
            }

            var searchText = (TopLevelControl as MainView).TextToFind;// this.toolStripIncrFindText.Text;
            if (searchTypeItem == null)
                searchTypeItem = this.toolStripIncrFindType.SelectedItem as MRUComboHelper<SearchType, string>;

            if (searchTypeItem == null)
            {
                return;
            }

            searchTypeItem.MRU.Remove(searchText);
            searchTypeItem.MRU.Insert(0, searchText);
            this.toolStripIncrFindStatus.Text = string.Empty;
            float totalNodes = PluginList.All.Enumerate(x => x != null).Count();
            int currentCount = 0, prevCount = 0;
            Predicate<BaseRecord> updateFunc = n =>
            {
                if (this.IsBackroundProcessCanceled())
                {
                    // returning true will stop it
                    return false;
                }

                var counter = (int)(++currentCount / totalNodes * 100.0f);
                if (counter != prevCount)
                {
                    prevCount = counter;
                    if (counter % 10 == 0)
                    {
                        this.UpdateBackgroundProgress(counter);
                    }
                }

                return true;
            };

            var searchContext = new SearchSettings();
            searchContext.Type = searchTypeItem.Key;
            searchContext.Text = (TopLevelControl as MainView).TextToFind;// this.toolStripIncrFindText.Text;
            searchContext.Partial = !this.toolStripIncrFindExact.Checked;
            searchContext.Rectype = this.toolStripIncrFindTypeFilter.SelectedItem as string;
            searchContext.Criteria = this.toolStripIncrSelectCriteria.Tag as SearchCriteriaSettings;
            searchContext.UpdateFunc = updateFunc;

            // exclude null Text searches except for when type is specified
            if (searchContext.Type == SearchType.BasicCriteriaRef)
            {
                if (searchContext.Criteria == null || !searchContext.Criteria.Items.Any())
                {
                    if (!Settings.Default.NoWindowsSounds)
                    {
                        SystemSounds.Beep.Play();
                    }

                    MainView.PostStatusWarning("No search criteria selected!");
                }
            }
            else if (searchContext.Type != SearchType.TypeEditorIdSearch && string.IsNullOrEmpty(searchContext.Text))
            {
                if (!Settings.Default.NoWindowsSounds)
                {
                    SystemSounds.Beep.Play();
                }

                this.toolStripIncrFind.Focus();
                this.toolStripIncrFindText.Select();
                this.toolStripIncrFindText.Focus();
                return;
            }

            this.listSearchView.ClearObjects();

            SearchResults results = null;
            this.StartBackgroundWork(
                () =>
                {
                    results = new SearchResults
                    {
                        Type = searchContext.Type,
                        Partial = searchContext.Partial,
                        Rectype = searchContext.Rectype,
                        Text = searchContext.Text,
                        Criteria = searchContext.Criteria,
                        Records = new AdvancedList<Record>(this.PerformSearch(searchContext))
                    };
                },
                () =>
                {
                    if (this.IsBackroundProcessCanceled())
                    {
                        this.toolStripIncrFindStatus.Text = "Search Canceled";
                        this.toolStripIncrFindStatus.ForeColor = Color.Black;
                    }
                    else
                    {
                        if (results != null && results.Records != null && results.Records.Count > 0)
                        {
                            this.toolStripIncrFindStatus.Text = string.Format(Resources.SearchProgressChanged_Items_Found, results.Records.Count);
                            this.toolStripIncrFindStatus.ForeColor = Color.Black;
                            this.toolStripIncrFindText.Tag = false;
                            this.UpdateSearchList(results);
                        }
                        else
                        {
                            this.toolStripIncrFindText.Tag = true;
                            this.toolStripIncrFindStatus.Text = "No Matches Found";
                            this.toolStripIncrFindStatus.ForeColor = Color.Maroon;
                            if (!Settings.Default.NoWindowsSounds)
                            {
                                SystemSounds.Beep.Play();
                            }
                        }

                        if (!(this.TopLevelControl as MainView).BoolMove)
                        {
                            this.toolStripIncrFind.Focus();
                            this.toolStripIncrFindText.Select();
                            this.toolStripIncrFindText.Focus();
                        }
                    }
                });
        }

        private void BatchEditSelectedRecords()
        {
            var selRec = this.listSearchView.SelectedObjects.OfType<Record>();
            using (var dlg = new BatchEditRecords(selRec))
            {
                if (DialogResult.OK == dlg.ShowDialog(this))
                {
                    BatchEditRecords.EditRecords(selRec, dlg.Criteria); // generate report of changes?
                    this.listSearchView.RebuildColumns();
                }
            }
        }

        private void CancelBackgroundProcess()
        {
            this.backgroundWorkCanceled = true;
            this.backgroundWorker1.CancelAsync();
        }

        private void EditSelectedRecords()
        {
            var dockParent = this.FindDockContent(this);
            var dockPanel = dockParent != null ? dockParent.DockHandler.DockPanel : null;
            bool first = true;
            foreach (var r in this.listSearchView.SelectedObjects.OfType<Record>())
            {
                var form = new FullRecordEditor(r);
                if (dockParent != null)
                {
                    var sz = form.Size;
                    if (first)
                    {
                        form.StartPosition = FormStartPosition.CenterScreen;
                        form.Show(dockPanel, DockState.Float);
                        form.Pane.FloatWindow.Size = sz;
                        first = false;
                    }
                    else
                    {
                        form.Show(dockPanel);
                    }
                }
                else
                {
                    form.Show(this);
                }
            }
        }

        private IDockContent FindDockContent(Control c)
        {
            if (c is IDockContent)
            {
                return c as IDockContent;
            }
            else if (c.Parent != null)
            {
                return this.FindDockContent(c.Parent);
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

            return null;
        }

        private ICollection<Record> IncrementalSearch(Predicate<BaseRecord> searchFunc)
        {
            return PluginList.All.Enumerate(searchFunc).OfType<Record>().Take(Settings.Default.MaxSearchResults).ToList();
        }

        private void InitializeToolStripFind()
        {
            if (DesignMode)
            {
                return;
            }

            if (Settings.Default.SearchMRUNameList == null)
            {
                Settings.Default.SearchMRUNameList = new StringCollection();
            }

            if (Settings.Default.SearchMRUFormIDList == null)
            {
                Settings.Default.SearchMRUFormIDList = new StringCollection();
            }

            if (Settings.Default.SearchMRUFullList == null)
            {
                Settings.Default.SearchMRUFullList = new StringCollection();
            }

            var items = new object[]
                {
                    new MRUComboHelper<SearchType, string>(SearchType.EditorID, "Editor ID", Settings.Default.SearchMRUNameList),
                    new MRUComboHelper<SearchType, string>(SearchType.FormID, "Form ID", Settings.Default.SearchMRUFormIDList),
                    new MRUComboHelper<SearchType, string>(SearchType.FullSearch, "Full Search", Settings.Default.SearchMRUFullList),
                    new MRUComboHelper<SearchType, string>(SearchType.TypeEditorIdSearch, "Name w/Type", Settings.Default.SearchMRUNameList),
                    new MRUComboHelper<SearchType, string>(SearchType.TypeFullSearch, "Full w/Type", Settings.Default.SearchMRUFullList),
                    new MRUComboHelper<SearchType, string>(SearchType.FormIDRef, "Form ID Ref.", Settings.Default.SearchMRUFormIDList),
                    new MRUComboHelper<SearchType, string>(SearchType.BasicCriteriaRef, "Basic Search", new StringCollection()),
                };
            this.toolStripIncrFindType.Items.Clear();
            this.toolStripIncrFindType.Items.AddRange(items);

            int idx = 0;
            if (!string.IsNullOrEmpty(Settings.Default.LastSearchType))
            {
                idx = this.toolStripIncrFindType.FindStringExact(Settings.Default.LastSearchType);
            }

            idx = idx >= 0 ? idx : 0;
            this.toolStripIncrFindType.SelectedIndex = idx;

            this.ResetSearch();
            this.toolStripIncrFindStatus.Text = string.Empty;
            if (!RecordStructure.Loaded)
            {
                RecordStructure.Load();
            }

            this.toolStripIncrFindTypeFilter.Sorted = true;
            this.toolStripIncrFindTypeFilter.Items.Clear();
            if (RecordStructure.Records != null)
            {
                var recitems = RecordStructure.Records.Keys.OfType<object>().ToArray();
                this.toolStripIncrFindTypeFilter.Items.AddRange(recitems);
            }

            this.toolStripIncrFindTypeFilter.SelectedIndex = 0;

            this.backgroundWorker1.DoWork += this.BackgroundWorker1_DoWork;
            this.backgroundWorker1.RunWorkerCompleted += this.BackgroundWorker1_RunWorkerCompleted;
            this.backgroundWorker1.ProgressChanged += this.BackgroundWorker1_ProgressChanged;
        }

        private bool IsBackroundProcessCanceled()
        {
            return this.backgroundWorkCanceled;
        }

        /// <summary>
        /// Helper routine for doing an actual search.
        /// </summary>
        /// <param name="ctx">
        /// </param>
        /// <returns>
        /// The System.Collections.Generic.ICollection`1[T -&gt; TESVSnip.Model.Record].
        /// </returns>
        private ICollection<Record> PerformSearch(SearchSettings ctx)
        {
            Predicate<BaseRecord> searchFunction = null;

            switch (ctx.Type)
            {
                case SearchType.FormID:
                    {
                        if (string.IsNullOrEmpty(ctx.Text))
                        {
                            return null;
                        }

                        uint searchID;
                        if (!uint.TryParse(ctx.Text, NumberStyles.AllowHexSpecifier, null, out searchID))
                        {
                            MainView.PostStatusWarning("Invalid FormID");
                            return null;
                        }

                        searchFunction = node =>
                        {
                            var rec = node as Record;
                            if (rec == null)
                            {
                                return node is IGroupRecord;
                            }

                            if (ctx.UpdateFunc != null && !ctx.UpdateFunc(node))
                            {
                                return false;
                            }

                            return rec.FormID == searchID;
                        };
                    }

                    break;
                case SearchType.TypeEditorIdSearch:
                case SearchType.EditorID:
                    {
                        if (ctx.Type == SearchType.TypeEditorIdSearch && string.IsNullOrEmpty(ctx.Rectype))
                        {
                            return null;
                        }

                        if (ctx.Type == SearchType.EditorID && string.IsNullOrEmpty(ctx.Text))
                        {
                            return null;
                        }

                        string searchString = string.IsNullOrEmpty(ctx.Text) ? null : ctx.Text.ToLowerInvariant();
                        searchFunction = node =>
                        {
                            if (ctx.UpdateFunc != null && !ctx.UpdateFunc(node))
                            {
                                return false;
                            }

                            var rec = node as Record;
                            if (rec == null)
                            {
                                return node is IGroupRecord;
                            }

                            bool typeOk = true;
                            if (ctx.Type == SearchType.TypeEditorIdSearch)
                            {
                                typeOk = !string.IsNullOrEmpty(rec.Name) && string.Compare(rec.Name, ctx.Rectype, true) == 0;
                            }

                            if (typeOk)
                            {
                                if (string.IsNullOrEmpty(searchString))
                                {
                                    return true;
                                }
                                else if (ctx.Partial)
                                {
                                    var val = rec.DescriptiveName.ToLowerInvariant();
                                    if (val.Contains(searchString))
                                    {
                                        return true;
                                    }
                                }
                                else
                                {
                                    var val = rec.DescriptiveName.ToLowerInvariant().Substring(2, rec.DescriptiveName.Length - 3);

                                    if (val == searchString)
                                    {
                                        return true;
                                    }
                                }
                            }

                            return false;
                        };
                    }

                    break;
                case SearchType.TypeFullSearch:
                case SearchType.FullSearch:
                    {
                        if (ctx.Type == SearchType.TypeFullSearch && string.IsNullOrEmpty(ctx.Rectype))
                        {
                            return null;
                        }

                        if (ctx.Type == SearchType.FullSearch && string.IsNullOrEmpty(ctx.Text))
                        {
                            return null;
                        }

                        string searchString = ctx.Text.ToLowerInvariant();
                        searchFunction = node =>
                        {
                            if (ctx.UpdateFunc != null && !ctx.UpdateFunc(node))
                            {
                                return false;
                            }

                            var rec = node as Record;
                            if (rec == null)
                            {
                                return node is IGroupRecord;
                            }

                            bool typeOk = true;
                            if (ctx.Type == SearchType.TypeFullSearch)
                            {
                                typeOk = !string.IsNullOrEmpty(rec.Name) && string.Compare(rec.Name, ctx.Rectype, true) == 0;
                            }

                            if (typeOk)
                            {
                                foreach (SubRecord sr in rec.SubRecords)
                                {
                                    var val = sr.GetStrData();
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        val = val.ToLowerInvariant();
                                        if ((ctx.Partial && val.Contains(searchString)) || (val == searchString))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }

                            return false;
                        };
                    }

                    break;
                case SearchType.FormIDRef:
                    {
                        if (string.IsNullOrEmpty(ctx.Text))
                        {
                            return null;
                        }

                        uint searchID;
                        if (!uint.TryParse(ctx.Text, NumberStyles.AllowHexSpecifier, null, out searchID))
                        {
                            MainView.PostStatusWarning("Invalid FormID");
                            return null;
                        }

                        searchFunction = node =>
                        {
                            if (ctx.UpdateFunc != null && !ctx.UpdateFunc(node))
                            {
                                return false;
                            }

                            var rec = node as Record;
                            if (rec == null)
                            {
                                return node is IGroupRecord;
                            }

                            if (rec != null)
                            {
                                rec.MatchRecordStructureToRecord();
                                if (
                                    (from sr in rec.SubRecords from elem in rec.EnumerateElements(sr) let es = elem.Structure where es != null && es.type == ElementValueType.FormID select elem).Any(
                                        elem => searchID == TypeConverter.h2i(elem.Data)))
                                {
                                    return true;
                                }
                            }

                            return false;
                        };
                    }

                    break;

                case SearchType.BasicCriteriaRef:
                    {
                        if (ctx.Criteria == null || !ctx.Criteria.Items.Any())
                        {
                            MainView.PostStatusWarning("No search criteria selected!");
                            return null;
                        }

                        searchFunction = node =>
                        {
                            if (ctx.UpdateFunc != null && !ctx.UpdateFunc(node))
                            {
                                return false;
                            }

                            var rec = node as Record;
                            if (rec == null)
                            {
                                return node is IGroupRecord;
                            }

                            if (ctx.Criteria.Type != rec.Name)
                            {
                                return false;
                            }

                            rec.MatchRecordStructureToRecord();
                            bool all = false;
                            foreach (var m in ctx.Criteria.Items)
                            {
                                bool ok = m.Match(rec);
                                if (!ok)
                                {
                                    return false;
                                }

                                all = true;
                            }

                            return all;
                        };
                    }

                    break;
            }

            return this.IncrementalSearch(searchFunction);
        }

        private void RecordSearch_Load(object sender, EventArgs e)
        {
        }

        private void ResetSearch()
        {
            // use tag to indicate Text changed and therefore reset the search
            this.toolStripIncrFindText.Tag = true;
            this.toolStripIncrFindStatus.Text = string.Empty;
        }

        private void SynchronizeSelection()
        {
            MainView.SynchronizeSelection(this.listSearchView.SelectedObjects.OfType<BaseRecord>());
        }

        private void UpdateBackgroundProgress(int percentProgress)
        {
            this.backgroundWorker1.ReportProgress(percentProgress);
        }

        private void UpdateSearchList(SearchResults results)
        {
            if ((this.TopLevelControl as MainView).BoolMove)
            {
                if (results.Records.Count == 1)
                {
                    if (!(TopLevelControl as MainView).ArrRecordsToMove.Contains(results.Records[0]))
                        (TopLevelControl as MainView).ArrRecordsToMove.Add(results.Records[0]);
                    bUpdated = true;
                }
                else
                    throw new NotImplementedException();
            }

            if (!(TopLevelControl as MainView).BoolMove)
            {
                this.listSearchView.Columns.Clear();
                this.listSearchView.AllColumns.Clear();
                this.listSearchView.ClearObjects();
            }

            if (results == null)
            {
                return;
            }

            foreach (var rec in results.Records)
            {
                rec.MatchRecordStructureToRecord();
            }

            var fixedColumns =
                new List<OLVColumn>(
                    new[]
                        {
                            new OLVColumn
                                {
                                   Text = "Plugin", Name = "Plugin", AspectName = "Plugin", Width = 5, IsVisible = true, Groupable = true, AspectGetter = x => this.GetPluginFromNode(x as Record)
                                },
                            new OLVColumn
                                {
                                    Text = "Type",
                                    Name = "Type",
                                    AspectName = "Type",
                                    Width = 100,
                                    IsVisible = true,
                                    AspectGetter = x => {
                                        var rec = x as Record;
                                        return rec != null ? rec.Name : string.Empty;
                                    }
                                }, new OLVColumn
                                    {
                                        Text = "Name",
                                        Name = "Name",
                                        AspectName = "Name",
                                        Width = 200,
                                        IsVisible = true,
                                        Groupable = true,
                                        AspectGetter = x => {
                                            var rec = x as Record;
                                            var sr = rec != null ? rec.SubRecords.FirstOrDefault(r => r.Name == "EDID") : null;
                                            var elem = sr != null ? sr.GetStrData() : null;
                                            return elem ?? string.Empty;
                                        }
                                    },
                            new OLVColumn
                                {
                                    Text = "FormID",
                                    Name = "FormID",
                                    AspectName = "FormID",
                                    Width = 80,
                                    IsVisible = true,
                                    Groupable = true,
                                    AspectGetter = x => {
                                        var rec = x as Record;
                                        return rec != null ? rec.FormID.ToString("X8") : string.Empty;
                                    }
                                }, new OLVColumn
                                    {
                                        Text = "Full Name",
                                        Name = "FullName",
                                        AspectName = "FullName",
                                        Width = 200,
                                        IsVisible = true,
                                        Groupable = true,
                                        AspectGetter = x => {
                                            var rec = x as Record;
                                            var sr = rec != null ? rec.SubRecords.FirstOrDefault(r => r.Name == "FULL") : null;
                                            var elem = sr != null ? sr.GetLString() : null;
                                            return elem ?? string.Empty;
                                        }
                                    },
                        });

            var columns = new List<ColumnCriteria>();

            // Get custom columns
            if (results.Type == SearchType.BasicCriteriaRef)
            {
                if (results.Criteria != null)
                {
                    fixedColumns.AddRange(
                        from item in results.Criteria.Items.OfType<SearchElement>()
                        let type = item.Parent.Record.name
                        let name = item.Name
                        let colName = type + "." + name
                        let dispName = type + ": " + name
                        select new OLVColumn
                        {
                            Text = dispName,
                            Name = colName,
                            AspectName = name,
                            Width = 80,
                            IsVisible = true,
                            Groupable = true,
                            AspectGetter = x =>
                            {
                                var rec = x as Record;
                                var sr = rec != null ? rec.SubRecords.FirstOrDefault(r => r.Name == type) : null;
                                var se = sr != null ? sr.EnumerateElements().FirstOrDefault(e => e.Structure.name == name) : null;
                                return se != null ? sr.GetDisplayValue(se) : null;
                            }
                        });
                }
            }

            this.baseColumns = fixedColumns.ToArray();
            this.listSearchView.AllColumns.AddRange(this.baseColumns);

            var columnSettings = this.toolStripSelectColumns.Tag as ColumnSettings;
            this.ApplyColumnSettings(columnSettings, rebuild: false);

            if (!(TopLevelControl as MainView).BoolMove)
            {
                this.listSearchView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

                this.listSearchView.Objects = results.Records;
                this.listSearchView.RebuildColumns();
                this.listSearchView.Refresh();
            }
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var actions = e.Argument as Action[];
            if (actions == null)
            {
                return;
            }

            if (actions.Length > 0)
            {
                actions[0]();
            }

            if (actions.Length > 1)
            {
                e.Result = actions[1];
            }
        }

        private void BackgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.toolStripIncrFindStatus.Text = string.Format("{0}% Complete", e.ProgressPercentage);
        }

        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int nItems = this.listSearchView.GetItemCount();
            bool maxResultsHit = nItems == Settings.Default.MaxSearchResults;
            string text = string.Format(Resources.SearchProgressChanged_Items_Found, nItems);
            if (maxResultsHit)
            {
                text += " (Max Limited)";
            }

            if (!(TopLevelControl as MainView).BoolMove)
            {
                this.toolStripIncrFindCancel.Visible = false;
                this.toolStripIncrFindStatus.Text = text;
                this.toolStripIncrFindStatus.ForeColor = SystemColors.ControlText;
            }

            if (e.Cancelled || e.Error != null)
            {
                return;
            } (e.Result as Action)?.Invoke();
        }

        private void BatchEditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.BatchEditSelectedRecords();
        }

        private void ContextMenuStripList_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            this.copyToToolStripMenuItem.DropDownItems.Clear();
        }

        private void ContextMenuStripList_Opening(object sender, CancelEventArgs e)
        {
            if (!(this.toolStripIncrFindType.SelectedItem is MRUComboHelper<SearchType, string> searchTypeItem))
            {
                return;
            }

            var records = this.listSearchView.SelectedObjects.OfType<Record>().ToList();
            var hasDistictRecordsType = records.Select(x => x.Name).Distinct().Count() == 1;
            bool anyRecords = records.Any();

            if (hasDistictRecordsType)
            {
                this.batchEditToolStripMenuItem.Enabled = true;
                this.batchEditToolStripMenuItem.ToolTipText = string.Empty;
            }
            else
            {
                this.batchEditToolStripMenuItem.Enabled = false;
                this.batchEditToolStripMenuItem.ToolTipText = string.Format("Batch Edit not allowed when multiple record types are selected");
            }

            this.copyToToolStripMenuItem.Enabled = anyRecords;
            this.editToolStripMenuItem.Enabled = anyRecords;
            this.synchronizeToolStripMenuItem.Enabled = anyRecords;
            this.batchEditToolStripMenuItem.Enabled &= anyRecords;
            this.copyToolStripMenuItem.Enabled = anyRecords;

            if (this.copyToToolStripMenuItem.Enabled)
            {
                foreach (var plugin in PluginList.All.Records.OfType<Plugin>())
                {
                    Plugin curplugin = plugin;
                    var copyRecs = records.Where(x => x.GetPlugin() != curplugin);
                    if (!copyRecs.Any())
                    {
                        continue;
                    }

                    var tsi = new ToolStripButton(plugin.Name)
                    {
                        Tag = new object[] { copyRecs.OfType<BaseRecord>().ToArray(), curplugin }
                    };
                    var sz = TextRenderer.MeasureText(plugin.Name, this.copyToToolStripMenuItem.Font);
                    if (sz.Width > tsi.Width)
                    {
                        tsi.Width = sz.Width;
                    }

                    tsi.AutoSize = true;
                    this.copyToToolStripMenuItem.DropDownItems.Add(tsi);
                }
            }
        }

        private void CopyToToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem.Tag is object[] array && array.Length == 2)
            {
                int count = Spells.CopyRecordsTo(array[0] as BaseRecord[], array[1] as IGroupRecord);
            }
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainView.Clipboard = this.listSearchView.SelectedObjects.OfType<BaseRecord>().ToArray();
        }

        private void EditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.EditSelectedRecords();
        }

        private void ListSearchView_BeforeCreatingGroups(object sender, CreateGroupsEventArgs e)
        {
            try
            {
                if (e.Parameters.GroupByColumn != null)
                {
                    if (!groupingColumns.Contains(e.Parameters.GroupByColumn.Name))
                    {
                        var column = this.listSearchView.AllColumns.FirstOrDefault(x => x.Name == "Plugin");
                        if (column != null)
                        {
                            e.Parameters.GroupByColumn = column;
                            e.Parameters.SortItemsByPrimaryColumn = false;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void ListSearchView_BeforeSorting(object sender, BeforeSortingEventArgs e)
        {
        }

        private void ListSearchView_CellClick(object sender, CellClickEventArgs e)
        {
            if (e.ClickCount > 1)
            {
                this.SynchronizeSelection();
            }
        }

        private void ListSearchView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Control && !e.Alt && !e.Shift)
            {
                this.SynchronizeSelection();
            }
        }

        private void reportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dockParent = this.FindDockContent(this);

            foreach (Record r in this.listSearchView.SelectedObjects.OfType<Record>())
            {
                var form = new RichTextContent();
                form.UpdateRecord(r);
                form.StartPosition = FormStartPosition.CenterScreen;
                if (dockParent != null)
                {
                    var sz = form.Size;
                    form.Show(dockParent.DockHandler.DockPanel, DockState.Float);
                    form.Pane.FloatWindow.Size = sz;
                }
                else
                {
                    form.Show(this);
                }
            }
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ResetSearch();
        }

        private void synchronizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.SynchronizeSelection();
        }

        private void toolStripCheck_CheckStateChanged(object sender, EventArgs e)
        {
            var button = sender as ToolStripButton;
            if (button != null)
            {
                button.Image = button.Checked ? Resources.checkedbox : Resources.emptybox;
            }
        }

        private void toolStripIncrFindCancel_Click(object sender, EventArgs e)
        {
            this.CancelBackgroundProcess();
        }

        private void toolStripIncrFindClear_Click(object sender, EventArgs e)
        {
            this.listSearchView.ClearObjects();
        }

        private void toolStripIncrFindGo_Click(object sender, EventArgs e)
        {
            this.BackgroundIncrementalSearch();
        }

        private void toolStripIncrFindText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)Keys.Enter)
            {
                if (!(TopLevelControl as MainView).BoolMove)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    this.BackgroundIncrementalSearch();
                }
                else
                {
                    (TopLevelControl as MainView).CounterFormID = Convert.ToUInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[0]);
                    (TopLevelControl as MainView).CounterTalk = Convert.ToInt32(File.ReadLines(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt").First().Split(',')[1]);
                    (TopLevelControl as MainView).ArrRecordsToMove = new List<Record>();

                    var AllPlugins = PluginList.All.Records.OfType<BaseRecord>();

                    StreamReader _fileReader = null;
                    if (BoolNPC_)
                        _fileReader = File.OpenText(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\DAO\apps.csv");
                    else if (BoolINFO)
                        _fileReader = File.OpenText(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\DAO\ItemsG.csv");
                    else if (BoolQUST || BoolQUSTDialog)
                        _fileReader = File.OpenText(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\DAO\StagesDAO.csv");
                    else if (BoolDialog)
                        _fileReader = File.OpenText(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\DAO\Dialog.csv");

                    string _line;
                    while ((_line = _fileReader.ReadLine()) != null)
                    {
                        string[] splits = _line.Split(',');

                        if (!(TopLevelControl as MainView).ArrTemplates.Contains(splits[2].Split('_')[1].ToUpper()))
                            (TopLevelControl as MainView).ArrTemplates.Add(splits[2].Split('_')[1].ToUpper());

                        if (!(TopLevelControl as MainView).ArrDictNamesSource.ContainsKey(splits[0]))
                            (TopLevelControl as MainView).ArrDictNamesSource.Add(splits[0], splits[2].Split('_')[1].ToUpper());
                    }

                    e.SuppressKeyPress = true;
                    e.Handled = true;

                    bUpdated = true;
                    nCounter = (TopLevelControl as MainView).ArrTemplates.Count;

                    DoSearch();
                }
            }
        }

        private void GetDialogs(Plugin _plugin)
        {
            //get DLVW/DLBR/DIAL/INFO dictionaries per FormID/Descriptive Name
            foreach (var g in _plugin.Records)
            {
                if (g is GroupRecord && (g as GroupRecord).ContentsType == "DLVW")
                {
                    foreach (var r in (g as GroupRecord).Records)
                    {
                        if (r is GroupRecord)
                            Console.WriteLine();

                        string s = (r as Record).DescriptiveName.Split(' ')[1].TrimStart('(').TrimEnd(')');
                        ArrDictViews.Add(s, (r as Record).FormID);
                    }
                }

                if (g is GroupRecord && (g as GroupRecord).ContentsType == "DLBR")
                {
                    foreach (var r in (g as GroupRecord).Records)
                    {
                        if (r is GroupRecord)
                            Console.WriteLine();

                        string s = (r as Record).DescriptiveName.Split(' ')[1].TrimStart('(').TrimEnd(')');
                        ArrDictBranches.Add(s, (r as Record).FormID);
                    }
                }

                if (g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL")
                {
                    foreach (var r in (g as GroupRecord).Records)
                    {
                        string sDial = string.Empty;
                        if (!(r is GroupRecord))
                        {
                            sDial = (r as Record).DescriptiveName.Split(' ')[1].TrimStart('(').TrimEnd(')');
                            ArrDictTopics.Add(sDial, (r as Record).FormID);
                        }
                        else
                        {
                            foreach (var rr in (r as GroupRecord).Records)
                                ArrDictInfoToDial.Add((rr as Record).FormID, sDial);
                        }
                    }
                }
            }
        }

        private void DoSearch()
        {
            if (bUpdated)
            {
                (TopLevelControl as MainView).TextToFind = (TopLevelControl as MainView).ArrTemplates[(TopLevelControl as MainView).ArrTemplates.Count - 1];
                (TopLevelControl as MainView).ArrTemplates.RemoveAt((TopLevelControl as MainView).ArrTemplates.Count - 1);
                bUpdated = false;
            }

            this.BackgroundIncrementalSearch();

            if (aTimer == null)
                SetTimer();
        }

        private void SetTimer()
        {
            aTimer = new System.Timers.Timer(1000);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Start();
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            if (!this.backgroundWorker1.IsBusy)
            {
                if ((TopLevelControl as MainView).ArrTemplates.Count == 0)
                {
                    aTimer.Stop();
                    aTimer.Dispose();

                    xContent = new XElement("Content");
                    xdDocument = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xContent);

                    pSource = PluginList.All.Records.OfType<BaseRecord>().ToList()[0] as Plugin;
                    pDest = PluginList.All.Records.OfType<BaseRecord>().ToList()[1] as Plugin;

                    if (BoolNPC_)
                        StartCopyNPC_();
                    else if (BoolINFO)
                        StartCopyINFO();
                    else if (BoolQUST)
                        StartCopyQUST();
                    else if (BoolQUSTDialog)
                        StartCopyQUSTDialog();
                    else if (BoolDialog)
                        StartCopyDialog();

                    //TODO multiple orphan lines per quest as RANDOM entries

                    Console.WriteLine();
                }
                else
                {
                    DoSearch();
                }
            }
        }

        //to create QUST for orphan(no quest condition, usually last line/s) dialog
        private void StartCopyQUSTDialog()
        {
            if ((TopLevelControl as MainView).ArrRecordsToMove.Count != nCounter)
                throw new NotImplementedException();

            //ugly cheat 
            //DAO splits JournalStringID for Stage and Objective
            uint nStringID = 466961;//0x72011
            Dictionary<uint, string> dJournal = new Dictionary<uint, string>();

            string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\DLG_2159Simple\",
                  "*.xml",
                  System.IO.SearchOption.AllDirectories);

            foreach (string _file in files)
            {
                bool bOrphan = false;

                XDocument xdDoc = XDocument.Load(_file);
                CurrentConversation = ParseConversation(xdDoc);

                foreach (int n in CurrentConversation.StartList)
                {
                    FConvNode node = CurrentConversation.NPCLineList[n];
                    if (node.ConditionPlotURI == 0 &&
                        node.ConditionPlotFlag == -1)
                    {
                        //No condition = no owning quest, orphan line, create new quest
                        bOrphan = true;
                        break;
                    }
                }

                BaseRecord newR = null;
                string sQuestName = "";

                if (bOrphan)
                {
                    sQuestName = "Quest_" + CurrentConversation.ResRefName;
                    newR = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse("0007A732", System.Globalization.NumberStyles.HexNumber)).Clone();
                    newR.SetDescription(' ' + sQuestName);

                    foreach (var sr in (newR as Record).SubRecords)
                    {
                        if (sr.Name == "EDID")
                        {
                            byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sQuestName).Length + 1];
                            Array.Copy(Encoding.ASCII.GetBytes(sQuestName), plusNull, Encoding.ASCII.GetBytes(sQuestName).Length);

                            sr.SetData(plusNull);
                            break;
                        }
                    }

                    //increment counter formID
                    (newR as Record).FormID = (TopLevelControl as MainView).CounterFormID;
                    (TopLevelControl as MainView).CounterFormID++;
                    nStringID++;

                    string hexName = nStringID.ToString("X8");
                    string hexName6 = nStringID.ToString("X6");

                    byte[] arrNameFull = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
                    arrNameFull = arrNameFull.Reverse().ToArray();

                    foreach (var sr in (newR as Record).SubRecords)
                    {
                        if (sr.Name == "FULL")
                        {
                            sr.SetData(arrNameFull);

                            XElement xString = new XElement("String");
                            xString.Add(new XAttribute("List", 0));
                            xString.Add(new XAttribute("sID", hexName6));

                            XElement xEdid = new XElement("EDID", sQuestName);
                            xString.Add(xEdid);

                            XElement xRec = new XElement("REC", "QUST" + ':' + sr.Name);
                            xString.Add(xRec);

                            XElement xSource = new XElement("Source", sQuestName);
                            xString.Add(xSource);

                            XElement xDest = new XElement("Dest", sQuestName);
                            xString.Add(xDest);

                            xContent.Add(xString);
                        }
                    }

                    BaseRecord[] record = new BaseRecord[1];
                    record[0] = newR;
                    object[] nodes = new object[2];
                    nodes[0] = record;
                    nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                    int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
                }
            }

            //save counter formID + talkID
            string[] lines = new string[1];
            uint mF = (TopLevelControl as MainView).CounterFormID++;
            lines[0] = mF.ToString();

            using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
            {
                newTask.WriteLine(lines[0].ToString());
            }

            //save skyrim dao talk text
            xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english_DAO.xml");
        }

        private void ParseConversationOwners()
        {
            //load CSV for future
            using (TextFieldParser parser = new TextFieldParser(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\DAO\UTC_To_ConvBoth.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    //Process row
                    string[] fields = parser.ReadFields();

                    ConversationOwners convOwner = new ConversationOwners();
                    convOwner.sSkyrimFormID = fields[1];
                    convOwner.sDAOResRefName = fields[2];

                    ArrConversationOwners.Add(Convert.ToInt32(fields[0]), convOwner);
                }
            }
        }

        private void StartCopyDialog()//DLG first then TODO CUT
        {
            if ((TopLevelControl as MainView).ArrRecordsToMove.Count != nCounter)
                throw new NotImplementedException();

            ParseConversationOwners();//plural

            GetDialogs(pDest);

            ArrDictSubRecords = GetSubrecords();
            ArrDictSubRecordsSpecial = GetSubrecordsSpecial();

            string[] filesUTCConv = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTCConv\",
                                              "*.xml",
                                              System.IO.SearchOption.AllDirectories);

            List<string> files = new List<string>();

            foreach (string _file in filesUTCConv)
            {
                XDocument xdDoc = XDocument.Load(_file);

                string sPath = @"d:\Work\R_idle\DAO_All\DAO_extracted\XML\DLG_2159ResID\";
                string sConv = xdDoc.Root.Descendants("ConversationURI").ToList()[0].Value;

                if (!files.Contains(sPath + sConv + ".xml"))
                    files.Add(sPath + sConv + ".xml");
            }

            /*string[] filesDel = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\DLG_2159ResID\",
                                              "*.xml",
                                              SearchOption.AllDirectories);

            foreach (string _file in filesDel)
            {
                if (!files.Contains(_file))
                    File.Delete(_file);
            }*/

            foreach (string _file in files)
            {
                if (!File.Exists(_file))
                    Console.WriteLine();
            }

            foreach (string _file in files)
            {
                XDocument xdDoc = XDocument.Load(_file);
                CurrentConversation = ParseConversation(xdDoc);

                if (CurrentConversation.ResRefID == 0)
                    throw new NotImplementedException();

                ArrConversations.Add(CurrentConversation.ResRefID, CurrentConversation);

                //CreateDialogViewDLVW();
            }

            bool bGetQuestStart = false;
            if (bGetQuestStart)
                CreateDialogStart();//DLVW per QUST, with DLBR and starting DIAL/Fake INFO

            bool bGetDialogUpdate = true;
            if (bGetDialogUpdate)
                UpdateDialog();//update starting topic and info/s

            //save counter
            string[] lines = new string[1];
            uint counterFormID = (TopLevelControl as MainView).CounterFormID++;
            int counterTalk = (TopLevelControl as MainView).CounterTalk++;
            lines[0] = counterFormID.ToString() + ',' + counterTalk.ToString();

            using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
            {
                newTask.WriteLine(lines[0].ToString());
            }

            //save DialogsWithDefaults for troubleshooting
            if (bGetQuestStart)
            {
                var csv = new StringBuilder();

                foreach (var newLine in ArrDialogsWithDefaults)
                {
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("DialogsWithDefaults.csv", csv.ToString());
            }

            bool bSave = true;
            if (bSave)
            {
                var csvDialog = new StringBuilder();

                foreach (var d in ArrDictDialogIDSkyToDao)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csvDialog.AppendLine(newLine);
                }

                File.WriteAllText("SkyToDaoStringID.csv", csvDialog.ToString());
            }

            //save skyrim dao talk text
            xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english_DAO.xml");

            Console.WriteLine();
        }

        private void UpdateDialog()
        {
            foreach (var c in ArrConversations)
            {
                //TODO one lines maybe as Goodbye/InvisibleContinue/Walkaway

                CurrentConversation = c.Value;

                #region Handle Nodes
                var LinesList = new List<DialogConnector>();

                string sConversationOwner = string.Empty;

                //set local quest = no condition start branch, every conversation
                SetLocalQuest(CurrentConversation);
                if (!ArrDictQuestSkyrim.ContainsKey(0))
                {
                    ArrDictQuestSkyrim.Add(0, ArrDictQuestSkyrim[0]);
                }
                else
                    ArrDictQuestSkyrim[0] = ArrDictQuestSkyrim[0];

                ArrConversationSpeakers = GetConversationSpeakers();
                sConversationOwner = GetConversationOwner();//singular

                //last attempt to avoid error
                if (sConversationOwner == "")
                {
                    sConversationOwner = "bhm600cr_mouse_human";//ugly and manual :(
                }

                #endregion

                //TODO multiple speakers SCEN?

                #region Create Views and Branches
                List<Record> ArrViews = new List<Record>();
                List<Record> ArrBranches = new List<Record>();

                //changes every start branch
                string sQuestFormID = "";
                string sQuestName = "";
                int nBranchOwner = -1;
                Record view = null;
                Record branch = null;
                uint nBranchFormID = 0;

                ///Special: get branch only
                bool bBranch = true;
                int nBranch = -1;
                if (bBranch)
                {
                    /*var si = CurrentConversation.NPCLineList.Where(x => x.Comment ==
                                "IF: the Demon has been defeated, Mouse did not switch sides");*/
                    var si = CurrentConversation.NPCLineList.Where(x => x.text ==
                                "I... have I ever told you I really like the way you wear your hair?");
                    /*var si = CurrentConversation.NPCLineList.Where(x => x.text.Contains(
                                "Magic exists to serve man, and never to rule over him."));*/

                    if (si.ToList().Count != 1)
                        Console.WriteLine();

                    int i = CurrentConversation.NPCLineList.IndexOf(si.ToList()[0]);

                    nBranch = si.ToList()[0].lineIndex;
                }

                //first Start
                for (int i = 0; i < CurrentConversation.NPCLineList.Count; i++)
                {
                    if (!CurrentConversation.StartList.Contains(i))
                        continue;

                    if (bBranch && nBranch > -1)
                    {
                        if (i != nBranch)
                            continue;
                    }

                    var startLine = CurrentConversation.NPCLineList[i];

                    var dc = new DialogConnector
                    {
                        ConversationOwner = sConversationOwner,
                        LineIndex = startLine.lineIndex,
                        LinkToFormID = new Dictionary<uint, string>(),
                        LineNodes = new List<FConvNode>()
                    };

                    dc.LineNodes.Add(startLine);//self at 0

                    nBranchOwner = i;
                    dc.BranchOwner = nBranchOwner;

                    var xd = XDocument.Load(ArrDictQuestSkyrim[startLine.ConditionPlotURI]);
                    sQuestFormID = xd.Root.Attribute("formID").Value;//for short EDID
                    sQuestName = xd.Root.Descendants("Name").ToList()[0].Value;

                    dc.LineDialog = new DialGroupInfo
                    {
                        QNAMFormID = uint.Parse(sQuestFormID, System.Globalization.NumberStyles.HexNumber),
                        QNAMName = sQuestName,
                    };

                    //create view and branch
                    view = CreateDialogViewDLVW(dc);
                    if (!ArrViews.Contains(view))//skip duplicates
                        ArrViews.Add(view);

                    branch = CreateDialogBranchDLBR(dc, view);
                    if (!ArrBranches.Contains(branch))//skip duplicates
                        ArrBranches.Add(branch);

                    nBranchFormID = branch.FormID;

                    //update branch formID
                    dc.LineDialog.BNAMFormID = nBranchFormID;
                    dc.LineDialog.Dial = GetDialFromLine(dc);
                    dc.LineDialog.GroupInfo = GetGroupFromLine(dc);

                    //update branch SNAM
                    if (branch.SubRecords[branch.SubRecords.Count - 1].Name != "SNAM")
                        Console.WriteLine();

                    string hexFormID = dc.LineDialog.Dial.FormID.ToString("X8");

                    byte[] arrFormID = Enumerable.Range(0, hexFormID.Length / 2).Select(x => Convert.ToByte(hexFormID.Substring(x * 2, 2), 16)).ToArray();
                    arrFormID = arrFormID.Reverse().ToArray();

                    branch.SubRecords[branch.SubRecords.Count - 1].SetData(arrFormID);

                    if (dc.LineDialog.BNAMFormID == 0 || dc.LineDialog.QNAMFormID == 0)
                        Console.WriteLine();

                    StartLines.Add(dc);
                }

                if (bBranch && nBranch > -1)
                {
                    if (StartLines.Count != 1)
                        Console.WriteLine();

                    Queue<FConvNode> queue = new Queue<FConvNode>();

                    if (StartLines[0].LineNodes.Count != 1)
                        Console.WriteLine();

                    foreach (var t in StartLines[0].LineNodes[0].TransitionList)
                    {
                        queue.Enqueue(CurrentConversation.PlayerLineList[t.LineIndex]);
                    }

                    while (queue.Count > 0)
                    {
                        // Take the next node from the front of the queue
                        var node = queue.Dequeue();

                        var dc = new DialogConnector
                        {
                            ConversationOwner = sConversationOwner,
                            LineIndex = node.lineIndex,
                            LinkToFormID = new Dictionary<uint, string>(),
                            LineNodes = new List<FConvNode>()
                        };

                        dc.LineNodes.Add(node);//self at 0
                        dc.BranchOwner = StartLines[0].LineIndex;

                        if (IsPlayer(dc))
                        {
                            dc.LineDialog = new DialGroupInfo
                            {
                                BNAMFormID = StartLines[0].LineDialog.BNAMFormID,
                                QNAMFormID = StartLines[0].LineDialog.QNAMFormID,
                                QNAMName = StartLines[0].LineDialog.QNAMName
                            };

                            //update branch formID
                            dc.LineDialog.Dial = GetDialFromLine(dc);
                            dc.LineDialog.GroupInfo = GetGroupFromLine(dc);

                            if (dc.LineDialog.BNAMFormID == 0 || dc.LineDialog.QNAMFormID == 0)
                                Console.WriteLine();

                            foreach (var tp in node.TransitionList)
                            {
                                var nn = CurrentConversation.NPCLineList[tp.LineIndex];
                                if (nn.Speaker != "" && nn.Speaker != "OWNER")
                                {
                                    dc.IsTainted = true;
                                    break;
                                }
                            }

                            PlayerLines.Add(dc);
                        }
                        else
                        {
                            dc.LineDialog = new DialGroupInfo
                            {
                                BNAMFormID = StartLines[0].LineDialog.BNAMFormID,
                                QNAMFormID = StartLines[0].LineDialog.QNAMFormID,
                                QNAMName = StartLines[0].LineDialog.QNAMName
                            };

                            //disable DIAL for NPC

                            if (dc.LineDialog.BNAMFormID == 0 || dc.LineDialog.QNAMFormID == 0)
                                Console.WriteLine();

                            if (node.Speaker != "" && node.Speaker != "OWNER")
                                dc.IsTainted = true;

                            NPCLines.Add(dc);
                        }

                        foreach (var t in dc.LineNodes[0].TransitionList)
                        {
                            if (!t.IsLink)
                            {
                                if (IsPlayer(dc))
                                    queue.Enqueue(CurrentConversation.NPCLineList[t.LineIndex]);
                                else
                                    queue.Enqueue(CurrentConversation.PlayerLineList[t.LineIndex]);
                            }
                        }
                    }
                }
                else //full conversation
                {
                    //then NPC
                    for (int i = 0; i < CurrentConversation.NPCLineList.Count; i++)
                    {
                        if (CurrentConversation.StartList.Contains(i))
                            continue;

                        var npcLine = CurrentConversation.NPCLineList[i];

                        var dc = new DialogConnector
                        {
                            ConversationOwner = sConversationOwner,
                            LineIndex = npcLine.lineIndex,
                            LinkToFormID = new Dictionary<uint, string>(),
                            LineNodes = new List<FConvNode>()
                        };

                        dc.LineNodes.Add(npcLine);//self at 0

                        dc.BranchOwner = GetBranchOwnerNode(dc).lineIndex;

                        var sl = StartLines.Where(x => x.LineIndex == dc.BranchOwner);
                        if (sl.ToList().Count != 1)
                            Console.WriteLine();

                        dc.LineDialog = new DialGroupInfo
                        {
                            BNAMFormID = sl.ToList()[0].LineDialog.BNAMFormID,
                            QNAMFormID = sl.ToList()[0].LineDialog.QNAMFormID,
                            QNAMName = sl.ToList()[0].LineDialog.QNAMName
                        };

                        //disable DIAL for NPC
                        /*//update branch formID
                        dc.LineDialog.Dial = GetDialFromLine(dc);
                        dc.LineDialog.GroupInfo = GetGroupFromLine(dc);*/

                        if (dc.LineDialog.BNAMFormID == 0 || dc.LineDialog.QNAMFormID == 0)
                            Console.WriteLine();

                        NPCLines.Add(dc);
                    }

                    //then Player
                    for (int i = 0; i < CurrentConversation.PlayerLineList.Count; i++)
                    {
                        var playerLine = CurrentConversation.PlayerLineList[i];

                        var dc = new DialogConnector
                        {
                            ConversationOwner = sConversationOwner,
                            LineIndex = playerLine.lineIndex,
                            LinkToFormID = new Dictionary<uint, string>(),
                            LineNodes = new List<FConvNode>()
                        };

                        dc.LineNodes.Add(playerLine);//self at 0

                        dc.BranchOwner = GetBranchOwnerNode(dc).lineIndex;

                        var npcDirectLinkTo = StartLines.Concat(NPCLines).ToList().Where(x =>
                            x.LineNodes[0].TransitionList.Any(y => y.LineIndex == dc.LineIndex && y.IsLink == false));

                        if (npcDirectLinkTo.ToList().Count != 1)
                            Console.WriteLine();

                        dc.LineDialog = new DialGroupInfo
                        {
                            BNAMFormID = npcDirectLinkTo.ToList()[0].LineDialog.BNAMFormID,
                            QNAMFormID = npcDirectLinkTo.ToList()[0].LineDialog.QNAMFormID,
                            QNAMName = npcDirectLinkTo.ToList()[0].LineDialog.QNAMName
                        };

                        //update branch formID
                        dc.LineDialog.Dial = GetDialFromLine(dc);
                        dc.LineDialog.GroupInfo = GetGroupFromLine(dc);

                        if (dc.LineDialog.BNAMFormID == 0 || dc.LineDialog.QNAMFormID == 0)
                            Console.WriteLine();

                        PlayerLines.Add(dc);
                    }
                }
                #endregion

                //update LinkTo and add empty INFO w/ new form ID
                //first Start
                foreach (var nl in StartLines.ToList())
                {
                    int i = StartLines.IndexOf(nl);
                    DialogConnector n = StartLines[i];

                    if (n.LineDialog.Info != null)
                        Console.WriteLine();

                    n.LineDialog.Info = GetEmptyInfo(true);
                    n.LineDialog.GroupInfo.Records.Add(n.LineDialog.Info);

                    foreach (var t in n.LineNodes[0].TransitionList)
                    {
                        var l = PlayerLines.Where(x => x.LineIndex == t.LineIndex);

                        if (l.ToList().Count != 1)
                            Console.WriteLine();

                        if (n.LinkToFormID.ContainsKey(l.ToList()[0].LineDialog.Dial.FormID))
                            Console.WriteLine();

                        //to keep track, which link points where
                        n.LinkToFormID.Add(l.ToList()[0].LineDialog.Dial.FormID,
                            l.ToList()[0].LineNodes[0].text != "" ?
                            l.ToList()[0].LineNodes[0].lineIndex.ToString() + "|" + l.ToList()[0].LineNodes[0].text :
                            l.ToList()[0].LineNodes[0].lineIndex.ToString() + "|" + "CONTINUE");
                    }

                    StartLines[i] = n;
                }

                //then NPC
                foreach (var nl in NPCLines.ToList())
                {
                    int i = NPCLines.IndexOf(nl);
                    DialogConnector n = NPCLines[i];

                    if (n.LineDialog.Info != null)
                        Console.WriteLine();

                    n.LineDialog.Info = GetEmptyInfo(true);
                    //n.LineDialog.GroupInfo.Records.Add(n.LineDialog.Info);

                    foreach (var t in n.LineNodes[0].TransitionList)
                    {
                        var l = PlayerLines.Where(x => x.LineIndex == t.LineIndex);

                        if (l.ToList().Count != 1)
                            Console.WriteLine();

                        if (n.LinkToFormID.ContainsKey(l.ToList()[0].LineDialog.Dial.FormID))
                            Console.WriteLine();

                        //to keep track, which link points where
                        n.LinkToFormID.Add(l.ToList()[0].LineDialog.Dial.FormID,
                            l.ToList()[0].LineNodes[0].text != "" ?
                            l.ToList()[0].LineNodes[0].lineIndex.ToString() + "|" + l.ToList()[0].LineNodes[0].text :
                            l.ToList()[0].LineNodes[0].lineIndex.ToString() + "|" + "CONTINUE");
                    }

                    NPCLines[i] = n;
                }

                //then Player
                foreach (var pl in PlayerLines.ToList())
                {
                    if (pl.IsTainted)
                        continue;

                    int i = PlayerLines.IndexOf(pl);
                    DialogConnector p = PlayerLines[i];

                    if (p.LineDialog.Info != null)
                        Console.WriteLine();

                    //disable own INFO for Player
                    if (p.LineNodes[0].TransitionList.Count == 0)//Goodbye
                    {
                        p.LineDialog.Info = GetEmptyInfo(true);
                        p.LineDialog.GroupInfo.Records.Add(p.LineDialog.Info);
                    }
                    else
                    {
                        foreach (var t in p.LineNodes[0].TransitionList)
                        {
                            //loopback to Start?!?
                            if (CurrentConversation.StartList.Contains(t.LineIndex))
                            {
                                var l = StartLines.Where(x => x.LineIndex == t.LineIndex);

                                if (l.ToList().Count != 1)
                                    Console.WriteLine();

                                if (p.LinkToFormID.ContainsKey(l.ToList()[0].LineDialog.Dial.FormID))
                                    Console.WriteLine();

                                p.LinkToFormID.Add(l.ToList()[0].LineDialog.Dial.FormID,
                                    l.ToList()[0].LineNodes[0].text != "" ? l.ToList()[0].LineNodes[0].text : "CONTINUE");
                            }
                            else
                            {
                                var l = NPCLines.Where(x => x.LineIndex == t.LineIndex);

                                if (l.ToList().Count != 1)
                                    Console.WriteLine();

                                //find non tainted NPC line
                                DialogConnector found = new DialogConnector();
                                bool bTainted = false;

                                foreach (var nt in l.ToList()[0].LineNodes[0].TransitionList)
                                {
                                    var ppp = PlayerLines.Where(x => x.LineIndex == nt.LineIndex);

                                    if (ppp.ToList().Count != 1)
                                        Console.WriteLine();

                                    if (ppp.ToList()[0].IsTainted)
                                    {
                                        bTainted = true;
                                        break;
                                    }
                                }

                                if (bTainted)
                                {
                                    Queue<DialogConnector> queue = new Queue<DialogConnector>();

                                    if (l.ToList()[0].LineNodes[0].TransitionList.Count != 1)
                                        Console.WriteLine();

                                    foreach (var nt in l.ToList()[0].LineNodes[0].TransitionList)
                                    {
                                        var ppp = PlayerLines.Where(x => x.LineIndex == nt.LineIndex);

                                        if (ppp.ToList().Count != 1)
                                            Console.WriteLine();

                                        if (ppp.ToList()[0].IsTainted)
                                        {
                                            queue.Enqueue(ppp.ToList()[0]);
                                        }
                                        else
                                            Console.WriteLine();//?!? I'm tainted, eh?
                                    }

                                    while (queue.Count > 0)
                                    {
                                        var conn = queue.Dequeue();
                                        if (conn.IsTainted)
                                        {
                                            if (IsPlayer(conn))
                                            {
                                                foreach (var pt in conn.LineNodes[0].TransitionList)
                                                {
                                                    var n = NPCLines.Where(x => x.LineIndex == pt.LineIndex);

                                                    if (n.ToList().Count != 1)
                                                        Console.WriteLine();

                                                    if (!n.ToList()[0].IsTainted)
                                                        Console.WriteLine();

                                                    queue.Enqueue(n.ToList()[0]);
                                                }
                                            }
                                            else
                                            {
                                                foreach (var nt in conn.LineNodes[0].TransitionList)
                                                {
                                                    var pp = PlayerLines.Where(x => x.LineIndex == nt.LineIndex);

                                                    if (pp.ToList().Count != 1)
                                                        Console.WriteLine();

                                                    if (!pp.ToList()[0].IsTainted)
                                                    {
                                                        found = conn;//PlayerLines[nt.LineIndex];
                                                        p.NodeSwitch = found.LineNodes[0];

                                                        if (found.LineNodes[0].ConditionPlotURI > 0 ||
                                                            found.LineNodes[0].ActionPlotURI > 0)
                                                            Console.WriteLine();//in case we skip a line w/ Condition/Action

                                                        break;
                                                    }
                                                    else
                                                    {
                                                        queue.Enqueue(pp.ToList()[0]);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (found.LineDialog == null)
                                        Console.WriteLine();
                                }
                                else
                                    found = l.ToList()[0];

                                if (found.LineDialog.Info == null)
                                    Console.WriteLine();

                                if (t.IsLink)
                                {
                                    if (bTainted)
                                        Console.WriteLine();

                                    Record newInfo = l.ToList()[0].LineDialog.Info.Clone() as Record;
                                    newInfo.FormID = GetFormID();
                                    p.LineDialog.GroupInfo.Records.Add(newInfo);

                                }
                                else
                                    p.LineDialog.GroupInfo.Records.Add(l.ToList()[0].LineDialog.Info);

                                p.LineNodes.Add(l.ToList()[0].LineNodes[0]);//Player adds NPC node at 1

                                if (found.LinkToFormID.Count == 0)
                                    Console.WriteLine();

                                foreach (var lt in found.LinkToFormID)
                                {
                                    if (p.LinkToFormID.ContainsKey(lt.Key))
                                        Console.WriteLine();

                                    //already updated at NPC end
                                    //if (!p.LinkToFormID.ContainsKey(lt.Key))
                                    p.LinkToFormID.Add(lt.Key, lt.Value);
                                }
                            }
                        }
                    }

                    PlayerLines[i] = p;
                }

                //update INFO subrecords
                //first Start
                foreach (var nl in StartLines.ToList())
                {
                    int i = StartLines.IndexOf(nl);
                    DialogConnector line = StartLines[i];

                    if (line.LineDialog.GroupInfo.Records.Count != 1)
                        Console.WriteLine();

                    Record info = line.LineDialog.GroupInfo.Records[0] as Record;

                    List<SubRecord> subRecords = GetSubRecordsFromLine(line, 0);//has node at 0
                    foreach (var sr in subRecords)
                    {
                        info.SubRecords.Add(sr.Clone() as SubRecord);
                    }

                    StartLines[i] = line;
                }

                //skip for NPC

                //then Player
                foreach (var pl in PlayerLines.ToList())
                {
                    int i = PlayerLines.IndexOf(pl);
                    DialogConnector line = PlayerLines[i];

                    for (var j = 0; j < line.LineDialog.GroupInfo.Records.Count; j++)
                    {
                        Record info = line.LineDialog.GroupInfo.Records[j] as Record;

                        List<SubRecord> subRecords = GetSubRecordsFromLine(line, j + 1);
                        foreach (var sr in subRecords)
                        {
                            info.SubRecords.Add(sr.Clone() as SubRecord);
                        }
                    }

                    PlayerLines[i] = line;
                }

                //add lines to Copy Array
                foreach (var line in StartLines.ToList().Concat(PlayerLines).ToList())
                {
                    if (line.IsTainted)
                        continue;

                    if (line.LineDialog.Dial == null || line.LineDialog.GroupInfo == null)
                        Console.WriteLine();

                    if (line.LineDialog.GroupInfo.Records.Count == 0)
                        Console.WriteLine();

                    ArrDialAndInfo.Add(line.LineDialog.Dial);
                    ArrDialAndInfo.Add(line.LineDialog.GroupInfo);
                }

                //copy Views
                foreach (var d in ArrViews)
                {
                    string sEdid = System.Text.Encoding.Default.GetString(d.SubRecords[0].GetData());
                    sEdid = sEdid.TrimEnd('\0');

                    if (HasView(sEdid, true) == null)
                    {
                        BaseRecord[] record = new BaseRecord[1];
                        record[0] = d;
                        object[] nodes = new object[2];
                        nodes[0] = record;
                        nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                        int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
                    }
                }

                //and Branches
                foreach (var d in ArrBranches)
                {
                    string sEdid = System.Text.Encoding.Default.GetString(d.SubRecords[0].GetData());
                    sEdid = sEdid.TrimEnd('\0');

                    if (HasBranch(sEdid, true) == null)
                    {
                        BaseRecord[] record = new BaseRecord[1];
                        record[0] = d;
                        object[] nodes = new object[2];
                        nodes[0] = record;
                        nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                        int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
                    }
                }

                //copy dial/group/info
                bool bHasTopic = false;
                foreach (var d in ArrDialAndInfo)
                {
                    if (d is Record)
                    {
                        if ((d as Record).SubRecords[0].Name != "EDID")
                            Console.WriteLine();

                        string sEdid = System.Text.Encoding.Default.GetString((d as Record).SubRecords[0].GetData());
                        sEdid = sEdid.TrimEnd('\0');

                        if (HasTopic(sEdid, true) != null)
                        {
                            bHasTopic = true;
                            continue;
                        }
                    }

                    if (d is GroupRecord && bHasTopic)
                    {
                        bHasTopic = false;
                        continue;
                    }

                    BaseRecord[] record = new BaseRecord[1];
                    record[0] = d;
                    object[] nodes = new object[2];
                    nodes[0] = record;
                    nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                    int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
                }

                var csvFNV64 = new StringBuilder();

                foreach (var d in ArrDictFNV64)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csvFNV64.AppendLine(newLine);
                }

                File.WriteAllText(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\DAO\FNV64_" + CurrentConversation.ResRefName + ".csv", csvFNV64.ToString());

                Console.WriteLine();
            }
        }

        private uint GetFormID()
        {
            uint f = (TopLevelControl as MainView).CounterFormID;
            (TopLevelControl as MainView).CounterFormID++;
            return f;
        }

        private bool IsStartBranch(DialogConnector dc)
        {
            return (CurrentConversation.StartList.Contains(dc.LineIndex) && !IsPlayer(dc));
        }

        private Dictionary<string, string> GetConversationSpeakers()
        {
            Dictionary<string, string> conversationSpeakers = new Dictionary<string, string>();
            string sConversationOwner = string.Empty;

            for (int i = 0; i < CurrentConversation.NPCLineList.Count; i++)
            {
                var nLine = CurrentConversation.NPCLineList[i];
                if (!conversationSpeakers.ContainsKey(nLine.Speaker))
                {
                    if (nLine.Speaker == "" || nLine.Speaker == "OWNER")
                    {
                        if (string.IsNullOrEmpty(sConversationOwner))
                        {
                            string sDao = ArrConversationOwners[CurrentConversation.ResRefID].sDAOResRefName;
                            string sSky = ArrConversationOwners[CurrentConversation.ResRefID].sSkyrimFormID;

                            if (sDao.Contains('|'))//multiple
                            {
                                List<string> arrDao = sDao.Split('|').ToList();
                                List<string> arrSky = sSky.Split('|').ToList();

                                for (int j = 0; j < arrDao.Count; j++)
                                {
                                    if (i < arrDao.Count)
                                    {
                                        if (!conversationSpeakers.ContainsKey(arrDao[i]))
                                        {
                                            conversationSpeakers.Add(arrDao[i], arrSky[i]);
                                        }

                                        sConversationOwner = arrDao[i];
                                    }
                                }
                            }
                            else
                            {
                                if (!conversationSpeakers.ContainsKey(sDao))
                                {
                                    conversationSpeakers.Add(sDao, sSky);
                                }

                                sConversationOwner = sDao;
                            }
                        }
                    }
                    else
                    {
                        string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\NPC_\",
                              "*_" + nLine.Speaker + ".xml",
                              System.IO.SearchOption.TopDirectoryOnly);

                        string sFileToLoad = string.Empty;

                        if (filesDAO.Length != 1)
                        {
                            Console.WriteLine();
                        }
                        else
                        {
                            sFileToLoad = filesDAO[0];

                            var x = XDocument.Load(sFileToLoad);

                            string sName = x.Root.Descendants("Name").ToList()[0].Value;
                            string sFormID = x.Root.Attribute("formID").Value;

                            if (!conversationSpeakers.ContainsKey(sName))
                                conversationSpeakers.Add(sName, sFormID);
                        }
                    }
                }
            }

            return conversationSpeakers;
        }

        private string GetConversationOwner()
        {
            Dictionary<string, string> conversationSpeakers = new Dictionary<string, string>();
            string sConversationOwner = string.Empty;

            for (int i = 0; i < CurrentConversation.NPCLineList.Count; i++)
            {
                var nLine = CurrentConversation.NPCLineList[i];
                if (!conversationSpeakers.ContainsKey(nLine.Speaker))
                {
                    if (nLine.Speaker == "" || nLine.Speaker == "OWNER")
                    {
                        if (string.IsNullOrEmpty(sConversationOwner))
                        {
                            string sDao = ArrConversationOwners[CurrentConversation.ResRefID].sDAOResRefName;
                            string sSky = ArrConversationOwners[CurrentConversation.ResRefID].sSkyrimFormID;

                            if (sDao.Contains('|'))//multiple
                            {
                                List<string> arrDao = sDao.Split('|').ToList();
                                List<string> arrSky = sSky.Split('|').ToList();

                                for (int j = 0; j < arrDao.Count; j++)
                                {
                                    if (i < arrDao.Count)
                                    {
                                        if (!conversationSpeakers.ContainsKey(arrDao[i]))
                                        {
                                            conversationSpeakers.Add(arrDao[i], arrSky[i]);
                                        }

                                        sConversationOwner = arrDao[i];
                                    }
                                }
                            }
                            else
                            {
                                if (!conversationSpeakers.ContainsKey(sDao))
                                {
                                    conversationSpeakers.Add(sDao, sSky);
                                }

                                sConversationOwner = sDao;
                            }
                        }
                    }
                    else
                    {
                        string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\NPC_\",
                              "*_" + nLine.Speaker + ".xml",
                              System.IO.SearchOption.TopDirectoryOnly);

                        string sFileToLoad = string.Empty;

                        if (filesDAO.Length != 1)
                        {
                            Console.WriteLine();
                        }
                        else
                        {
                            sFileToLoad = filesDAO[0];

                            var x = XDocument.Load(sFileToLoad);

                            string sName = x.Root.Descendants("Name").ToList()[0].Value;
                            string sFormID = x.Root.Attribute("formID").Value;

                            if (!conversationSpeakers.ContainsKey(sName))
                                conversationSpeakers.Add(sName, sFormID);
                        }
                    }
                }
            }

            return sConversationOwner;
        }

        private List<SubRecord> GetSubRecordsFromLine(DialogConnector line, int index)
        {
            var listSubRecords = new List<SubRecord>();

            SubRecord srVMAD = null;//  //action
            SubRecord srENAM = null;//
            SubRecord srCNAM = ArrDictSubRecords["CNAM_None"].Clone() as SubRecord;// //always none
            List<SubRecord> TCLTs = new List<SubRecord>();// //multiple
            SubRecord srTRDT = null;//
            SubRecord srNAM1 = null;//
            SubRecord srNAM2 = null;//
            SubRecord srNAM3 = null;//
            List<SubRecord> CTDAs = new List<SubRecord>();//condition //multiple

            //VMAD 
            //TODO if set false
            //like so: check DLG for <ActionResult type="bool">False</ActionResult>
            //get FLAG name, say MORRIGAN_ROMANCE_ACTIVE
            //in SKY quest make 2 flags/stages :
            //MORRIGAN_ROMANCE_ACTIVE_ON and MORRIGAN_ROMANCE_ACTIVE_OFF, or similar

            int nAction = GetNodeActionOwner(line);
            //TODO if 2 line nodes have action, currently only player line action is set
            if (nAction != -1)//has action
            {
                bool bActionResult = line.LineNodes[nAction].ActionResult;

                if (IsExternalAction(line))//objective
                {
                    if (HasObjective(line))
                        srVMAD = ArrDictSubRecords["VMAD_External_Objective_End"].Clone() as SubRecord;
                    else
                        srVMAD = ArrDictSubRecords["VMAD_External_End"].Clone() as SubRecord;

                    byte[] arrSearchExt = new byte[] { 0x67, 0x45, 0x23, 0x01 };
                    byte[] arrReplaceExt = BitConverter.GetBytes
                        (int.Parse(GetCurrentQuest(ArrDictQuestSkyrim[line.LineNodes[nAction].ActionPlotURI]).Root.Attribute("formID").Value,
                        System.Globalization.NumberStyles.HexNumber));

                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearchExt, arrReplaceExt));
                }
                else
                    srVMAD = ArrDictSubRecords["VMAD_Local_End"].Clone() as SubRecord;

                if (!IsPlayer(line))
                    HandleScript(line.LineDialog.Info.FormID.ToString("X8"), line);
                else
                    HandleScript((line.LineDialog.GroupInfo.Records[index - 1] as Record).FormID.ToString("X8"), line);

                byte[] arrSearch = new byte[] { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37 };
                byte[] arrReplace = null;

                if (!IsPlayer(line))
                    arrReplace = Encoding.ASCII.GetBytes(line.LineDialog.Info.FormID.ToString("X8"));
                else
                    arrReplace = Encoding.ASCII.GetBytes((line.LineDialog.GroupInfo.Records[index - 1] as Record).FormID.ToString("X8"));

                //ugly, do it 3 time manually - TODO automatically
                srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
            }

            //ENAM
            //start generic, change if needed
            srENAM = ArrDictSubRecords["ENAM_Simple"].Clone() as SubRecord;

            if (IsGoodbye(line) || IsGoodbyeEmpty(line))
                srENAM = ArrDictSubRecords["ENAM_Goodbye"].Clone() as SubRecord;
            else
            {
                //TODO doublecheck, don't like checks only the first one?!?
                //check for invisible continue
                var next = PlayerLines.Where(x => x.LineDialog.Dial.FormID == line.LinkToFormID.ElementAt(0).Key);

                if (next.ToList().Count != 1)
                    Console.WriteLine();

                if (next.ToList()[0].LineNodes[0].text == "")
                    srENAM = ArrDictSubRecords["ENAM_InvisibleContinue"].Clone() as SubRecord;
            }

            //TCLT
            if (line.LinkToFormID.Count > 0)
            {
                foreach (var tclt in line.LinkToFormID)
                {
                    if (!IsPlayer(line))//skip start branches
                    {
                        var srTclt = ArrDictSubRecords["TCLT"].Clone() as SubRecord;

                        byte[] tcltFormIDBytes = BitConverter.GetBytes(tclt.Key);
                        /*if (BitConverter.IsLittleEndian)
                            Array.Reverse(tcltFormIDBytes);*/

                        srTclt.SetData(tcltFormIDBytes);

                        TCLTs.Add(srTclt);
                    }
                    else
                    {
                        var nodeIndex = int.Parse(tclt.Value.Split('|')[0]);
                        var srTclt = ArrDictSubRecords["TCLT"].Clone() as SubRecord;

                        byte[] tcltFormIDBytes = BitConverter.GetBytes(tclt.Key);
                        /*if (BitConverter.IsLittleEndian)
                            Array.Reverse(tcltFormIDBytes);*/

                        srTclt.SetData(tcltFormIDBytes);

                        if (line.NodeSwitch != null)
                        {
                            foreach (var t in line.NodeSwitch.TransitionList)
                            {
                                if (nodeIndex == t.LineIndex)
                                    TCLTs.Add(srTclt);
                            }
                        }
                        else
                        {
                            foreach (var t in line.LineNodes[index].TransitionList)
                            {
                                if (nodeIndex == t.LineIndex)
                                    TCLTs.Add(srTclt);
                            }
                        }

                    }
                }
            }

            //Response //Skip if no text/comment
            //var r = line.LineNodes.Where(x => !x.isPlayer && x.text != "");
            bool bHasResponse = false;

            if (IsStartBranch(line))
                if (line.LineNodes[0].text != "")
                    bHasResponse = true;

            if (IsPlayer(line))
                if (line.LineNodes[0].TransitionList.Count > 0)
                    if (line.LineNodes[index].text != "")
                        bHasResponse = true;

            if (bHasResponse)
            {
                if (IsStartBranch(line))
                    index = 0;
                else if (IsPlayer(line))
                {
                    /*index = 1;*/
                }
                else Console.WriteLine();

                //for now skip non-owner lines, ugly TODO
                if (line.LineNodes[index].Speaker != "" &&
                    line.LineNodes[index].Speaker != "OWNER")
                    Console.WriteLine();

                //TRDT
                srTRDT = ArrDictSubRecords["TRDT"].Clone() as SubRecord;

                var arr = srTRDT.GetData();
                arr[0] = Convert.ToByte(ConvertEmotions(line));
                srTRDT.SetData(arr);

                //NAM1
                srNAM1 = ArrDictSubRecords["NAM1_w_text"].Clone() as SubRecord;

                string hexName6 = line.LineNodes[0].StringID.ToString("X6");//to use unique ID
                string hexName6npc = line.LineNodes[index].StringID.ToString("X6");
                string hexName8 = "";

                if (!IsPlayer(line))
                    hexName8 = (line.LineDialog.GroupInfo.Records[0] as Record).FormID.ToString("X8");
                else//ugly
                    hexName8 = (line.LineDialog.GroupInfo.Records[index - 1] as Record).FormID.ToString("X8");

                byte[] nam1FormIDBytes = BitConverter.GetBytes(line.LineNodes[index].StringID);
                /*if (BitConverter.IsLittleEndian)
                    Array.Reverse(nam1FormIDBytes);*/

                srNAM1.SetData(nam1FormIDBytes);

                //ugly, not function TODO
                XElement xString = new XElement("String");
                xString.Add(new XAttribute("List", 2));
                xString.Add(new XAttribute("sID", hexName6));
                //added npcStringID to keep track of duplicates while keeping sID unique
                xString.Add(new XAttribute("npcStringID", hexName6npc));

                XElement xEdid = new XElement("EDID", '[' + hexName8 + ']');
                xString.Add(xEdid);

                XElement xRec = new XElement("REC", "INFO" + ':' + srNAM1.Name);
                xString.Add(xRec);

                //add visual hint if ACTION sets follower inc/dec flag as +/- value            
                XElement xSource = new XElement("Source", pSource.tlkDao[line.LineNodes[index].StringID]);
                xString.Add(xSource);

                XElement xDest = new XElement("Dest", pSource.tlkDao[line.LineNodes[index].StringID]);
                xString.Add(xDest);

                xContent.Add(xString);

                //ugly if - needed at all? TODO
                if (!ArrDictDialogIDSkyToDao.ContainsKey((line.LineDialog.GroupInfo.Records[0] as Record).FormID))
                    ArrDictDialogIDSkyToDao.Add((line.LineDialog.GroupInfo.Records[0] as Record).FormID, line.LineNodes[index].StringID);

                //if blank TODO
                //srNAM1 = ArrDictSubRecords["NAM1_wo_text"].Clone() as SubRecord;

                //NAM2
                if (HasComment(line))
                {
                    srNAM2 = ArrDictSubRecords["NAM2_w_text"].Clone() as SubRecord; ;

                    string sComment = !string.IsNullOrEmpty(line.LineNodes[index].VoiceOverComment) ? line.LineNodes[index].VoiceOverComment : line.LineNodes[index].Comment;
                    byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sComment).Length + 1];

                    Array.Copy(Encoding.ASCII.GetBytes(sComment), plusNull, Encoding.ASCII.GetBytes(sComment).Length);

                    srNAM2.SetData(plusNull);
                }
                else
                {
                    srNAM2 = ArrDictSubRecords["NAM2_wo_text"].Clone() as SubRecord; ;
                    srNAM2.SetData(new byte[] { 0x00 });
                }

                //NAM3 Always empty
                srNAM3 = ArrDictSubRecords["NAM3_wo_text"].Clone() as SubRecord; ;
                srNAM3.SetData(new byte[] { 0x00 });
            }

            //CTDA
            SubRecord srCTDA = null;

            foreach (var node in line.LineNodes)
            {
                if (node.ConditionPlotURI > 0)
                {
                    bool bConditionResult = node.ConditionResult;

                    List<SubRecord> specials = GetSpecialConditions(line);

                    if (bConditionResult)//true GetStageDone
                        srCTDA = ArrDictSubRecords["Condition_GetStageDone"].Clone() as SubRecord;
                    else //false GetStageDone Not
                        srCTDA = ArrDictSubRecords["Condition_GetStageDone_NotAnd"].Clone() as SubRecord;

                    //Update QUST FormID
                    var xd = XDocument.Load(ArrDictQuestSkyrim[node.ConditionPlotURI]);

                    string hexFormID = xd.Root.Attribute("formID").Value;
                    string sCondFlag = (node.ConditionPlotFlag * 10).ToString("X8");//TODO 0 * 10 + 1

                    byte[] arrQUST = Enumerable.Range(0, hexFormID.Length / 2).Select(x => Convert.ToByte(hexFormID.Substring(x * 2, 2), 16)).ToArray();
                    arrQUST = arrQUST.Reverse().ToArray();

                    byte[] arrFlag = Enumerable.Range(0, sCondFlag.Length / 2).Select(x => Convert.ToByte(sCondFlag.Substring(x * 2, 2), 16)).ToArray();
                    arrFlag = arrFlag.Reverse().ToArray();

                    byte[] arrReplace = arrQUST.Concat(arrFlag).ToArray();

                    byte[] arrSearch = new byte[] { 0x67, 0x45, 0x23, 0x01, //formID
                                                0x65, 0x00, 0x00, 0x00};//Stage
                    srCTDA.SetData(ReplaceBytes(srCTDA.GetData(), arrSearch, arrReplace));

                    if (specials.Count > 0)
                        CTDAs = CTDAs.Concat(specials).ToList();

                    CTDAs.Add(srCTDA);
                }
            }

            #region GetIsID
            //always GetIsID() is being used in CTDAs for NPC
            //GetIsID And/Or

            if (IsGoodbye(line))
                index = 0;

            //if more NPCs owned the line
            bool bOR = false;
            if (ArrConversationOwners[CurrentConversation.ResRefID].sSkyrimFormID.Contains('|'))
                bOR = true;

            //for now skip non-owner lines, ugly TODO
            /*if (line.LineNodes[index].Speaker == "" ||
                    line.LineNodes[index].Speaker == "OWNER" || //Owner
                    line.LineNodes[index].Speaker == "PLAYER") //Check Player for Goodbye line { }*/
            foreach (var sFormID in ArrConversationOwners[CurrentConversation.ResRefID].sSkyrimFormID.Split('|'))
            {
                if (bOR)
                    srCTDA = ArrDictSubRecords["Condition_GetIsID_Or"].Clone() as SubRecord;
                else
                    srCTDA = ArrDictSubRecords["Condition_GetIsID_And"].Clone() as SubRecord;

                var nOwnerFormID = uint.Parse(sFormID, System.Globalization.NumberStyles.HexNumber);
                string hexFormID = nOwnerFormID.ToString("X8");

                byte[] arrNPC_FormID = Enumerable.Range(0, hexFormID.Length / 2).Select(x => Convert.ToByte(hexFormID.Substring(x * 2, 2), 16)).ToArray();
                arrNPC_FormID = arrNPC_FormID.Reverse().ToArray();

                byte[] arrSearch = new byte[] { 0x67, 0x45, 0x23, 0x01 };
                srCTDA.SetData(ReplaceBytes(srCTDA.GetData(), arrSearch, arrNPC_FormID));

                CTDAs.Add(srCTDA);
            }
            /*else
            {
                //TODO SNAM Type = SCEN for non-owner speaker
                //for now skip, should never hit
                srCTDA = ArrDictSubRecords["Condition_GetIsID_And"].Clone() as SubRecord;

                if (!ArrConversationSpeakers.ContainsKey(line.LineNodes[index].Speaker))
                    Console.WriteLine();

                string hexFormID = ArrConversationSpeakers[line.LineNodes[index].Speaker];

                byte[] arrNPC_FormID = Enumerable.Range(0, hexFormID.Length / 2).Select(x => Convert.ToByte(hexFormID.Substring(x * 2, 2), 16)).ToArray();
                arrNPC_FormID = arrNPC_FormID.Reverse().ToArray();

                byte[] arrSearch = new byte[] { 0x67, 0x45, 0x23, 0x01 };
                srCTDA.SetData(ReplaceBytes(srCTDA.GetData(), arrSearch, arrNPC_FormID));

                CTDAs.Add(srCTDA);
            }*/
            #endregion

            //add the non-null results to the SubRecord list
            if (srVMAD != null)
                listSubRecords.Add(srVMAD);

            if (srENAM != null)
                listSubRecords.Add(srENAM);

            if (srCNAM != null)//always != , enjoy redundance :D
                listSubRecords.Add(srCNAM);

            if (TCLTs.Count > 0)
            {
                foreach (var tclt in TCLTs)
                    listSubRecords.Add(tclt);
            }

            if (srTRDT != null)
                listSubRecords.Add(srTRDT);

            if (srNAM1 != null)
                listSubRecords.Add(srNAM1);

            if (srNAM2 != null)
                listSubRecords.Add(srNAM2);

            if (srNAM3 != null)
                listSubRecords.Add(srNAM3);

            if (CTDAs.Count > 0)
            {
                foreach (var ctda in CTDAs)
                    listSubRecords.Add(ctda);
            }

            return listSubRecords;
        }

        private int GetNodeActionOwner(DialogConnector line)
        {
            //TODO currently returns only the first action found, update the logic

            if (line.LineNodes.Count == 0)
                Console.WriteLine();

            List<int> actions = new List<int>();
            for (int i = 0; i < line.LineNodes.Count; i++)
            {
                var node = line.LineNodes[i];
                if (node.ActionPlotURI > 0)
                    actions.Add(i);
            }

            return actions.Count == 0 ? -1 : actions[0];//return first
        }

        private string IsApprovalAction(DialogConnector line)
        {
            if (!HasAction(line))
                return "";

            int nAction = line.LineNodes[0].ActionPlotURI;
            int nActionFlag = line.LineNodes[0].ActionPlotFlag;

            if (nAction == 999 ||//Alistair
                nAction == 1011 ||//Dog
                nAction == 1006 ||//Leliana
                nAction == 1012 ||//Loghain
                nAction == 1005 ||//Morrigan
                nAction == 1007 ||//Oghren
                nAction == 1008 ||//Shale
                nAction == 1009 ||//Sten
                nAction == 1010 ||//Wynne
                nAction == 1004)  //Zevran
            {
                switch (nActionFlag)
                {
                    case 0: return " (+1)";//APP_FOLLOWER_INC_VLOW
                    case 1: return " (+2)";//APP_FOLLOWER_INC_LOW
                    case 2: return " (+4)";//APP_FOLLOWER_INC_MED
                    case 3: return " (+7)";//APP_FOLLOWER_INC_HIGH
                    case 4: return " (+12)";//APP_FOLLOWER_INC_VHIGH
                    case 5: return " (+20)";//APP_FOLLOWER_INC_EXTREME
                    case 6: return " (-1)";//APP_FOLLOWER_DEC_VLOW
                    case 7: return " (-3)";//APP_FOLLOWER_DEC_LOW
                    case 8: return " (-5)";//APP_FOLLOWER_DEC_MED
                    case 9: return " (-10)";//APP_FOLLOWER_DEC_HIGH
                    case 10: return " (-15)";//APP_FOLLOWER_DEC_VHIGH
                    case 11: return " (-20)";//APP_FOLLOWER_DEC_EXTREME
                    default: return "";
                }
            }

            return "";
        }

        private bool IsPlayer(DialogConnector line)
        {
            return line.LineNodes[0].Speaker == "PLAYER";
        }

        private bool IsBlank(DialogConnector line)
        {
            return line.LineNodes[0].text == "" ? true : false;
            //return pSource.tlkDao.ContainsKey(line.LineNode.StringID) ? false : true;
        }

        private bool IsGoodbyeEmpty(DialogConnector dc)
        {
            return (IsBlank(dc) && IsGoodbye(dc));
        }

        private bool IsGoodbye(DialogConnector dc)
        {
            return (dc.LineNodes[0].TransitionList.Count == 0) ? true : false;
        }

        private void HandleScript(string sInfoFormIDHex, DialogConnector line)
        {
            //TODO add Fragment_1 if Double action

            string sPathIn = @"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\Skyrim.esm\ScriptsSource\Template\";
            string sPathOutCompiled = @"d:\Games\HajdukTM\Data\Scripts\";
            string sPathOutSource = @"d:\Games\HajdukTM\Data\Source\Scripts\";
            string sTemplate = string.Empty;

            if (IsExternalAction(line))
            {
                if (HasObjective(line))
                    sTemplate = "E2";//TIF__01234567_E2 psc Text/pex Compiled //external w/ objective pQUST 0101
                else
                    sTemplate = "E1";//TIF__01234567_E1 psc Text/pex Compiled //external w/o objective QUST 0101
            }
            else
            {
                if (HasObjective(line))
                    sTemplate = "2";//TIF__01234567_2 psc Text/pex Compiled //local w/ objective
                else
                    sTemplate = "1";//TIF__01234567_1 psc Text/pex Compiled //local w/o objective
            }

            //the rest
            //compiled
            byte[] fileBytesIn = null;
            byte[] fileBytesOut = null;
            using (FileStream fs = new FileStream(sPathIn + "TIF__01234567_" + sTemplate + ".pex", FileMode.Open, FileAccess.Read))
            {
                fileBytesIn = new byte[fs.Length];
                fileBytesOut = new byte[fs.Length];
                fs.Read(fileBytesIn, 0, (int)fs.Length);
            }

            //replace text first
            byte[] arrSearch = Encoding.ASCII.GetBytes("01234567");
            byte[] arrReplace = Encoding.ASCII.GetBytes(sInfoFormIDHex);

            //ugly
            fileBytesOut = ReplaceBytes(fileBytesIn, arrSearch, arrReplace);
            fileBytesOut = ReplaceBytes(fileBytesOut, arrSearch, arrReplace);

            //replace flag
            arrSearch = new byte[] { 0x01, 0x23, 0x45, 0x67 };
            arrReplace = BitConverter.GetBytes(line.LineNodes[GetNodeActionOwner(line)].ActionPlotFlag * 10);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(arrReplace);

            //replace once for No Objective
            fileBytesOut = ReplaceBytes(fileBytesOut, arrSearch, arrReplace);

            using (FileStream writeFileStream = new FileStream(sPathOutCompiled + "TIF__" + sInfoFormIDHex + ".pex", FileMode.Create))
            {
                writeFileStream.Write(fileBytesOut, 0, fileBytesOut.Length);
            }

            //plain text
            List<string> lines = new List<string>();
            using (var _fileReader = File.OpenText(sPathIn + "TIF__01234567_" + sTemplate + ".psc"))
            {
                string _line;
                while ((_line = _fileReader.ReadLine()) != null)
                {
                    if (_line.Contains("01234567"))
                        _line = _line.Replace("01234567", sInfoFormIDHex);

                    if (_line.Contains("(0101)"))
                        _line = _line.Replace("(0101)", '(' + (line.LineNodes[GetNodeActionOwner(line)].ActionPlotFlag * 10).ToString() + ')');

                    lines.Add(_line);
                }

                _fileReader.Close();
            }

            string[] nLines = lines.ToArray();
            File.WriteAllLines(sPathOutSource + "TIF__" + sInfoFormIDHex + ".psc", nLines);
        }

        private List<SubRecord> GetSpecialConditions(DialogConnector line)
        {
            List<SubRecord> specials = new List<SubRecord>();

            if (line.LineNodes[0].ConditionPlotURI != 24//gen00pt_backgrounds
                    && line.LineNodes[0].ConditionPlotURI != 26//gen00pt_class_race_gend
                        && line.LineNodes[0].ConditionPlotURI != 28//gen00pt_combat
                            && line.LineNodes[0].ConditionPlotURI != 791)//gen00pt_generic_actions
                return specials;//TODO more specials

            var xd = XDocument.Load(ArrDictQuestDAO[line.LineNodes[0].ConditionPlotURI]);

            IEnumerable<XElement> blobs = from blob in xd.Root.Descendants("StatusList").Descendants("Agent")
                                          where !blob.Parent.Name.ToString().Contains("PlotAssistInfoList") &&
                                                Convert.ToInt32(blob.Descendants("Flag").ToList()[0].Value) == line.LineNodes[0].ConditionPlotFlag
                                          select blob;

            if (blobs.ToList().Count != 1)
                Console.WriteLine();

            var b = blobs.ToList()[0];

            return ArrDictSubRecordsSpecial[b.Descendants("name").ToList()[0].Value];
        }

        private Dictionary<string, SubRecord> GetSubrecords()
        {
            var dsr = new Dictionary<string, SubRecord>();

            //for BNAM in DLVW/DLBR
            Record viewTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogView"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in viewTemplate.SubRecords)
            {
                if (sr.Name == "BNAM")//
                {
                    dsr.Add("BNAM", sr.Clone() as SubRecord);
                }
            }

            //for INFO
            Record infoTemplate = null;

            //DialogInfoNPCBothGoodbye
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCBothGoodbye"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "VMAD")//
                {
                    var srVMAD = sr.Clone() as SubRecord;

                    byte[] arrSearch = Encoding.ASCII.GetBytes(0x000D39AE.ToString("X8"));
                    byte[] arrReplace = Encoding.ASCII.GetBytes(0x01234567.ToString("X8"));

                    //ugly, do it 3 time manually - TODO automatically
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));

                    dsr.Add("VMAD_Local_Begin", srVMAD);//000D39AE
                }
                if (sr.Name == "ENAM")//
                {
                    dsr.Add("ENAM_Goodbye", sr.Clone() as SubRecord);
                }
                if (sr.Name == "CNAM")//
                {
                    dsr.Add("CNAM_None", sr.Clone() as SubRecord);
                }
                if (sr.Name == "TRDT")//
                {
                    dsr.Add("TRDT", sr.Clone() as SubRecord);
                }
                if (sr.Name == "NAM1")//
                {
                    dsr.Add("NAM1_w_text", sr.Clone() as SubRecord);
                }
                if (sr.Name == "NAM2")//
                {
                    dsr.Add("NAM2_wo_text", sr.Clone() as SubRecord);
                }
                if (sr.Name == "NAM3")//
                {
                    dsr.Add("NAM3_w_text", sr.Clone() as SubRecord);//useless?
                }
                if (sr.Name == "CTDA")//
                {
                    var srCTDA = sr.Clone() as SubRecord;

                    //replace ID FormID
                    byte[] arrSearchExt = new byte[] { 0xB8, 0x3B, 0x01, 0x00 };
                    byte[] arrReplaceExt = new byte[] { 0x67, 0x45, 0x23, 0x01 };

                    srCTDA.SetData(ReplaceBytes(srCTDA.GetData(), arrSearchExt, arrReplaceExt));

                    dsr.Add("Condition_GetIsID_And", srCTDA);//00013BB8

                    //do OR here too
                    var srOr = srCTDA.Clone() as SubRecord;
                    var data = srOr.GetData();
                    data[0] = 0x01;//Equal Or
                    srOr.SetData(data);
                    dsr.Add("Condition_GetIsID_Or", srOr.Clone() as SubRecord);//00013BB8
                }
            }

            //DialogInfoNPCStartSplit
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCStartSharedInfo"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "ENAM")//
                {
                    dsr.Add("ENAM_Simple", sr.Clone() as SubRecord);
                }
                if (sr.Name == "DNAM")//
                {
                    dsr.Add("DNAM", sr.Clone() as SubRecord);//0001362A
                }
            }

            //DialogInfoNPCBothTemplate
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCBothTemplate"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "VMAD")//
                {
                    var srVMAD = sr.Clone() as SubRecord;

                    byte[] arrSearch = Encoding.ASCII.GetBytes(0x000DF1DA.ToString("X8"));
                    byte[] arrReplace = Encoding.ASCII.GetBytes(0x01234567.ToString("X8"));

                    //ugly, do it 3 time manually - TODO automatically
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));

                    dsr.Add("VMAD_Local_End", srVMAD);//000DF1DA
                }
                if (sr.Name == "NAM3")//
                {
                    dsr.Add("NAM3_wo_text", sr.Clone() as SubRecord);//useless?
                }
            }

            //DialogInfoNPCStartLinkTo
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCStartLinkTo"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "TCLT")//
                {
                    if (!dsr.ContainsKey("TCLT"))
                        dsr.Add("TCLT", sr.Clone() as SubRecord);//00098D3D
                }
                if (sr.Name == "NAM2")//
                {
                    dsr.Add("NAM2_w_text", sr.Clone() as SubRecord);
                }
            }

            //DialogInfoNPCBlankLine
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                ArrDictNamesSource["DialogInfoNPCBlankLine"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "TRDT")//
                {
                    dsr.Add("TRDT_Empty", sr.Clone() as SubRecord);
                }
                if (sr.Name == "NAM1")//
                {
                    dsr.Add("NAM1_wo_text", sr.Clone() as SubRecord);
                }
            }

            //DialogInfoNPCActionExternal
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                ArrDictNamesSource["DialogInfoNPCActionExternal"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "VMAD")//
                {
                    var srVMAD = sr.Clone() as SubRecord;

                    //replace Quest FormID
                    byte[] arrSearchExt = new byte[] { 0x52, 0xEA, 0x01, 0x00 };
                    byte[] arrReplaceExt = new byte[] { 0x67, 0x45, 0x23, 0x01 };

                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearchExt, arrReplaceExt));

                    //replace DB03 with QUST
                    arrSearchExt = Encoding.ASCII.GetBytes("DB03");
                    arrReplaceExt = Encoding.ASCII.GetBytes("QUST");

                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearchExt, arrReplaceExt));

                    byte[] arrSearch = Encoding.ASCII.GetBytes(0x000AAAC9.ToString("X8"));
                    byte[] arrReplace = Encoding.ASCII.GetBytes(0x01234567.ToString("X8"));

                    //ugly, do it 3 time manually - TODO automatically
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));

                    dsr.Add("VMAD_External_End", srVMAD);//000AAAC9 - 0001EA52
                }
            }

            //DialogInfoNPCActionExternalObjective
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                ArrDictNamesSource["DialogInfoNPCActionExternalObjective"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "VMAD")//
                {
                    var srVMAD = sr.Clone() as SubRecord;

                    //replace Quest FormID
                    byte[] arrSearchExt = new byte[] { 0x52, 0xEA, 0x01, 0x00 };
                    byte[] arrReplaceExt = new byte[] { 0x67, 0x45, 0x23, 0x01 };

                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearchExt, arrReplaceExt));

                    //replace DB03 with QUST
                    arrSearchExt = Encoding.ASCII.GetBytes("DB03");
                    arrReplaceExt = Encoding.ASCII.GetBytes("QUST");

                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearchExt, arrReplaceExt));

                    byte[] arrSearch = Encoding.ASCII.GetBytes(0x000D2AE6.ToString("X8"));
                    byte[] arrReplace = Encoding.ASCII.GetBytes(0x01234567.ToString("X8"));

                    //ugly, do it 3 time manually - TODO automatically
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));
                    srVMAD.SetData(ReplaceBytes(srVMAD.GetData(), arrSearch, arrReplace));

                    dsr.Add("VMAD_External_Objective_End", srVMAD);//000D2AE6 - 0001EA52
                }
            }

            //DialogInfoNPCSimpleGetStageDone
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                ArrDictNamesSource["DialogInfoNPCSimpleGetStageDone"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")//
                {
                    var srCTDA = sr.Clone() as SubRecord;
                    byte[] arrSearch = new byte[] { 0x0A, 0x61, 0x02, 0x00, //Quest FormID
                        0x32, 0x00, 0x00, 0x00 }; //Quest Stage 50
                    byte[] arrReplace = new byte[] { 0x67, 0x45, 0x23, 0x01,
                        0x65, 0x00, 0x00, 0x00};//0101

                    srCTDA.SetData(ReplaceBytes(srCTDA.GetData(), arrSearch, arrReplace));

                    dsr.Add("Condition_GetStageDone", srCTDA);//0002610A
                }
            }

            //TODO NOT Equal or, maybe for defined flags?!?
            //DialogInfoNPCSimpleGetStageDoneNot
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                ArrDictNamesSource["DialogInfoNPCSimpleGetStageDoneNotAnd"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")//
                {
                    if (!dsr.ContainsKey("Condition_GetStageDone_NotAnd"))
                    {
                        var srCTDA = sr.Clone() as SubRecord;
                        byte[] arrSearch = new byte[] { 0xFB, 0xC3, 0x01, 0x00, //Quest FormID
                        0x64, 0x00, 0x00, 0x00 }; //Quest Stage 100
                        byte[] arrReplace = new byte[] { 0x67, 0x45, 0x23, 0x01,
                        0x65, 0x00, 0x00, 0x00};//0101

                        srCTDA.SetData(ReplaceBytes(srCTDA.GetData(), arrSearch, arrReplace));

                        dsr.Add("Condition_GetStageDone_NotAnd", srCTDA);//0001C3FB - 000DD758
                    }
                }
            }

            //DialogInfoNPCSimpleInvisibleContinue
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                ArrDictNamesSource["DialogInfoNPCSimpleInvisibleContinue"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "ENAM")//
                {
                    dsr.Add("ENAM_InvisibleContinue", sr.Clone() as SubRecord);
                }
            }

            return dsr;
        }

        private Dictionary<string, List<SubRecord>> GetSubrecordsSpecial()
        {
            var dsrs = new Dictionary<string, List<SubRecord>>();
            var srList = new List<SubRecord>();
            Record infoTemplate = null;

            //Backgrounds TODO better
            //elf dalish = highelf+bow
            //elf city = ?
            //dwarf commoner = woodelf+bow
            //dwarf noble = ?
            //human noble = human race

            //DialogInfoNPCSimpleGetPCIsClassArcher
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCIsClassArcher"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            //local
            var lArcher = new List<SubRecord>();
            int n = 0;
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    if (n > 2)
                    {
                        lArcher.Add(sr.Clone() as SubRecord);
                    }
                    n++;
                }
            }

            //DialogInfoNPCSimpleGetPCIsSexFemale
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCIsSexFemale"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            srList = new List<SubRecord>();
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    srList.Add(sr.Clone() as SubRecord);
                }
            }
            dsrs.Add("GEN_GENDER_FEMALE", srList);

            //DialogInfoNPCSimpleGetPCIsSexMale
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCIsSexMale"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            srList = new List<SubRecord>();
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    srList.Add(sr.Clone() as SubRecord);
                }
            }
            dsrs.Add("GEN_GENDER_MALE", srList);

            //DialogInfoNPCSimpleGetPCIsRaceWoodElf - Dwarf replacement
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCIsRaceWoodElf"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            srList = new List<SubRecord>();
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    srList.Add(sr.Clone() as SubRecord);
                }
            }
            dsrs.Add("GEN_RACE_DWARF", srList);
            dsrs.Add("GEN_BACK_DWARF_NOBLE", srList);
            dsrs.Add("GEN_BACK_DWARF_COMMONER", srList.Concat(lArcher).ToList());

            //DialogInfoNPCSimpleGetPCIsRaceHighElf - Elf replacement TODO better?more elves?
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCIsRaceHighElf"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            srList = new List<SubRecord>();
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    srList.Add(sr.Clone() as SubRecord);
                }
            }
            dsrs.Add("GEN_RACE_ELF", srList);
            dsrs.Add("GEN_BACK_ELF_CITY", srList);
            dsrs.Add("GEN_BACK_ELF_DALISH", srList.Concat(lArcher).ToList());

            //DialogInfoNPCSimpleGetPCIsRaceHuman - Human replacement
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCIsRaceHuman"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            srList = new List<SubRecord>();
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    srList.Add(sr.Clone() as SubRecord);
                }
            }
            dsrs.Add("GEN_RACE_HUMAN", srList);
            dsrs.Add("GEN_BACK_HUMAN_NOBLE", srList);

            //DialogInfoNPCSimpleGetPCIsClassMage
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCIsClassMage"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            srList = new List<SubRecord>();
            n = 0;
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    if (n > 0 && n < 6) //1-5, skip 0,6
                    {
                        srList.Add(sr.Clone() as SubRecord);
                    }
                    n++;
                }
            }
            dsrs.Add("GEN_CLASS_MAGE", srList);

            //DialogInfoNPCSimpleGetPCInFaction for Bloodmage-Reaver-Assassin TODO better
            infoTemplate = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                           (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                           ArrDictNamesSource["DialogInfoNPCSimpleGetPCInFaction"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            srList = new List<SubRecord>();
            n = 0;
            foreach (var sr in infoTemplate.SubRecords)
            {
                if (sr.Name == "CTDA")
                {
                    if (n == 2)
                    {
                        srList.Add(sr.Clone() as SubRecord);
                    }
                    n++;
                }
            }
            dsrs.Add("GEN_CLASS_DEF_BLOODMAGE_OR_REAVER_OR_ASSASSIN", srList);

            return dsrs;
        }

        private Record GetEmptyInfo(bool bNewFormID)
        {
            Record info = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault
                            (x => x.FormID == uint.Parse((TopLevelControl as MainView).
                                ArrDictNamesSource["DialogInfoNPCBothTemplate"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            while (info.SubRecords.Count > 0)
            {
                info.SubRecords.RemoveAt(info.SubRecords.Count - 1);
            }

            if (bNewFormID)
            {
                info.FormID = (TopLevelControl as MainView).CounterFormID;
                (TopLevelControl as MainView).CounterFormID++;
            }

            return info;
        }

        private GroupRecord GetGroupFromLine(DialogConnector line)
        {
            //make a new GRUP
            GroupRecord group = (TopLevelControl as MainView).GroupInfo.Clone() as GroupRecord;
            group.Records.RemoveAt(group.Records.Count - 1);//empty it

            //update Group data to match DIAL FormID
            string hexDialToInfoName = line.LineDialog.Dial.FormID.ToString("X8");

            byte[] arrDialToInfoNameFull = Enumerable.Range(0, hexDialToInfoName.Length / 2).Select(x => Convert.ToByte(hexDialToInfoName.Substring(x * 2, 2), 16)).ToArray();
            arrDialToInfoNameFull = arrDialToInfoNameFull.Reverse().ToArray();

            group.SetData(arrDialToInfoNameFull);

            return group;
        }

        private void SetRecordEdid(ref Record topic, string sEdid, DialogConnector line)
        {
            foreach (var sr in (topic as Record).SubRecords)
            {
                if (sr.Name == "EDID")
                {
                    byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sEdid).Length + 1];
                    Array.Copy(Encoding.ASCII.GetBytes(sEdid), plusNull, Encoding.ASCII.GetBytes(sEdid).Length);

                    sr.SetData(plusNull);

                    topic.SetDescription(" (" + sEdid + ")");
                    break;
                }
            }
        }

        private void SetRecordFull(ref Record record, string sFull, int nTalkId = -1)
        {
            int nTalk = -1;
            if (nTalkId == -1)
            {
                nTalk = (TopLevelControl as MainView).CounterTalk;
                (TopLevelControl as MainView).CounterTalk++;
            }
            else
                nTalk = nTalkId;

            string sEdid = "";
            foreach (var sr in (record as Record).SubRecords)
            {
                if (sr.Name == "EDID")
                {
                    sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                    sEdid = sEdid.TrimEnd('\0');
                    break;
                }
            }

            if (sEdid == "")
            {
                if (record.Name != "INFO")
                    Console.WriteLine();

                sEdid = '[' + record.FormID.ToString("X8") + ']';
            }

            string hexName = nTalk.ToString("X8");
            string hexName6 = nTalk.ToString("X6");

            byte[] arrNameFull = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
            arrNameFull = arrNameFull.Reverse().ToArray();

            foreach (var sr in (record as Record).SubRecords)
            {
                if (sr.Name == "FULL")
                {
                    sr.SetData(arrNameFull);

                    XElement xString = new XElement("String");
                    xString.Add(new XAttribute("List", 0));
                    xString.Add(new XAttribute("sID", hexName6));

                    XElement xEdid = new XElement("EDID", sEdid);
                    xString.Add(xEdid);

                    XElement xRec = new XElement("REC", record.Name + ':' + sr.Name);
                    xString.Add(xRec);

                    XElement xSource = new XElement("Source", sFull);
                    xString.Add(xSource);

                    XElement xDest = new XElement("Dest", sFull);
                    xString.Add(xDest);

                    xContent.Add(xString);
                }
            }
        }

        private XDocument GetCurrentQuest(string sCurrent)
        {
            XDocument xdCurrent = XDocument.Load(sCurrent);

            if (xdCurrent.Root.Name == "Resource")//it's a DAO quest, need to convert to Skyrim
            {
                return GetQuestSkyFromDao(xdCurrent.Root.Descendants("ResRefName").ToList()[0].Value);
            }
            else
                return xdCurrent;
        }

        private bool HasComment(DialogConnector line)
        {
            return (line.LineNodes[0].VoiceOverComment != "" || line.LineNodes[0].Comment != "");
        }

        private bool HasObjective(DialogConnector line)
        {
            if (HasAction(line))
            {
                XDocument xdCurrentQuest = GetCurrentQuest(ArrDictQuestSkyrim[line.LineNodes[GetNodeActionOwner(line)].ActionPlotURI]);

                foreach (var q in xdCurrentQuest.Root.Descendants("Quest_Objective_Index"))
                {
                    if (q.Value == (line.LineNodes[GetNodeActionOwner(line)].ActionPlotFlag * 10).ToString())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsExternalAction(DialogConnector line)
        {
            //say my action is Quest25 and branch condition is Quest35, then it's external
            return (HasAction(line) && line.LineNodes[GetNodeActionOwner(line)].ActionPlotURI != GetBranchOwnerNode(line).ConditionPlotURI);
        }

        private bool HasCondition(DialogConnector line)
        {
            return line.LineNodes[0].ConditionPlotURI > 0;
        }

        private bool HasAction(DialogConnector line)
        {
            //TODO reminder: it skips cod_* quests
            if (IsCodex(line.LineNodes[0].ActionPlotURI))
                return false;

            //return line.LineNodes[0].ActionPlotURI > 0;
            return GetNodeActionOwner(line) != -1;
        }

        private bool IsCodex(int actionPlotURI)
        {
            var xd = XDocument.Load(ArrDictQuestSkyrim[actionPlotURI]);

            string s = xd.Root.Descendants("EDID").ToList()[0].Descendants("Name").ToList()[0].Value;

            if (s == "")
                Console.WriteLine();

            if (s.ToCharArray()[0] == 'c' &&
                s.ToCharArray()[1] == 'o' &&
                s.ToCharArray()[2] == 'd' &&
                s.ToCharArray()[3] == '_')
                return true;

            return false;
        }

        private void SetLocalQuest(FConversation currentConversation)
        {
            //check for local non-condition start line
            bool bFound = false;

            foreach (var n in CurrentConversation.StartList)
            {
                if (CurrentConversation.NPCLineList[n].ConditionPlotURI == 0)
                {
                    string[] filesEmpty = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                  "*Quest_" + CurrentConversation.ResRefName + ".xml",
                                  System.IO.SearchOption.TopDirectoryOnly);

                    if (filesEmpty.Length != 1)
                        Console.WriteLine();

                    ArrDictQuestSkyrim[0] = filesEmpty[0];//replace 0 with current Quest_*        

                    bFound = true;

                    break;
                }
            }

            if (!bFound)
                Console.WriteLine();
        }

        private string GetQuestName(FConvNode node)
        {
            string sQuestName = "";
            string _s = "";

            if (node.ConditionPlotURI != 0) //HasCondition
            {
                XDocument xdf = XDocument.Load(ArrDictQuestDAO[node.ConditionPlotURI]);

                _s = xdf.Root.Descendants("ResRefName").ToList()[0].Value;

                string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                          "*_" + _s + ".xml",
                          System.IO.SearchOption.TopDirectoryOnly);

                string sFileToLoad = string.Empty;

                if (filesDAO.Length != 1)
                {
                    sFileToLoad = GetQuestSkyXML(filesDAO, _s);
                }
                else
                {
                    sFileToLoad = filesDAO[0];
                }

                if (string.IsNullOrEmpty(sFileToLoad))
                    Console.WriteLine();

                sQuestName = _s;

                //TODO Attributes and Skill flags
                if (_s == "gen00pt_backgrounds" ||
                    _s == "gen00pt_class_race_gend" ||
                    _s == "gen00pt_combat" ||
                    _s == "gen00pt_generic_actions")
                {
                    XDocument xdDoc = XDocument.Load(ArrDictQuestSkyrim[0]);

                    _s = xdDoc.Root.Descendants("FULL").ToList()[0].
                        Descendants("mayLocalize_Name").ToList()[0].Attribute("name").Value;

                    sQuestName = _s;
                }
            }
            else //no condition, own quest owner
            {
                XDocument xdDoc = XDocument.Load(ArrDictQuestSkyrim[0]);

                _s = xdDoc.Root.Descendants("FULL").ToList()[0].
                    Descendants("mayLocalize_Name").ToList()[0].Attribute("name").Value;

                sQuestName = _s;
            }

            return sQuestName;
        }

        private XDocument GetQuestSkyFromDao(string sResRefName)
        {
            XDocument xdQuest = null;

            string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                "*_" + sResRefName + ".xml",
                System.IO.SearchOption.TopDirectoryOnly);

            string sFileToLoad = string.Empty;

            if (filesDAO.Length != 1)
            {
                sFileToLoad = GetQuestSkyXML(filesDAO, sResRefName);
            }
            else
            {
                sFileToLoad = filesDAO[0];
            }

            xdQuest = XDocument.Load(sFileToLoad);

            return xdQuest;
        }

        private string GetQuestSkyPathFromDao(string file)
        {
            XDocument xd = XDocument.Load(file);

            string sResRefName = xd.Root.Descendants("ResRefName").ToList()[0].Value;

            string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                "*_" + sResRefName + ".xml",
                System.IO.SearchOption.TopDirectoryOnly);

            string sFileToLoad = string.Empty;

            if (filesDAO.Length != 1)
            {
                Console.WriteLine();
            }
            else
            {
                sFileToLoad = filesDAO[0];
            }

            return sFileToLoad;
        }

        public int FindBytes(byte[] src, byte[] find)
        {
            int index = -1;
            int matchIndex = 0;
            // handle the complete source array
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == find[matchIndex])
                {
                    if (matchIndex == (find.Length - 1))
                    {
                        index = i - matchIndex;
                        break;
                    }
                    matchIndex++;
                }
                else if (src[i] == find[0])
                {
                    matchIndex = 1;
                }
                else
                {
                    matchIndex = 0;
                }

            }
            return index;
        }

        public byte[] ReplaceBytes(byte[] src, byte[] search, byte[] repl)
        {
            byte[] dst = null;
            int index = FindBytes(src, search);
            if (index >= 0)
            {
                dst = new byte[src.Length - search.Length + repl.Length];
                // before found array
                Buffer.BlockCopy(src, 0, dst, 0, index);
                // repl copy
                Buffer.BlockCopy(repl, 0, dst, index, repl.Length);
                // rest of src array
                Buffer.BlockCopy(
                    src,
                    index + search.Length,
                    dst,
                    index + repl.Length,
                    src.Length - (index + search.Length));
            }

            return dst;
        }

        private Record HasView(string sViewNameFNV64, bool bSkipActive = false)
        {
            if (!bSkipActive)
            {
                //first in active records
                Record view = GetRecordFromConversationRecords(sViewNameFNV64);

                if (view != null)
                    return view;
            }

            foreach (var g in pDest.Records)//check in HajdukAge.esp
            {
                if (g is GroupRecord && (g as GroupRecord).ContentsType == "DLVW")
                {
                    foreach (var r in (g as GroupRecord).Records)
                    {
                        if (r is GroupRecord)
                            Console.WriteLine();

                        foreach (var sr in (r as Record).SubRecords)
                        {
                            if (sr.Name == "EDID")
                            {
                                string sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                                sEdid = sEdid.TrimEnd('\0');

                                if (sEdid == sViewNameFNV64)
                                    return (r as Record);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private Record HasBranch(string sBranchNameFNV64, bool bSkipActive = false)
        {
            if (!bSkipActive)
            {
                //first in active records
                Record branch = GetRecordFromConversationRecords(sBranchNameFNV64);

                if (branch != null)
                    return branch;
            }

            foreach (var g in pDest.Records)//check in HajdukAge.esp
            {
                if (g is GroupRecord && (g as GroupRecord).ContentsType == "DLBR")
                {
                    foreach (var r in (g as GroupRecord).Records)
                    {
                        if (r is GroupRecord)
                            Console.WriteLine();

                        foreach (var sr in (r as Record).SubRecords)
                        {
                            if (sr.Name == "EDID")
                            {
                                string sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                                sEdid = sEdid.TrimEnd('\0');

                                if (sEdid == sBranchNameFNV64)
                                    return (r as Record);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private Record HasTopic(string sTopicNameFNV64, bool bSkipActive = false)
        {
            if (!bSkipActive)
            {
                //first in active records
                Record topic = GetRecordFromConversationRecords(sTopicNameFNV64);

                if (topic != null)
                    return topic;
            }

            foreach (var g in pDest.Records)//check in HajdukAge.esp
            {
                if (g is GroupRecord && (g as GroupRecord).ContentsType == "DIAL")
                {
                    foreach (var r in (g as GroupRecord).Records)
                    {
                        if (r is Record)
                        {
                            foreach (var sr in (r as Record).SubRecords)
                            {
                                if (sr.Name == "EDID")
                                {
                                    string sEdid = System.Text.Encoding.Default.GetString(sr.GetData());
                                    sEdid = sEdid.TrimEnd('\0');

                                    if (sEdid == sTopicNameFNV64)
                                        return (r as Record);
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private Record GetRecordFromConversationRecords(string sNameFNV64)
        {
            if (ArrDictConversationRecords.ContainsKey(sNameFNV64))
                return ArrDictConversationRecords[sNameFNV64];

            return null;
        }

        private Record GetDialFromLine(DialogConnector line)
        {
            if (line.ConversationOwner == "")//check for BNAM/QNAM later as needed
                Console.WriteLine();

            string sType = IsPlayer(line) ? "P" : "N";
            sType = IsStartBranch(line) ? "Start" : sType;

            string sTopicName = line.LineDialog.QNAMName + '_' + line.ConversationOwner + '_' + GetBranchOwnerNode(line).ConditionPlotURI + '_' + sType + '_' + "Topic" + line.LineIndex;
            string sTopicNameFNV64 = GetFNV64Hashed(sTopicName);

            Record dialogTopic = HasTopic(sTopicNameFNV64);

            if (dialogTopic != null)
                return dialogTopic;

            //add to dict if null
            ArrDictFNV64.Add(sTopicNameFNV64, sTopicName);

            //has FULL, BNAM, QUST SubRecord
            dialogTopic = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse((TopLevelControl as MainView).ArrDictNamesSource["DialogTopicPlayerReply"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

            SetRecordEdid(ref dialogTopic, sTopicNameFNV64, line);

            //set FULL, skip if
            if (!IsPlayer(line))
            {
                if (IsStartBranch(line))
                    SetRecordFull(ref dialogTopic, "Start Dialogue");
                else
                    Console.WriteLine();
            }
            else
            {
                if (line.LineNodes[0].text == "")
                    SetRecordFull(ref dialogTopic, "(Invisible continue)", line.LineNodes[0].StringID);
                else //add Approval values on the visual dialog wheel 
                    SetRecordFull(ref dialogTopic, line.LineNodes[0].text + IsApprovalAction(line), line.LineNodes[0].StringID);
            }

            dialogTopic.FormID = (TopLevelControl as MainView).CounterFormID;//new ID for NPC Split
            (TopLevelControl as MainView).CounterFormID++;

            //set BNAM and QNAM
            foreach (var sr in dialogTopic.SubRecords)
            {
                if (sr.Name == "EDID")
                {
                    byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sTopicNameFNV64).Length + 1];
                    Array.Copy(Encoding.ASCII.GetBytes(sTopicNameFNV64), plusNull, Encoding.ASCII.GetBytes(sTopicNameFNV64).Length);

                    sr.SetData(plusNull);
                }

                if (sr.Name == "BNAM")
                {
                    if (line.LineDialog.BNAMFormID == 0)
                        Console.WriteLine();

                    string hex = line.LineDialog.BNAMFormID.ToString("X8");

                    byte[] arr = Enumerable.Range(0, hex.Length / 2).Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16)).ToArray();
                    arr = arr.Reverse().ToArray();

                    sr.SetData(arr);
                }

                if (sr.Name == "QNAM")
                {
                    string hex = "";

                    if (line.LineDialog.QNAMFormID == 0)
                        Console.WriteLine();

                    hex = line.LineDialog.QNAMFormID.ToString("X8");

                    byte[] arr = Enumerable.Range(0, hex.Length / 2).Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16)).ToArray();
                    arr = arr.Reverse().ToArray();

                    sr.SetData(arr);
                }
            }

            ArrDictConversationRecords.Add(sTopicNameFNV64, dialogTopic);

            return dialogTopic;
        }

        private FConvNode GetBranchOwnerNode(DialogConnector line)
        {
            if (CurrentConversation.StartList.Contains(line.LineIndex))
                return CurrentConversation.NPCLineList[line.LineIndex];

            int branch = -1;

            foreach (var i in CurrentConversation.StartList)
            {
                if (i < line.LineIndex)
                    branch = i;
                else
                    break;
            }

            return CurrentConversation.NPCLineList[branch];
        }

        private Record CreateDialogViewDLVW(DialogConnector line)
        {
            if (line.ConversationOwner == "" ||
                line.LineDialog.QNAMFormID == 0 ||
                line.LineDialog.QNAMName == "")
                Console.WriteLine();

            string sViewName = line.LineDialog.QNAMName + '_' +
                line.ConversationOwner + '_' +
                GetBranchOwnerNode(line).ConditionPlotURI + '_' +
                GetBranchOwnerNode(line).ConditionPlotFlag + '_' +
                GetBranchOwnerNode(line).ConditionResult.ToString() + '_' +
                "View" + '_' +
                line.LineIndex;//needed for bugged convs with multiple lines w/o conditions

            string sViewNameFNV64 = GetFNV64Hashed(sViewName);

            Record view = HasView(sViewNameFNV64);

            if (view != null)
                return view;

            //add to dict if null
            ArrDictFNV64.Add(sViewNameFNV64, sViewName);

            //create View
            var dialogView = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse((TopLevelControl as MainView).ArrDictNamesSource["DialogView"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;
            dialogView.FormID = (TopLevelControl as MainView).CounterFormID;
            (TopLevelControl as MainView).CounterFormID++;

            string sQuestFormID = line.LineDialog.QNAMFormID.ToString("X8");
            byte[] arrNameQuestToView = Enumerable.Range(0, sQuestFormID.Length / 2).Select(x => Convert.ToByte(sQuestFormID.Substring(x * 2, 2), 16)).ToArray();
            arrNameQuestToView = arrNameQuestToView.Reverse().ToArray();

            dialogView.SetDescription(' ' + sViewNameFNV64);

            foreach (var sr in (dialogView as Record).SubRecords)
            {
                if (sr.Name == "EDID")
                {
                    byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sViewNameFNV64).Length + 1];
                    Array.Copy(Encoding.ASCII.GetBytes(sViewNameFNV64), plusNull, Encoding.ASCII.GetBytes(sViewNameFNV64).Length);

                    sr.SetData(plusNull);
                }

                if (sr.Name == "QNAM")
                {
                    sr.SetData(arrNameQuestToView);
                }
            }

            var bnam = (dialogView as Record).SubRecords.Where(x => x.Name == "BNAM");
            if (bnam.ToList().Count != 1)
                Console.WriteLine();

            int i = (dialogView as Record).SubRecords.IndexOf(bnam.ToList()[0]);

            if ((dialogView as Record).SubRecords[i].Name != "BNAM")
                Console.WriteLine();

            (dialogView as Record).SubRecords.RemoveAt(i);

            ArrDictConversationRecords.Add(sViewNameFNV64, dialogView);

            return dialogView;
        }

        private Record CreateDialogBranchDLBR(DialogConnector line, Record dialogView = null)
        {
            if (line.ConversationOwner == "" || line.LineDialog.QNAMName == "" || line.LineDialog.QNAMFormID == 0)
                Console.WriteLine();

            if (dialogView == null)
            {
                //look for view
                string sViewName = line.LineDialog.QNAMName + '_' +
                                    line.ConversationOwner + '_' +
                                    GetBranchOwnerNode(line).ConditionPlotURI + '_' +
                                    GetBranchOwnerNode(line).ConditionPlotFlag + '_' +
                                    GetBranchOwnerNode(line).ConditionResult.ToString() + '_' +
                                    "View" + '_' +
                                    line.LineIndex;//needed for bugged convs with multiple lines w/o conditions

                string sViewNameFNV64 = GetFNV64Hashed(sViewName);

                dialogView = HasView(sViewNameFNV64);
            }

            if (dialogView == null)
                Console.WriteLine();

            //look for branch
            string sBranchName = line.LineDialog.QNAMName + '_' +
                                    line.ConversationOwner + '_' +
                                    GetBranchOwnerNode(line).ConditionPlotURI + '_' +
                                    GetBranchOwnerNode(line).ConditionPlotFlag + '_' +
                                    GetBranchOwnerNode(line).ConditionResult.ToString() + '_' +
                                    "Branch" + '_' +
                                    line.LineIndex;

            string sBranchNameFNV64 = GetFNV64Hashed(sBranchName);

            Record branch = HasBranch(sBranchNameFNV64);

            if (branch != null)
                return branch;

            //add to dict if null
            ArrDictFNV64.Add(sBranchNameFNV64, sBranchName);

            //create Branch and attach to view
            var dialogBranch = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse((TopLevelControl as MainView).ArrDictNamesSource["DialogBranch"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;
            dialogBranch.FormID = (TopLevelControl as MainView).CounterFormID;
            (TopLevelControl as MainView).CounterFormID++;

            //update branch
            string sQuestFormID = line.LineDialog.QNAMFormID.ToString("X8");
            byte[] arrNameQuestToBranch = Enumerable.Range(0, sQuestFormID.Length / 2).Select(x => Convert.ToByte(sQuestFormID.Substring(x * 2, 2), 16)).ToArray();
            arrNameQuestToBranch = arrNameQuestToBranch.Reverse().ToArray();

            dialogBranch.SetDescription(' ' + sBranchNameFNV64);

            foreach (var sr in (dialogBranch as Record).SubRecords)
            {
                if (sr.Name == "EDID")
                {
                    byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sBranchNameFNV64).Length + 1];
                    Array.Copy(Encoding.ASCII.GetBytes(sBranchNameFNV64), plusNull, Encoding.ASCII.GetBytes(sBranchNameFNV64).Length);

                    sr.SetData(plusNull);
                }

                if (sr.Name == "QNAM")
                {
                    sr.SetData(arrNameQuestToBranch);
                }
            }

            //BNAM for DLVW
            SubRecord bnam = ArrDictSubRecords["BNAM"].Clone() as SubRecord;

            var enam = (dialogView as Record).SubRecords.Where(x => x.Name == "ENAM");
            if (enam.ToList().Count != 1)
                Console.WriteLine();

            int i = (dialogView as Record).SubRecords.IndexOf(enam.ToList()[0]);

            if ((dialogView as Record).SubRecords[i].Name != "ENAM")
                Console.WriteLine();

            string sBranchFormID = dialogBranch.FormID.ToString("X8");
            byte[] arrBranch = Enumerable.Range(0, sBranchFormID.Length / 2).Select(x => Convert.ToByte(sBranchFormID.Substring(x * 2, 2), 16)).ToArray();
            arrBranch = arrBranch.Reverse().ToArray();

            bnam.SetData(arrBranch);
            dialogView.SubRecords.Insert(i, bnam);

            ArrDictConversationRecords.Add(sBranchNameFNV64, dialogBranch);

            return dialogBranch;
        }

        private Record CreateDialogTopicDIALOld(uint nBranchFormID)
        {
            //create start DIAL
            var dialogTopic = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse((TopLevelControl as MainView).ArrDictNamesSource["DialogTopicStart"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;
            dialogTopic.FormID = (TopLevelControl as MainView).CounterFormID;
            (TopLevelControl as MainView).CounterFormID++;
            dialogTopic.SetDescription(' ' + CurrentQuestName + "TopicStart" + CurrentConditionFlag);

            foreach (var sr in (dialogTopic as Record).SubRecords)
            {
                if (sr.Name == "EDID")
                {
                    byte[] plusNull = new byte[Encoding.ASCII.GetBytes(CurrentQuestName + "TopicStart" + CurrentConditionFlag).Length + 1];
                    Array.Copy(Encoding.ASCII.GetBytes(CurrentQuestName + "TopicStart" + CurrentConditionFlag), plusNull, Encoding.ASCII.GetBytes(CurrentQuestName + "TopicStart" + CurrentConditionFlag).Length);

                    sr.SetData(plusNull);
                    break;
                }
            }

            string hexNameQuestToTopic = CurrentQuestFormID;

            byte[] arrNameQuestToTopic = Enumerable.Range(0, hexNameQuestToTopic.Length / 2).Select(x => Convert.ToByte(hexNameQuestToTopic.Substring(x * 2, 2), 16)).ToArray();
            arrNameQuestToTopic = arrNameQuestToTopic.Reverse().ToArray();

            foreach (var sr in (dialogTopic as Record).SubRecords)
            {
                if (sr.Name == "QNAM")
                {
                    sr.SetData(arrNameQuestToTopic);
                    break;
                }
            }

            //update DIAL to belong to DLBR
            string hexNameBranchToTopic = nBranchFormID.ToString("X8");

            byte[] arrNameBranchToTopic = Enumerable.Range(0, hexNameBranchToTopic.Length / 2).Select(x => Convert.ToByte(hexNameBranchToTopic.Substring(x * 2, 2), 16)).ToArray();
            arrNameBranchToTopic = arrNameBranchToTopic.Reverse().ToArray();

            foreach (var sr in (dialogTopic as Record).SubRecords)
            {
                if (sr.Name == "BNAM")
                {
                    sr.SetData(arrNameBranchToTopic);
                    break;
                }
            }

            return dialogTopic;
        }

        private Record UpdateBranchStartingTopic(Record branch, uint nTopicFormID)
        {
            string hexNameTopicToBranch = nTopicFormID.ToString("X8");

            byte[] arrNameTopicToBranch = Enumerable.Range(0, hexNameTopicToBranch.Length / 2).Select(x => Convert.ToByte(hexNameTopicToBranch.Substring(x * 2, 2), 16)).ToArray();
            arrNameTopicToBranch = arrNameTopicToBranch.Reverse().ToArray();

            foreach (var sr in (branch as Record).SubRecords)
            {
                if (sr.Name == "SNAM")
                {
                    sr.SetData(arrNameTopicToBranch);
                    break;
                }
            }

            return branch;
        }

        private GroupRecord CreateDialogGroupINFO(uint nTopicFormID)
        {
            //add GRUP INFO
            var dialogGroupInfo = (TopLevelControl as MainView).GroupInfo.Clone() as GroupRecord;
            dialogGroupInfo.Parent = (TopLevelControl as MainView).GroupInfo.Parent;

            string hexDialToInfoName = nTopicFormID.ToString("X8");

            byte[] arrDialToInfoNameFull = Enumerable.Range(0, hexDialToInfoName.Length / 2).Select(x => Convert.ToByte(hexDialToInfoName.Substring(x * 2, 2), 16)).ToArray();
            arrDialToInfoNameFull = arrDialToInfoNameFull.Reverse().ToArray();

            (dialogGroupInfo as GroupRecord).SetData(arrDialToInfoNameFull);

            foreach (var r in dialogGroupInfo.Records)
            {
                (r as Record).FormID = (TopLevelControl as MainView).CounterFormID;
                (TopLevelControl as MainView).CounterFormID++;
            }

            return dialogGroupInfo;
        }

        private void CreateDialogStart()
        {
            int nConvCounter = 0;
            int nMaxConv = ArrConversations.Count;

            foreach (var c in ArrConversations)
            {
                nConvCounter++;

                ArrViewsForQuests = new List<BaseRecord>();
                ArrBranchesForView = new List<Record>();
                ArrDialAndInfo = new List<BaseRecord>();

                foreach (var sb in c.Value.StartList)
                {
                    //if there are more than one starting dialog line w/o conditions is a bug
                    //bool BoolBug = false;
                    //if certain condition will replace with Script and move to owner w/ no cond branch
                    bool bSpecialStart = false;

                    string _s = "";
                    XDocument xdDoc = null;

                    Record dialogView = null;

                    if (c.Value.NPCLineList[sb].ConditionPlotURI != 0) //HasCondition
                    {
                        XDocument xdf = XDocument.Load(ArrDictQuestDAO[c.Value.NPCLineList[sb].ConditionPlotURI]);

                        _s = xdf.Root.Descendants("ResRefName").ToList()[0].Value;

                        string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                  "*_" + _s + ".xml",
                                  System.IO.SearchOption.TopDirectoryOnly);

                        string sFileToLoad = string.Empty;

                        if (filesDAO.Length != 1)
                        {
                            sFileToLoad = GetQuestSkyXML(filesDAO, _s);
                        }
                        else
                        {
                            sFileToLoad = filesDAO[0];
                        }

                        if (string.IsNullOrEmpty(sFileToLoad))
                            Console.WriteLine();

                        xdDoc = XDocument.Load(sFileToLoad);
                        CurrentQuestFormID = xdDoc.Root.Attribute("formID").Value;
                        CurrentQuestName = _s;
                        CurrentConditionFlag = c.Value.NPCLineList[sb].ConditionPlotFlag.ToString();

                        if (_s == "gen00pt_backgrounds" ||
                            _s == "gen00pt_class_race_gend" ||
                            _s == "gen00pt_combat" ||
                            _s == "gen00pt_generic_actions")
                        {
                            bSpecialStart = true;
                            Console.WriteLine();
                        }

                        _s = _s + '|' + CurrentConditionFlag;

                        if (!bSpecialStart)
                        {
                            if (!ArrDictQuestCounter.ContainsKey(_s))
                            {
                                ArrDictQuestCounter.Add(_s, 1);

                                uint nQuestFormID = uint.Parse(xdDoc.Root.Attribute("formID").Value, System.Globalization.NumberStyles.HexNumber);
                                if (!ArrDictQuestAndViewDone.ContainsKey(_s.Split('|')[0]))
                                {
                                    ArrDictQuestAndViewDone.Add(_s.Split('|')[0], nQuestFormID);

                                    //dialogView = CreateDialogViewDLVW() as Record;//disabled, doublecheck
                                    ArrViewsForQuests.Add(dialogView);
                                }
                            }
                            else
                            {
                                int n = ArrDictQuestCounter[_s];
                                ArrDictQuestCounter[_s] = n + 1;
                            }
                        }
                    }
                    else //no condition, own quest owner
                    {
                        string[] filesDAO = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                  "*_Quest_" + c.Value.ResRefName + ".xml",
                                  System.IO.SearchOption.TopDirectoryOnly);

                        string sFileToLoad = string.Empty;

                        if (filesDAO.Length != 1)
                        {
                            sFileToLoad = GetQuestSkyXML(filesDAO, c.Value.ResRefName);
                        }
                        else
                        {
                            sFileToLoad = filesDAO[0];
                        }

                        xdDoc = XDocument.Load(sFileToLoad);

                        _s = xdDoc.Root.Descendants("FULL").ToList()[0].
                            Descendants("mayLocalize_Name").ToList()[0].Attribute("name").Value;

                        CurrentQuestFormID = xdDoc.Root.Attribute("formID").Value;
                        CurrentQuestName = _s;
                        CurrentConditionFlag = "0";//No Condition

                        if (_s == "gen00pt_backgrounds" ||
                            _s == "gen00pt_class_race_gend" ||
                            _s == "gen00pt_combat" ||
                            _s == "gen00pt_generic_actions")
                            Console.WriteLine();//this should never happen, here for useless reasons :D

                        _s = _s + '|' + CurrentConditionFlag;

                        if (!ArrDictQuestCounter.ContainsKey(_s))
                        {
                            ArrDictQuestCounter.Add(_s, 1);

                            uint nQuestFormID = uint.Parse(xdDoc.Root.Attribute("formID").Value, System.Globalization.NumberStyles.HexNumber);
                            if (!ArrDictQuestAndViewDone.ContainsKey(_s.Split('|')[0]))
                            {
                                ArrDictQuestAndViewDone.Add(_s.Split('|')[0], nQuestFormID);

                                //dialogView = CreateDialogViewDLVW() as Record;//disabled, doublecheck
                                ArrViewsForQuests.Add(dialogView);
                            }
                        }
                        else
                        {
                            int n = ArrDictQuestCounter[_s];
                            ArrDictQuestCounter[_s] = n + 1;

                            BoolBug = true;
                            if (!ArrDialogsWithDefaults.Contains(_s.Split('|')[0]))
                                ArrDialogsWithDefaults.Add(_s.Split('|')[0]);
                        }
                    }

                    if (_s == "")
                        Console.WriteLine();

                    if (!BoolBug /*&& !bSpecialStart*/)
                        AddDialogRecords(dialogView);
                }

                //update Branches in View
                foreach (var v in ArrViewsForQuests)
                {
                    //update records
                    //add BranchesForView records to View
                    SubRecord bnam = null;
                    List<SubRecord> BeforeBNAM = new List<SubRecord>();
                    List<SubRecord> AfterBNAM = new List<SubRecord>();

                    foreach (var sr in (v as Record).SubRecords)
                    {
                        if (sr.Name == "EDID")
                        {
                            BeforeBNAM.Add(sr.Clone() as SubRecord);
                        }
                        else if (sr.Name == "QNAM")
                        {
                            BeforeBNAM.Add(sr.Clone() as SubRecord);
                        }
                        else if (sr.Name == "BNAM")
                        {
                            bnam = sr.Clone() as SubRecord;
                        }
                        else if (sr.Name == "ENAM")
                        {
                            AfterBNAM.Add(sr.Clone() as SubRecord);
                        }
                        else if (sr.Name == "DNAM")
                        {
                            AfterBNAM.Add(sr.Clone() as SubRecord);
                        }
                    }

                    if (bnam == null)
                        Console.WriteLine();

                    while ((v as Record).SubRecords.Count > 0)
                    {
                        (v as Record).SubRecords.RemoveAt((v as Record).SubRecords.Count - 1);
                    }

                    foreach (var b in BeforeBNAM)
                    {
                        (v as Record).SubRecords.Add(b.Clone() as SubRecord);
                    }

                    string sViewQuest = string.Empty;

                    foreach (var sr in (v as Record).SubRecords)
                    {
                        if (sr.Name == "QNAM")
                        {
                            sViewQuest = BitConverter.ToString(sr.GetData()).Replace("-", "");
                            break;
                        }
                    }

                    foreach (var bv in ArrBranchesForView)
                    {
                        string sBranchQuest = string.Empty;

                        foreach (var sr in (bv as Record).SubRecords)
                        {
                            if (sr.Name == "QNAM")
                            {
                                sBranchQuest = BitConverter.ToString(sr.GetData()).Replace("-", "");
                                break;
                            }
                        }

                        if (sViewQuest == sBranchQuest)
                        {
                            string hexNameBranch = bv.FormID.ToString("X8");

                            byte[] arrNameBranch = Enumerable.Range(0, hexNameBranch.Length / 2).Select(x => Convert.ToByte(hexNameBranch.Substring(x * 2, 2), 16)).ToArray();
                            arrNameBranch = arrNameBranch.Reverse().ToArray();

                            var bnamTemp = bnam.Clone() as SubRecord;
                            bnamTemp.SetData(arrNameBranch);

                            (v as Record).SubRecords.Add(bnamTemp);
                        }
                    }

                    foreach (var a in AfterBNAM)
                    {
                        (v as Record).SubRecords.Add(a.Clone() as SubRecord);
                    }
                }

                //copy x Views
                foreach (var v in ArrViewsForQuests)
                {
                    BaseRecord[] recordDLVW = new BaseRecord[1];
                    recordDLVW[0] = v;/*CurrentDialogViewDLVW*/
                    object[] nodesDLVW = new object[2];
                    nodesDLVW[0] = recordDLVW;
                    nodesDLVW[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                    int resDLVW = (TopLevelControl as MainView).CopyRecordsTo(nodesDLVW);
                }

                //copy x Branches
                foreach (var br in ArrBranchesForView)
                {
                    BaseRecord[] record = new BaseRecord[1];
                    record[0] = br;
                    object[] nodes = new object[2];
                    nodes[0] = record;
                    nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                    int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
                }

                if (ArrDialAndInfo.Count / 2 != ArrBranchesForView.Count)
                    Console.WriteLine();

                //copy x Start Topics and Info
                foreach (var d in ArrDialAndInfo)
                {
                    BaseRecord[] record = new BaseRecord[1];
                    record[0] = d;
                    object[] nodes = new object[2];
                    nodes[0] = record;
                    nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                    int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
                }
            }

            /*foreach(var q in QuestCounter)
            {
                if (q.Key!= "gen00pt_backgrounds" &&
                    q.Key!= "gen00pt_class_race_gend"&&
                    q.Key!= "gen00pt_combat"&&
                    q.Key!= "gen00pt_generic_actions")
                {
                    //create DialogView
                    CurrentDialogViewDLVW = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse((TopLevelControl as MainView).ArrDictNamesSource["DialogView"], System.Globalization.NumberStyles.HexNumber)).Clone() as Record;

                    CurrentDialogViewDLVW.FormID = (TopLevelControl as MainView).CounterFormID;

                    (TopLevelControl as MainView).CounterFormID++;
                }
            }*/

            bool bSave = false;
            if (bSave)
            {
                var csv = new StringBuilder();

                foreach (var d in ArrDictQuestCounter)
                {
                    var newLine = $"{d.Key},{d.Value}";
                    csv.AppendLine(newLine);
                }

                File.WriteAllText("QuestCounter.csv", csv.ToString());
            }
        }

        private string GetQuestSkyXML(string[] filesDAO, string sResRefName)
        {
            foreach (var f in filesDAO)
            {
                string[] fp = f.Split('\\');
                string[] fu = fp[fp.Length - 1].Split('.')[0].Split('_');

                string s = string.Empty;
                for (int i = 2; i < fu.Length; i++)
                {
                    s += (fu[i] + '_');
                }

                s = s.TrimEnd('_');

                if (s == sResRefName)
                {
                    return f;
                }
            }

            return "";
        }

        private void AddDialogRecords(Record dialogView)
        {
            Record dialogBranch = null; //CreateDialogBranchDLBR();

            //add DLBR
            if (!BoolBranchAlready)
            {
                Record dialogTopic = CreateDialogTopicDIALOld(dialogBranch.FormID);

                dialogBranch = UpdateBranchStartingTopic(dialogBranch, dialogTopic.FormID);

                ArrBranchesForView.Add(dialogBranch);
                ArrDictBranchesDone.Add(dialogBranch.DescriptiveName, dialogBranch.FormID);

                //add DIAL and INFO
                ArrDialAndInfo.Add(dialogTopic);
                ArrDialAndInfo.Add(CreateDialogGroupINFO(dialogTopic.FormID));
            }
        }

        private void StartCopyQUST()
        {
            if ((TopLevelControl as MainView).ArrRecordsToMove.Count != nCounter)
                throw new NotImplementedException();

            //ugly cheat 
            //DAO splits JournalStringID for Stage and Objective
            uint nJournalSplitCounter = 466417;
            Dictionary<uint, string> dJournal = new Dictionary<uint, string>();

            //(TopLevelControl as MainView).ArrDictNamesSource 0w 1wo
            string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\PLO_900Simple\",
                  "*.xml",
                  System.IO.SearchOption.AllDirectories);

            int nQuestCounter = 0;

            foreach (string _file in files)
            {
                XDocument xdDoc = XDocument.Load(_file);

                IEnumerable<XElement> blobs = from blob in xdDoc.Root.Descendants("StatusList").Descendants("Agent")
                                              where !blob.Parent.Name.ToString().Contains("PlotAssistInfoList")
                                              select blob;

                if (blobs.ToList().Count == 0)
                    continue;

                BaseRecord newR = null;

                nQuestCounter++;

                bool bQuestHasName = false;
                int nName = int.Parse(xdDoc.Root.Descendants("NameStringID").ToList()[0].Value.ToString());

                string sName = string.Empty;
                if (pSource.tlkDao.ContainsKey(nName))
                {
                    sName = pSource.tlkDao[nName];
                    bQuestHasName = true;
                }
                else
                {
                    //throw new NotImplementedException();
                }

                if (blobs.ToList().Count == 0)
                    newR = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse("0007A732", System.Globalization.NumberStyles.HexNumber)).Clone();
                else
                    newR = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse("0006A086", System.Globalization.NumberStyles.HexNumber)).Clone();

                if (String.IsNullOrEmpty(newR.Name))
                    throw new NotImplementedException();

                string sEdid = xdDoc.Root.Descendants("ResRefName").ToList()[0].Value;

                if (sEdid == string.Empty)
                    throw new NotImplementedException();

                string sSubrec = string.Empty;
                string sRecordType = newR.Name;

                string[] stage = new string[] { "INDX", "QSDT", "CNAM" };
                string[] objective = new string[] { "QOBJ", "FNAM", "NNAM", "QSTA" };

                List<SubRecord> templStage = new List<SubRecord>();
                List<SubRecord> templObjective = new List<SubRecord>();
                var templR = newR.Clone();

                foreach (var sr in (templR as Record).SubRecords)
                {
                    if (templStage.Count == stage.Length)
                        break;

                    if (sr.Name == "INDX")
                        templStage.Add(sr);
                    else if (sr.Name == "QSDT")
                        templStage.Add(sr);
                    else if (sr.Name == "CNAM")
                        templStage.Add(sr);
                }

                foreach (var sr in (templR as Record).SubRecords)
                {
                    if (templObjective.Count == objective.Length)
                        break;

                    if (sr.Name == "QOBJ")
                        templObjective.Add(sr);
                    else if (sr.Name == "FNAM")
                        templObjective.Add(sr);
                    else if (sr.Name == "NNAM")
                        templObjective.Add(sr);
                    else if (sr.Name == "QSTA")
                        templObjective.Add(sr);
                }

                for (int i = (templR as Record).SubRecords.Count - 1; i >= 0; i--)
                {
                    (templR as Record).SubRecords.RemoveAt(i);
                }

                foreach (var sr in (newR as Record).SubRecords)
                {
                    if (sr.Name == "FULL" && bQuestHasName)
                    {
                        (templR as Record).SubRecords.Add(sr);
                    }
                    else if (sr.Name != "INDX" &&
                        sr.Name != "QSDT" &&
                        sr.Name != "CNAM" &&
                        sr.Name != "QOBJ" &&
                        sr.Name != "FNAM" &&
                        sr.Name != "NNAM" &&
                        sr.Name != "QSTA")
                        (templR as Record).SubRecords.Add(sr);
                }

                templR.SetDescription(' ' + sEdid);

                foreach (var sr in (templR as Record).SubRecords)
                {
                    if (sr.Name == "EDID")
                    {
                        byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sEdid).Length + 1];
                        Array.Copy(Encoding.ASCII.GetBytes(sEdid), plusNull, Encoding.ASCII.GetBytes(sEdid).Length);

                        sr.SetData(plusNull);
                        break;
                    }
                }

                //increment counter formID
                (templR as Record).FormID = (TopLevelControl as MainView).CounterFormID;
                (TopLevelControl as MainView).CounterFormID++;

                string hexName = nName.ToString("X8");
                string hexName6 = nName.ToString("X6");

                byte[] arrNameFull = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
                arrNameFull = arrNameFull.Reverse().ToArray();

                foreach (var sr in (templR as Record).SubRecords)
                {
                    if (sr.Name == "FULL")
                    {
                        sr.SetData(arrNameFull);

                        XElement xString = new XElement("String");
                        xString.Add(new XAttribute("List", 0));
                        xString.Add(new XAttribute("sID", hexName6));

                        XElement xEdid = new XElement("EDID", sEdid);
                        xString.Add(xEdid);

                        XElement xRec = new XElement("REC", sRecordType + ':' + sr.Name);
                        xString.Add(xRec);

                        XElement xSource = new XElement("Source", String.IsNullOrEmpty(sName) ? sEdid : sName);
                        xString.Add(xSource);

                        XElement xDest = new XElement("Dest", String.IsNullOrEmpty(sName) ? sEdid : sName);
                        xString.Add(xDest);

                        xContent.Add(xString);
                    }
                }

                List<SubRecord> subStage = new List<SubRecord>();
                List<SubRecord> subObjective = new List<SubRecord>();

                //TODO Repeatable

                int nStageCNAMMax = 0;
                int nObjectiveNNAMMax = 0;

                //add stages and objectives
                foreach (var blob in blobs)
                {
                    bool bHasJournal = false;

                    if (blob.Descendants("JournalText").ToList()[0].Value.ToString() != string.Empty)
                    {
                        bHasJournal = true;
                    }

                    bool bHasObjective = false;
                    int nFlag = Convert.ToInt32(blob.Descendants("Flag").ToList()[0].Value) * 10;

                    if (nFlag > 3999)
                        throw new NotImplementedException();

                    int nFinal = blob.Descendants("Final").ToList()[0].Value == "True" ? 1 : 0;
                    int nJournal = Convert.ToInt32(blob.Descendants("JournalTextStringID").ToList()[0].Value);

                    string sJournalAll = string.Empty;
                    string[] arrJournal = new string[2];

                    if (bHasJournal && pSource.tlkDao.ContainsKey(nJournal))
                        sJournalAll = pSource.tlkDao[nJournal];

                    if (sJournalAll.Contains(@"{/emp}\n"))
                    {
                        arrJournal = sJournalAll.Split(new string[] { @"{/emp}\n" }, StringSplitOptions.None);
                        arrJournal[0] = arrJournal[0].Split(new string[] { @"{emp}" }, StringSplitOptions.None)[1];
                        bHasObjective = true;
                    }
                    else arrJournal[1] = sJournalAll;

                    //stage
                    //INDX Stage_Index UShort = Flag | 0
                    //QSDT Stage_Flags Byte = Final | 1
                    //CNAM Log_Entry_Text FormID = JournalText[1] | 2 on List 1
                    foreach (var ts in templStage)
                    {
                        BaseRecord tt = (ts as SubRecord).Clone();

                        if ((tt as SubRecord).Name == "INDX")
                        {
                            string sHex = nFlag.ToString("X4");//short

                            byte[] arrName = Enumerable.Range(0, sHex.Length / 2).Select(x => Convert.ToByte(sHex.Substring(x * 2, 2), 16)).ToArray();
                            arrName = arrName.Reverse().ToArray();

                            byte[] arrFinal = new byte[4];
                            arrFinal[0] = arrName[0];
                            arrFinal[1] = arrName[1];
                            arrFinal[2] = (tt as SubRecord).GetData()[2];
                            arrFinal[3] = (tt as SubRecord).GetData()[3];

                            (tt as SubRecord).SetData(arrFinal);

                            subStage.Add(tt as SubRecord);
                        }
                        else if ((tt as SubRecord).Name == "QSDT")
                        {
                            string sHex = nFinal.ToString("X2");//byte

                            byte[] arrName = Enumerable.Range(0, sHex.Length / 2).Select(x => Convert.ToByte(sHex.Substring(x * 2, 2), 16)).ToArray();
                            arrName = arrName.Reverse().ToArray();

                            (tt as SubRecord).SetData(arrName);

                            subStage.Add(tt as SubRecord);
                        }
                        else if ((tt as SubRecord).Name == "CNAM")
                        {
                            if (bHasJournal)
                            {
                                string sHex = nJournal.ToString("X8");//FormID/UInt

                                byte[] arrName = Enumerable.Range(0, sHex.Length / 2).Select(x => Convert.ToByte(sHex.Substring(x * 2, 2), 16)).ToArray();
                                arrName = arrName.Reverse().ToArray();

                                (tt as SubRecord).SetData(arrName);

                                subStage.Add(tt as SubRecord);

                                nStageCNAMMax++;
                            }
                        }
                    }

                    if (bHasObjective)
                    {
                        //objective
                        //QOBJ Quest_Objective_Index UShort = Flag | 0
                        //FNAM 4xBytes same | 1
                        //NNAM Text FormID = JournalText[0] | 2
                        //QSTA 2xUInt same | 3
                        foreach (var to in templObjective)
                        {
                            BaseRecord tt = (to as SubRecord).Clone();

                            if ((tt as SubRecord).Name == "QOBJ")
                            {
                                string sHex = nFlag.ToString("X4");//short

                                byte[] arrName = Enumerable.Range(0, sHex.Length / 2).Select(x => Convert.ToByte(sHex.Substring(x * 2, 2), 16)).ToArray();
                                arrName = arrName.Reverse().ToArray();

                                (tt as SubRecord).SetData(arrName);

                                subObjective.Add((tt as SubRecord));
                            }
                            else if ((tt as SubRecord).Name == "FNAM")
                            {
                                subObjective.Add((tt as SubRecord));
                            }
                            else if ((tt as SubRecord).Name == "NNAM")
                            {
                                string sHex = nJournalSplitCounter.ToString("X8");//FormID/UInt
                                dJournal.Add(nJournalSplitCounter, arrJournal[0]);

                                nJournalSplitCounter++;//increment

                                byte[] arrName = Enumerable.Range(0, sHex.Length / 2).Select(x => Convert.ToByte(sHex.Substring(x * 2, 2), 16)).ToArray();
                                arrName = arrName.Reverse().ToArray();

                                (tt as SubRecord).SetData(arrName);

                                subObjective.Add((tt as SubRecord));

                                nObjectiveNNAMMax++;
                            }
                            else if ((tt as SubRecord).Name == "QSTA")
                            {
                                subObjective.Add((tt as SubRecord));
                            }
                        }
                    }
                }

                //insert Stages and Objectives
                int nInsert = -1;
                for (int i = 0; i < (templR as Record).SubRecords.Count; i++)
                {
                    if ((templR as Record).SubRecords[i].Name == "NEXT")
                    {
                        nInsert = i;
                        break;
                    }
                }

                if (nInsert == -1)
                    throw new NotImplementedException();

                int nStageIDCounter = 0;
                int nObjectiveIDCounter = 0;

                foreach (var t in subStage)
                {
                    nInsert++;
                    (templR as Record).SubRecords.Insert(nInsert, t);

                    if (t.Name == "CNAM")
                    {
                        string sHex = String.Concat(Array.ConvertAll(t.GetData().Reverse().ToArray(), x => x.ToString("X2")));
                        sHex = sHex.TrimStart('0');
                        int nString = int.Parse(sHex, System.Globalization.NumberStyles.HexNumber);

                        //add to Talk file
                        XElement xString = new XElement("String");
                        xString.Add(new XAttribute("List", 1));
                        xString.Add(new XAttribute("sID", nString.ToString("X6")));

                        string sJournalAll = string.Empty;
                        string[] arrJournal = new string[2];

                        if (pSource.tlkDao.ContainsKey(nString))
                            sJournalAll = pSource.tlkDao[nString];

                        if (sJournalAll.Contains(@"{/emp}\n"))
                        {
                            arrJournal = sJournalAll.Split(new string[] { @"{/emp}\n" }, StringSplitOptions.None);
                            arrJournal[0] = arrJournal[0].Split(new string[] { @"{emp}" }, StringSplitOptions.None)[1];
                        }
                        else arrJournal[1] = sJournalAll;

                        XElement xEdid = new XElement("EDID", sEdid);
                        xString.Add(xEdid);

                        XElement xRec = new XElement("REC", sRecordType + ':' + (t as SubRecord).Name);
                        xRec.Add(new XAttribute("id", nStageIDCounter));
                        xRec.Add(new XAttribute("idMax", nStageCNAMMax));
                        xString.Add(xRec);

                        if (string.IsNullOrEmpty(arrJournal[1]))
                            throw new NotImplementedException();

                        XElement xSource = new XElement("Source", arrJournal[1]);
                        xString.Add(xSource);

                        XElement xDest = new XElement("Dest", arrJournal[1]);
                        xString.Add(xDest);

                        xContent.Add(xString);

                        nStageIDCounter++;
                    }
                }

                uint lastNString = 0;

                foreach (var t in subObjective)
                {
                    nInsert++;
                    (templR as Record).SubRecords.Insert(nInsert, t);

                    if (t.Name == "NNAM")
                    {
                        string sHex = String.Concat(Array.ConvertAll(t.GetData().Reverse().ToArray(), x => x.ToString("X2")));
                        sHex = sHex.TrimStart('0');
                        uint nString = uint.Parse(sHex, System.Globalization.NumberStyles.HexNumber);

                        if (nString == lastNString)
                            throw new NotImplementedException();

                        //add to Talk file
                        XElement xString = new XElement("String");
                        xString.Add(new XAttribute("List", 0));
                        xString.Add(new XAttribute("sID", nString.ToString("X6")));

                        XElement xEdid = new XElement("EDID", sEdid);
                        xString.Add(xEdid);

                        XElement xRec = new XElement("REC", sRecordType + ':' + (t as SubRecord).Name);
                        xRec.Add(new XAttribute("id", nObjectiveIDCounter));
                        xRec.Add(new XAttribute("idMax", nObjectiveNNAMMax));
                        xString.Add(xRec);

                        if (string.IsNullOrEmpty(dJournal[nString]))
                            throw new NotImplementedException();

                        XElement xSource = new XElement("Source", dJournal[nString]);
                        xString.Add(xSource);

                        XElement xDest = new XElement("Dest", dJournal[nString]);
                        xString.Add(xDest);

                        xContent.Add(xString);

                        nObjectiveIDCounter++;
                        lastNString = nString;
                    }
                }

                BaseRecord[] record = new BaseRecord[1];
                record[0] = templR;
                object[] nodes = new object[2];
                nodes[0] = record;
                nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
            }

            //save counter formID + talkID
            string[] lines = new string[1];
            uint mF = (TopLevelControl as MainView).CounterFormID++;
            lines[0] = mF.ToString();

            using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
            {
                newTask.WriteLine(lines[0].ToString());
            }

            //save skyrim dao talk text
            xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english_DAO.xml");
        }

        private void StartCopyINFO()
        {
            if ((TopLevelControl as MainView).ArrRecordsToMove.Count != nCounter)
                throw new NotImplementedException();

            foreach (var rtm in (TopLevelControl as MainView).ArrDictNamesSource)
            {
                var newR = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse(rtm.Value, System.Globalization.NumberStyles.HexNumber)).Clone();

                if (String.IsNullOrEmpty(newR.Name))
                    throw new NotImplementedException();

                string sEdid = string.Empty;
                string sSubrec = string.Empty;
                string sRecordType = newR.Name;

                newR.SetDescription(' ' + sEdid);

                foreach (var sr in (newR as Record).SubRecords)
                {
                    if (sr.Name == "EDID")
                    {
                        byte[] plusNull = new byte[Encoding.ASCII.GetBytes(rtm.Key).Length + 1];
                        Array.Copy(Encoding.ASCII.GetBytes(rtm.Key), plusNull, Encoding.ASCII.GetBytes(rtm.Key).Length);

                        sr.SetData(plusNull);
                        sEdid = rtm.Key;
                        break;
                    }
                }

                if (sEdid == string.Empty)
                    throw new NotImplementedException();

                //increment counter formID
                (newR as Record).FormID = (TopLevelControl as MainView).CounterFormID;
                (TopLevelControl as MainView).CounterFormID++;

                XDocument xdDoc = XDocument.Load(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTI_1261Simple\" + rtm.Key + ".xml");
                int nName = int.Parse(xdDoc.Root.Descendants("NameStringID").ToList()[0].Value.ToString());
                int nDesc = int.Parse(xdDoc.Root.Descendants("DescriptionStringID").ToList()[0].Value.ToString());

                string sName = pSource.tlkDao[nName];

                string sDesc = string.Empty;
                bool bHasDesc = false;
                if (pSource.tlkDao.ContainsKey(nDesc))
                {
                    sDesc = pSource.tlkDao[nDesc];
                    bHasDesc = true;
                }

                string hexName = nName.ToString("X8");
                string hexName6 = nName.ToString("X6");

                string hexDesc = string.Empty;
                string hexDesc6 = string.Empty;

                if (bHasDesc)
                {
                    hexDesc = nDesc.ToString("X8");
                    hexDesc6 = nDesc.ToString("X8");
                }

                byte[] bName = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
                bName = bName.Reverse().ToArray();

                if (bHasDesc)
                {
                    byte[] bDesc = Enumerable.Range(0, hexDesc.Length / 2).Select(x => Convert.ToByte(hexDesc.Substring(x * 2, 2), 16)).ToArray();
                    bDesc = bDesc.Reverse().ToArray();
                }

                //update talk strings
                foreach (var sr in (newR as Record).SubRecords)
                {
                    if (sr.Name == "FULL")
                    {
                        sr.SetData(bName);

                        XElement xString = new XElement("String");
                        xString.Add(new XAttribute("List", 0));
                        xString.Add(new XAttribute("sID", hexName6));

                        XElement xEdid = new XElement("EDID", sEdid);
                        xString.Add(xEdid);

                        XElement xRec = new XElement("REC", sRecordType + ':' + sr.Name);
                        xString.Add(xRec);

                        XElement xSource = new XElement("Source", sName);
                        xString.Add(xSource);

                        XElement xDest = new XElement("Dest", sName);
                        xString.Add(xDest);

                        xContent.Add(xString);
                    }

                    if (sr.Name == "DESC" && bHasDesc)
                    {
                        sr.SetData(bName);

                        XElement xString = new XElement("String");
                        xString.Add(new XAttribute("List", 1));
                        xString.Add(new XAttribute("sID", hexDesc6));

                        XElement xEdid = new XElement("EDID", sEdid);
                        xString.Add(xEdid);

                        XElement xRec = new XElement("REC", sRecordType + ':' + sr.Name);
                        xString.Add(xRec);

                        XElement xSource = new XElement("Source", sDesc);
                        xString.Add(xSource);

                        XElement xDest = new XElement("Dest", sDesc);
                        xString.Add(xDest);

                        xContent.Add(xString);
                    }
                }

                BaseRecord[] record = new BaseRecord[1];
                record[0] = newR;
                object[] nodes = new object[2];
                nodes[0] = record;
                nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
            }

            //save counter formID + talkID
            string[] lines = new string[1];
            uint mF = (TopLevelControl as MainView).CounterFormID++;
            lines[0] = mF.ToString();

            using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
            {
                newTask.WriteLine(lines[0].ToString());
            }

            //save skyrim dao talk text
            xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english_DAO.xml");
        }

        private void StartCopyNPC_()
        {
            if ((TopLevelControl as MainView).ArrRecordsToMove.Count != nCounter)
                throw new NotImplementedException();

            string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\UTC_Simple\",
                  "*.xml",
                  System.IO.SearchOption.AllDirectories);

            foreach (string _file in files)
            {
                XDocument xdDoc = XDocument.Load(_file);

                BaseRecord newR = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse(GetTemplateFormID(xdDoc), System.Globalization.NumberStyles.HexNumber)).Clone(); ;

                if (newR == null)
                    Console.WriteLine();

                int nName = int.MaxValue;

                //ugly
                if (xdDoc.Root.Descendants("NameStringID").ToList()[0].Value.ToString() != "-1")
                    nName = int.Parse(xdDoc.Root.Descendants("NameStringID").ToList()[0].Value.ToString());

                string sEdid = xdDoc.Root.Descendants("ResRefName").ToList()[0].Value;

                string sName = string.Empty;
                if (pSource.tlkDao.ContainsKey(nName))
                    sName = pSource.tlkDao[nName];
                else
                    sName = sEdid;

                if (sEdid == string.Empty)
                    throw new NotImplementedException();

                string sSubrec = string.Empty;
                string sRecordType = newR.Name;

                newR.SetDescription(' ' + sEdid);

                foreach (var sr in (newR as Record).SubRecords)
                {
                    if (sr.Name == "EDID")
                    {
                        byte[] plusNull = new byte[Encoding.ASCII.GetBytes(sEdid).Length + 1];
                        Array.Copy(Encoding.ASCII.GetBytes(sEdid), plusNull, Encoding.ASCII.GetBytes(sEdid).Length);

                        sr.SetData(plusNull);
                        break;
                    }
                }

                //increment counter formID
                (newR as Record).FormID = (TopLevelControl as MainView).CounterFormID;
                (TopLevelControl as MainView).CounterFormID++;

                string hexName = nName.ToString("X8");
                string hexName6 = nName.ToString("X6");

                byte[] arrNameFull = Enumerable.Range(0, hexName.Length / 2).Select(x => Convert.ToByte(hexName.Substring(x * 2, 2), 16)).ToArray();
                arrNameFull = arrNameFull.Reverse().ToArray();

                foreach (var sr in (newR as Record).SubRecords)
                {
                    if (sr.Name == "FULL")
                    {
                        sr.SetData(arrNameFull);

                        XElement xString = new XElement("String");
                        xString.Add(new XAttribute("List", 0));
                        xString.Add(new XAttribute("sID", hexName6));

                        XElement xEdid = new XElement("EDID", sEdid);
                        xString.Add(xEdid);

                        XElement xRec = new XElement("REC", sRecordType + ':' + sr.Name);
                        xString.Add(xRec);

                        XElement xSource = new XElement("Source", String.IsNullOrEmpty(sName) ? sEdid : sName);
                        xString.Add(xSource);

                        XElement xDest = new XElement("Dest", String.IsNullOrEmpty(sName) ? sEdid : sName);
                        xString.Add(xDest);

                        xContent.Add(xString);
                    }

                    if (sr.Name == "SHRT")
                    {
                        sr.SetData(arrNameFull);

                        XElement xString = new XElement("String");
                        xString.Add(new XAttribute("List", 0));
                        xString.Add(new XAttribute("sID", hexName6));

                        XElement xEdid = new XElement("EDID", sEdid);
                        xString.Add(xEdid);

                        XElement xRec = new XElement("REC", sRecordType + ':' + sr.Name);
                        xString.Add(xRec);

                        string sString = String.IsNullOrEmpty(sName) ? sEdid : sName;
                        if (sString.Contains(' '))
                            sString = sString.Split(' ')[0];

                        XElement xSource = new XElement("Source", sString);
                        xString.Add(xSource);

                        XElement xDest = new XElement("Dest", sString);
                        xString.Add(xDest);

                        xContent.Add(xString);
                    }
                }

                //mix wynne with morrigan
                //and oghre, shale, sten with vilkas
                if (sEdid == "gen00fl_wynne")
                {
                    BaseRecord rMorrigan = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse((TopLevelControl as MainView).ArrDictNamesSource["gen00fl_morrigan"], System.Globalization.NumberStyles.HexNumber)).Clone();

                    newR = MixRecord(newR, rMorrigan).Clone();
                }
                else if (sEdid == "gen00fl_oghren" ||
                    sEdid == "gen00fl_shale" ||
                    sEdid == "gen00fl_sten")
                {
                    BaseRecord rVilkas = (TopLevelControl as MainView).ArrRecordsToMove.FirstOrDefault(x => x.FormID == uint.Parse((TopLevelControl as MainView).ArrDictNamesSource["gen00fl_2H"], System.Globalization.NumberStyles.HexNumber)).Clone();

                    newR = MixRecord(newR, rVilkas).Clone();
                }

                BaseRecord[] record = new BaseRecord[1];
                record[0] = newR;
                object[] nodes = new object[2];
                nodes[0] = record;
                nodes[1] = PluginList.All.Records.OfType<BaseRecord>().ToList()[1];

                int res = (TopLevelControl as MainView).CopyRecordsTo(nodes);
            }

            //save counter formID + talkID
            string[] lines = new string[1];
            uint mF = (TopLevelControl as MainView).CounterFormID++;
            lines[0] = mF.ToString();

            using (StreamWriter newTask = new StreamWriter(@"d:\Work\C#\Snip\dev-nogardeht\!Work\counter.txt", false))
            {
                newTask.WriteLine(lines[0].ToString());
            }

            //save skyrim dao talk text
            xdDocument.Save(@"d:\Work\C#\Snip\dev-nogardeht\!Work\XML\Skyrim_english_english_DAO.xml");
        }

        private BaseRecord MixRecord(BaseRecord rRec, BaseRecord rMix)
        {
            BaseRecord newR = rRec.Clone();
            BaseRecord newMix = rMix.Clone();

            foreach (var srm in (newMix as Record).SubRecords)
            {
                if (srm.Name == "DNAM")
                {
                    bool bFound = false;

                    foreach (var srn in (newR as Record).SubRecords)
                    {
                        if (srn.Name == "DNAM")
                        {
                            srn.SetData(srm.GetData());
                            bFound = true;
                            break;
                        }
                    }

                    if (bFound)
                        break;
                }
            }

            return newR;
        }

        private string GetTemplateFormID(XDocument xdDoc)
        {
            string sResRefName = xdDoc.Root.Descendants("ResRefName").ToList()[0].Value;
            string sName = xdDoc.Root.Descendants("name").ToList()[0].Value.ToString().ToLower();

            int nRace = Convert.ToInt32(xdDoc.Root.Descendants("Race").ToList()[0].Value);
            int nGender = Convert.ToInt32(xdDoc.Root.Descendants("Gender").ToList()[0].Value);
            int nGroup = Convert.ToInt32(xdDoc.Root.Descendants("Group").ToList()[0].Value);
            int nTeam = Convert.ToInt32(xdDoc.Root.Descendants("Team").ToList()[0].Value);
            int nClass = Convert.ToInt32(xdDoc.Root.Descendants("Class").ToList()[0].Value);
            int nAppearance = Convert.ToInt32(xdDoc.Root.Descendants("Appearance").ToList()[0].Value);

            string sRace = (TopLevelControl as MainView).GetRace(nRace);
            string sGender = ((nGender == 2) ? "Female" : "Male");
            string sGroup = (TopLevelControl as MainView).GetGroup(nGroup);
            string sTeamp = nTeam.ToString();
            string sClass = (TopLevelControl as MainView).GetClass(nClass);
            string sApp = ((TopLevelControl as MainView).GetAppearance(nAppearance).Split('_')[1]);

            if ((TopLevelControl as MainView).ArrDictNamesSource.ContainsKey(sApp))
                return (TopLevelControl as MainView).ArrDictNamesSource[sApp];
            else
            {
                if (sName.Contains("soldier") || sName.Contains("guard"))
                {
                    string sHeader = GetHeader(sResRefName);
                    if (sHeader == "arl")
                        return (TopLevelControl as MainView).ArrDictNamesSource["Whiterun Soldier"];
                    else if (sHeader == "bdc" || sHeader == "bdn" || sHeader == "orz")
                        return (TopLevelControl as MainView).ArrDictNamesSource["Windhelm Soldier"];
                    else if (sHeader == "bec" || sHeader == "bed" || sHeader == "ntb")
                        return (TopLevelControl as MainView).ArrDictNamesSource["Riften Soldier"];
                    else if (sHeader == "bhm" || sHeader == "cir")
                        return (TopLevelControl as MainView).ArrDictNamesSource["Winterhold Soldier"];
                    else if (sHeader == "bhn" || sHeader == "cli" || sHeader == "lot")
                        return (TopLevelControl as MainView).ArrDictNamesSource["Morthal Soldier"];
                    else if (sHeader == "lit")
                        return (TopLevelControl as MainView).ArrDictNamesSource["Falkreath Soldier"];
                    else if (sHeader == "den" || sHeader == "epi")
                        return (TopLevelControl as MainView).ArrDictNamesSource["Dawnstar Soldier"];
                    else //pre/ran
                        return (TopLevelControl as MainView).ArrDictNamesSource["Solitude Soldier"];
                }
                else if (sResRefName.Contains("gen00fl_"))
                {
                    return (TopLevelControl as MainView).ArrDictNamesSource[sResRefName];
                }
                else if (sGroup == "GROUP_FRIENDLY")
                {
                    return (TopLevelControl as MainView).ArrDictNamesSource[sApp + ' ' + sGender];
                }
                else
                {
                    if (sClass == "CLASS_WIZARD")
                        return (TopLevelControl as MainView).ArrDictNamesSource[sApp + ' ' + "Mage"];
                    else if (sClass == "CLASS_ROGUE")
                        return (TopLevelControl as MainView).ArrDictNamesSource[sApp + ' ' + "Range"];
                    else
                        return (TopLevelControl as MainView).ArrDictNamesSource[sApp + ' ' + "Melee"];
                }
            }
        }

        private string GetFNV64Hashed(string sString)
        {
            ulong FnvPrime = 0x00000100000001B3;
            ulong _hash = 0xCBF29CE484222325;

            foreach (char cc in sString.ToCharArray())
            {
                unchecked
                {
                    _hash ^= (byte)cc;
                    _hash *= FnvPrime;
                }
            }

            return _hash.ToString("x8");
        }

        private string GetHeader(string sResRefName)
        {
            char[] c = sResRefName.ToCharArray();
            return new string(new char[] { c[0], c[1], c[2] });
        }

        private int ConvertEmotions(DialogConnector line)
        {
            int e = -1;
            if (IsStartBranch(line))
                e = line.LineNodes[0].Emotion;
            else if (IsPlayer(line))
                e = line.LineNodes[1].Emotion;
            else
                Console.WriteLine();

            switch (e)
            {
                case 1: return 7;//angry/confused->Puzzled
                case 2: return 1;//angry/default->Anger
                case 3: return 2;//angry/rage->Disgust
                case 4: return 7;//angry/thinking->Puzzled
                case 5: return 6;//flirt/checkout->Surprise
                case 6: return 6;//flirt/coy->Surprise
                case 7: return 6;//flirt/default->Surprise
                case 8: return 5;//happy/default->Happy
                case 9: return 5;//happy/fake->Happy
                case 10: return 3;//happy/nervous->Fear
                case 11: return 5;//happy/overjoyed->Happy
                case 12: return 7;//happy/thinking->Puzzled
                case 13: return 4;//sad/default->Sad
                case 14: return 3;//sad/devastated->Fear
                case 15: return 7;//sad/nervous->Puzzled
                case 16: return 6;//sad/thinking->Surprize
                default: return 0;//default->neutral
            }
        }

        private FConversation ParseConversation(XDocument xdDoc)
        {
            var conversation = new FConversation
            {
                ResRefID = Convert.ToInt32(xdDoc.Root.Attribute("ResRefID").Value),
                ResRefName = xdDoc.Root.Attribute("ResRefName").Value
            };

            XElement StartList = xdDoc.Root.Descendants().First().Descendants("StartList").First();
            XElement NPCLineList = xdDoc.Root.Descendants().First().Descendants("NPCLineList").First();
            XElement PlayerLineList = xdDoc.Root.Descendants().First().Descendants("PlayerLineList").First();

            //Start Branches
            foreach (var s in StartList.Descendants("LineIndex"))
            {
                conversation.StartList.Add(Convert.ToInt32(s.Value));
            }

            //TODO AnimationListList

            //NPC
            IEnumerable<XElement> npcLines = from blob in NPCLineList.Descendants("Agent")
                                             where blob.Parent.Name.ToString().Contains("NPCLineList")
                                             select blob;

            for (int i = 0; i < npcLines.ToList().Count; i++)
            {
                var n = npcLines.ToList()[i];
                FConvNode npcNode = new FConvNode
                {
                    lineIndex = i
                };

                foreach (var d in n.Descendants())
                {
                    if (d.Name == "StringID") npcNode.StringID = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "LanguageID") npcNode.LanguageID = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionScriptURI") npcNode.ConditionScriptURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionParameter") npcNode.ConditionParameter = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionParameterText") npcNode.ConditionParameterText = d.Value;
                    if (d.Name == "ConditionPlotURI") npcNode.ConditionPlotURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionPlotFlag") npcNode.ConditionPlotFlag = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionResult") npcNode.ConditionResult = d.Value == "False" ? false : true;
                    if (d.Name == "ActionScriptURI") npcNode.ActionScriptURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionParameter") npcNode.ActionParameter = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionParameterText") npcNode.ActionParameterText = d.Value;
                    if (d.Name == "ActionPlotURI") npcNode.ActionPlotURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionPlotFlag") npcNode.ActionPlotFlag = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionResult") npcNode.ActionResult = d.Value == "False" ? false : true;
                    if (d.Name == "text") npcNode.text = d.Value;
                    if (d.Name == "TextRequiresReTranslation") npcNode.TextRequiresReTranslation = d.Value == "False" ? false : true;
                    if (d.Name == "TextRequiresReRecording") npcNode.TextRequiresReRecording = d.Value == "False" ? false : true;
                    if (d.Name == "Speaker") npcNode.Speaker = d.Value;
                    if (d.Name == "PreviousSpeaker") npcNode.PreviousSpeaker = d.Value;
                    if (d.Name == "Listener") npcNode.Listener = d.Value;
                    if (d.Name == "icon") npcNode.icon = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "Comment") npcNode.Comment = d.Value;
                    if (d.Name == "FastPath") npcNode.FastPath = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "SlideShowTexture") npcNode.SlideShowTexture = d.Value;
                    if (d.Name == "VoiceOverTag") npcNode.VoiceOverTag = d.Value;
                    if (d.Name == "VoiceOverComment") npcNode.VoiceOverComment = d.Value;
                    if (d.Name == "EditorComment") npcNode.EditorComment = d.Value;
                    if (d.Name == "LineVisibility") npcNode.LineVisibility = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "Ambient") npcNode.Ambient = d.Value == "False" ? false : true;
                    if (d.Name == "SkipLine") npcNode.SkipLine = d.Value == "False" ? false : true;
                    if (d.Name == "StageURI") npcNode.StageURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "StageTag") npcNode.StageTag = d.Value;
                    if (d.Name == "StageAtCurrentLocation") npcNode.StageAtCurrentLocation = d.Value == "False" ? false : true;
                    if (d.Name == "CameraTag") npcNode.CameraTag = d.Value;
                    if (d.Name == "CameraLocked") npcNode.CameraLocked = d.Value == "False" ? false : true;
                    if (d.Name == "SecondaryCameratag") npcNode.SecondaryCameratag = d.Value;
                    if (d.Name == "SecondaryCameraDelay") npcNode.SecondaryCameraDelay = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToSingle(d.Value);
                    if (d.Name == "Emotion") npcNode.Emotion = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "CustomCutsceneURI") npcNode.CustomCutsceneURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "SpeakerAnimation") npcNode.SpeakerAnimation = d.Value;
                    if (d.Name == "RevertAnimation") npcNode.RevertAnimation = d.Value == "False" ? false : true;
                    if (d.Name == "LockAnimations") npcNode.LockAnimations = d.Value == "False" ? false : true;
                    if (d.Name == "PlaySoundEvents") npcNode.PlaySoundEvents = d.Value == "False" ? false : true;
                    if (d.Name == "RoboBradSeed") npcNode.RoboBradSeed = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "RoboBradSeedOverride") npcNode.RoboBradSeedOverride = d.Value == "False" ? false : true;
                    if (d.Name == "RoboBradLocked") npcNode.RoboBradLocked = d.Value == "False" ? false : true;
                    if (d.Name == "PreviewAreaURI") npcNode.PreviewAreaURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "PreviewStageUseFirstMatch") npcNode.PreviewStageUseFirstMatch = d.Value == "False" ? false : true;
                    //if (d.Name == "PreviewStagePosition") npcNode.PreviewStagePosition = FVector(0.f);//String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value); 
                    //if (d.Name == "PreviewStageOrientation") npcNode.PreviewStageOrientation = FVector(0.f);//String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value); 
                    if (d.Name == "UseAnimationDuration") npcNode.UseAnimationDuration = d.Value == "False" ? false : true;
                    if (d.Name == "NoVOInGame") npcNode.NoVOInGame = d.Value == "False" ? false : true;
                    if (d.Name == "Narration") npcNode.Narration = d.Value == "False" ? false : true;
                    if (d.Name == "PreCacheVO") npcNode.PreCacheVO = d.Value == "False" ? false : true;

                    if (d.Name == "TransitionList")
                    {
                        var tl = d.Descendants("Agent");

                        foreach (var t in tl)
                        {
                            FConvTransition npcNodeTransition = new FConvTransition();

                            foreach (var tc in t.Descendants())
                            {
                                if (tc.Name == "IsLink") npcNodeTransition.IsLink = tc.Value == "False" ? false : true;
                                if (tc.Name == "LineIndex") npcNodeTransition.LineIndex = Convert.ToInt32(tc.Value);
                            }

                            npcNode.TransitionList.Add(npcNodeTransition);
                        }
                    }
                }

                conversation.NPCLineList.Add(npcNode);
            }

            //Player
            IEnumerable<XElement> playerLines = from blob in PlayerLineList.Descendants("Agent")
                                                where blob.Parent.Name.ToString().Contains("PlayerLineList")
                                                select blob;

            for (int i = 0; i < playerLines.ToList().Count; i++)
            {
                var p = playerLines.ToList()[i];
                FConvNode playerNode = new FConvNode
                {
                    lineIndex = i,
                };

                foreach (var d in p.Descendants())
                {
                    if (d.Name == "StringID") playerNode.StringID = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "LanguageID") playerNode.LanguageID = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionScriptURI") playerNode.ConditionScriptURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionParameter") playerNode.ConditionParameter = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionParameterText") playerNode.ConditionParameterText = d.Value;
                    if (d.Name == "ConditionPlotURI") playerNode.ConditionPlotURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionPlotFlag") playerNode.ConditionPlotFlag = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ConditionResult") playerNode.ConditionResult = d.Value == "False" ? false : true;
                    if (d.Name == "ActionScriptURI") playerNode.ActionScriptURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionParameter") playerNode.ActionParameter = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionParameterText") playerNode.ActionParameterText = d.Value;
                    if (d.Name == "ActionPlotURI") playerNode.ActionPlotURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionPlotFlag") playerNode.ActionPlotFlag = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "ActionResult") playerNode.ActionResult = d.Value == "False" ? false : true;
                    if (d.Name == "text") playerNode.text = d.Value;
                    if (d.Name == "TextRequiresReTranslation") playerNode.TextRequiresReTranslation = d.Value == "False" ? false : true;
                    if (d.Name == "TextRequiresReRecording") playerNode.TextRequiresReRecording = d.Value == "False" ? false : true;
                    if (d.Name == "Speaker") playerNode.Speaker = d.Value;
                    if (d.Name == "PreviousSpeaker") playerNode.PreviousSpeaker = d.Value;
                    if (d.Name == "Listener") playerNode.Listener = d.Value;
                    if (d.Name == "icon") playerNode.icon = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "Comment") playerNode.Comment = d.Value;
                    if (d.Name == "FastPath") playerNode.FastPath = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "SlideShowTexture") playerNode.SlideShowTexture = d.Value;
                    if (d.Name == "VoiceOverTag") playerNode.VoiceOverTag = d.Value;
                    if (d.Name == "VoiceOverComment") playerNode.VoiceOverComment = d.Value;
                    if (d.Name == "EditorComment") playerNode.EditorComment = d.Value;
                    if (d.Name == "LineVisibility") playerNode.LineVisibility = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "Ambient") playerNode.Ambient = d.Value == "False" ? false : true;
                    if (d.Name == "SkipLine") playerNode.SkipLine = d.Value == "False" ? false : true;
                    if (d.Name == "StageURI") playerNode.StageURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "StageTag") playerNode.StageTag = d.Value;
                    if (d.Name == "StageAtCurrentLocation") playerNode.StageAtCurrentLocation = d.Value == "False" ? false : true;
                    if (d.Name == "CameraTag") playerNode.CameraTag = d.Value;
                    if (d.Name == "CameraLocked") playerNode.CameraLocked = d.Value == "False" ? false : true;
                    if (d.Name == "SecondaryCameratag") playerNode.SecondaryCameratag = d.Value;
                    if (d.Name == "SecondaryCameraDelay") playerNode.SecondaryCameraDelay = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToSingle(d.Value);
                    if (d.Name == "Emotion") playerNode.Emotion = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "CustomCutsceneURI") playerNode.CustomCutsceneURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "SpeakerAnimation") playerNode.SpeakerAnimation = d.Value;
                    if (d.Name == "RevertAnimation") playerNode.RevertAnimation = d.Value == "False" ? false : true;
                    if (d.Name == "LockAnimations") playerNode.LockAnimations = d.Value == "False" ? false : true;
                    if (d.Name == "PlaySoundEvents") playerNode.PlaySoundEvents = d.Value == "False" ? false : true;
                    if (d.Name == "RoboBradSeed") playerNode.RoboBradSeed = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "RoboBradSeedOverride") playerNode.RoboBradSeedOverride = d.Value == "False" ? false : true;
                    if (d.Name == "RoboBradLocked") playerNode.RoboBradLocked = d.Value == "False" ? false : true;
                    if (d.Name == "PreviewAreaURI") playerNode.PreviewAreaURI = String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value);
                    if (d.Name == "PreviewStageUseFirstMatch") playerNode.PreviewStageUseFirstMatch = d.Value == "False" ? false : true;
                    //if (d.Name == "PreviewStagePosition") playerNode.PreviewStagePosition = FVector(0.f);//String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value); 
                    //if (d.Name == "PreviewStageOrientation") playerNode.PreviewStageOrientation = FVector(0.f);//String.IsNullOrEmpty(d.Value.ToString()) ? 0 : Convert.ToInt32(d.Value); 
                    if (d.Name == "UseAnimationDuration") playerNode.UseAnimationDuration = d.Value == "False" ? false : true;
                    if (d.Name == "NoVOInGame") playerNode.NoVOInGame = d.Value == "False" ? false : true;
                    if (d.Name == "Narration") playerNode.Narration = d.Value == "False" ? false : true;
                    if (d.Name == "PreCacheVO") playerNode.PreCacheVO = d.Value == "False" ? false : true;

                    if (d.Name == "TransitionList")
                    {
                        var tl = d.Descendants("Agent");

                        foreach (var t in tl)
                        {
                            FConvTransition playerNodeTransition = new FConvTransition();

                            foreach (var tc in t.Descendants())
                            {
                                if (tc.Name == "IsLink") playerNodeTransition.IsLink = tc.Value == "False" ? false : true;
                                if (tc.Name == "LineIndex") playerNodeTransition.LineIndex = Convert.ToInt32(tc.Value);
                            }

                            playerNode.TransitionList.Add(playerNodeTransition);
                        }
                    }
                }

                conversation.PlayerLineList.Add(playerNode);
            }

            //get the Quest references ready for both DAO and Skyrim

            //TODO maybe more efficient?
            var sum = conversation.NPCLineList.Concat(conversation.PlayerLineList);

            foreach (var n in conversation.StartList)
            {
                if (conversation.NPCLineList[n].ConditionPlotURI == 0)
                {
                    string[] filesEmpty = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                  "*Quest_" + conversation.ResRefName + ".xml",
                                  System.IO.SearchOption.TopDirectoryOnly);

                    if (filesEmpty.Length != 1)
                        Console.WriteLine();

                    if (!ArrDictQuestSkyrim.ContainsKey(0))
                        ArrDictQuestSkyrim.Add(0, filesEmpty[0]);//the current Quest_*
                    else
                        ArrDictQuestSkyrim[0] = filesEmpty[0];


                    break;
                }
            }

            foreach (var node in sum)
            {
                //check for bad stuff
                if (node.ConditionPlotURI == 0 && node.ConditionPlotFlag != -1)
                    throw new NotImplementedException();

                if (node.ConditionPlotURI != 0 && node.ConditionPlotFlag == -1)
                    throw new NotImplementedException();

                if (node.ActionPlotURI == 0 && node.ActionPlotFlag != -1)
                    throw new NotImplementedException();

                if (node.ActionPlotURI != 0 && node.ActionPlotFlag == -1)
                    throw new NotImplementedException();

                if (node.ConditionPlotURI != 0)
                {
                    if (!ArrDictQuestDAO.ContainsKey(node.ConditionPlotURI))
                    {
                        //get the quest
                        string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\PLO_900Simple\",
                          "*.xml",
                          System.IO.SearchOption.AllDirectories);

                        foreach (string _file in files)
                        {
                            bool bAdd = false;
                            XDocument xdDocQuest = XDocument.Load(_file);

                            if (xdDocQuest.Root.Attribute("ResRefID").Value == node.ConditionPlotURI.ToString())
                            {
                                if (!ArrDictQuestDAO.ContainsKey(node.ConditionPlotURI))
                                {
                                    ArrDictQuestDAO.Add(node.ConditionPlotURI, _file);
                                    bAdd = true;
                                }

                                if (bAdd)
                                {
                                    string[] filesSkyrim = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                      "*.xml",
                                      System.IO.SearchOption.AllDirectories);

                                    foreach (string _fileSkyrim in filesSkyrim)
                                    {
                                        XDocument xdDocQuestSkyrim = XDocument.Load(_fileSkyrim);

                                        if (xdDocQuestSkyrim.Root.Descendants("EDID").ToList()[0].
                                                Descendants("Name").ToList()[0].Value == xdDocQuest.Root.Descendants("ResRefName").ToList()[0].Value)
                                        {
                                            if (!ArrDictQuestSkyrim.ContainsKey(node.ConditionPlotURI))
                                                ArrDictQuestSkyrim.Add(node.ConditionPlotURI, GetQuestSkyPathFromDao(_file));

                                            break;
                                        }
                                    }
                                }

                                break;
                            }
                        }
                    }
                }

                if (node.ActionPlotURI != 0)
                {
                    if (!ArrDictQuestDAO.ContainsKey(node.ActionPlotURI))
                    {
                        //get the quest
                        string[] files = Directory.GetFiles(@"d:\Work\R_idle\DAO_All\DAO_extracted\XML\PLO_900Simple\",
                              "*.xml",
                              System.IO.SearchOption.AllDirectories);

                        foreach (string _file in files)
                        {
                            bool bAdd = false;
                            XDocument xdDocQuest = XDocument.Load(_file);

                            if (xdDocQuest.Root.Attribute("ResRefID").Value == node.ActionPlotURI.ToString())
                            {
                                if (!ArrDictQuestDAO.ContainsKey(node.ActionPlotURI))
                                {
                                    ArrDictQuestDAO.Add(node.ActionPlotURI, _file);
                                    bAdd = true;
                                }

                                if (bAdd)
                                {
                                    string[] filesSkyrim = Directory.GetFiles(@"g:\Games\Nexus Mod Manager\Misc\SSEEdit\Backups\XML\HajdukAge.esm\QUST\",
                                      "*.xml",
                                      System.IO.SearchOption.AllDirectories);

                                    foreach (string _fileSkyrim in filesSkyrim)
                                    {
                                        XDocument xdDocQuestSkyrim = XDocument.Load(_fileSkyrim);

                                        if (xdDocQuestSkyrim.Root.Descendants("EDID").ToList()[0].
                                                Descendants("Name").ToList()[0].Value == xdDocQuest.Root.Descendants("ResRefName").ToList()[0].Value)
                                        {
                                            if (!ArrDictQuestSkyrim.ContainsKey(node.ActionPlotURI))
                                                ArrDictQuestSkyrim.Add(node.ActionPlotURI, GetQuestSkyPathFromDao(_file));
                                            break;
                                        }
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

            return conversation;
        }

        private void toolStripIncrFindText_TextChanged(object sender, EventArgs e)
        {
            this.ResetSearch();
        }

        private void toolStripIncrFindTypeFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ResetSearch();
        }

        private void toolStripIncrFindType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = this.toolStripIncrFindType.SelectedItem as MRUComboHelper<SearchType, string>;
            if (item != null)
            {
                Settings.Default.LastSearchType = this.toolStripIncrFindType.Text;
            }

            if (item != null && (item.Key == SearchType.TypeEditorIdSearch || item.Key == SearchType.TypeFullSearch))
            {
                this.toolStripIncrFindTypeFilter.Visible = true;
                this.toolStripIncrFindExact.Visible = false;
                this.toolStripSelectColumns.Visible = true;
            }
            else
            {
                this.toolStripIncrFindTypeFilter.Visible = false;
                this.toolStripIncrFindExact.Visible = true;
                this.toolStripSelectColumns.Visible = false;
            }

            if (item != null && item.Key == SearchType.BasicCriteriaRef)
            {
                this.toolStripIncrSelectCriteria.Visible = true;
                this.toolStripIncrFindText.Visible = false;
                this.toolStripIncrFindText.Items.Clear();
                this.toolStripIncrFindGo.Enabled = this.toolStripIncrSelectCriteria.Tag != null;
                this.toolStripSelectColumns.Visible = true;
            }
            else
            {
                this.toolStripIncrFindGo.Enabled = true;
                this.toolStripIncrSelectCriteria.Visible = false;
                this.toolStripIncrFindText.Visible = true;
                this.toolStripIncrFindText.Items.Clear();
                if (item != null && item.MRU != null && item.MRU.Count > 0)
                {
                    this.toolStripIncrFindText.Items.AddRange(item.MRU.OfType<object>().Take(15).ToArray());
                }
            }
        }

        private void toolStripIncrSelectCriteria_Click(object sender, EventArgs e)
        {
            using (var dlg = new SearchFilterBasic())
            {
                dlg.Criteria = this.toolStripIncrSelectCriteria.Tag as SearchCriteriaSettings;
                var result = dlg.ShowDialog(this);
                if (DialogResult.Cancel != result)
                {
                    this.toolStripIncrSelectCriteria.Tag = dlg.Criteria;
                    this.toolStripIncrFindGo.Enabled = dlg.Criteria != null && dlg.Criteria.Items.Any();
                    if (result == DialogResult.Yes)
                    {
                        this.BackgroundIncrementalSearch();
                    }
                }
            }
        }

        private void toolStripSelectColumns_Click(object sender, EventArgs e)
        {
            RecordStructure rec = null;

            var searchTypeItem = this.toolStripIncrFindType.SelectedItem as MRUComboHelper<SearchType, string>;
            if (searchTypeItem == null)
            {
                return;
            }

            if (searchTypeItem.Key == SearchType.BasicCriteriaRef)
            {
                var scs = this.toolStripIncrSelectCriteria.Tag as SearchCriteriaSettings;
                if (scs != null && !string.IsNullOrEmpty(scs.Type))
                {
                    RecordStructure.Records.TryGetValue(scs.Type, out rec);
                }
            }
            else
            {
                var recType = this.toolStripIncrFindTypeFilter.SelectedItem as string;
                if (!string.IsNullOrEmpty(recType))
                {
                    RecordStructure.Records.TryGetValue(recType, out rec);
                }
            }

            using (var dlg = new RecordColumnSelect(rec))
            {
                dlg.Criteria = this.toolStripSelectColumns.Tag as ColumnSettings;
                if (DialogResult.OK == dlg.ShowDialog(this))
                {
                    var settings = dlg.Criteria;
                    this.ApplyColumnSettings(settings, rebuild: true);
                    this.toolStripSelectColumns.Tag = dlg.Criteria;
                }
            }
        }

        private void toolStripSynchronize_Click(object sender, EventArgs e)
        {
            this.SynchronizeSelection();
        }

        private class ComboHelper<T, U>
        {
            public ComboHelper(T key, U value)
            {
                this.Key = key;
                this.Value = value;
            }

            public T Key { get; set; }

            public U Value { get; set; }

            public override string ToString()
            {
                return this.Value.ToString();
            }
        }

        private class MRUComboHelper<T, U> : ComboHelper<T, U>
        {
            private readonly StringCollection mru;

            public MRUComboHelper(T key, U value, StringCollection mru)
                : base(key, value)
            {
                this.mru = mru;
            }

            public StringCollection MRU
            {
                get
                {
                    return this.mru;
                }
            }
        }

        private class SearchResults
        {
            public SearchCriteriaSettings Criteria;

            public bool Partial;

            public AdvancedList<Record> Records = new AdvancedList<Record>();

            public string Rectype;

            public string Text;

            public SearchType Type;
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// Type of search to perform. 
        /// </param>
        /// <param name="text">
        /// Text to search for. 
        /// </param>
        /// <param name="partial">
        /// Allow for partial Text matches. 
        /// </param>
        /// <param name="updateFunc">
        /// Function to call to update the UI when doing select. 
        /// </param>
        private class SearchSettings
        {
            public SearchCriteriaSettings Criteria;

            public bool Partial;

            public string Rectype;

            public string Text;

            public SearchType Type;

            public Predicate<BaseRecord> UpdateFunc;

            public SearchSettings()
            {
                this.Type = SearchType.EditorID;
                this.Text = null;
                this.Partial = true;
                this.Criteria = null;
                this.UpdateFunc = null;
                this.Rectype = null;
            }
        }
    }
}
