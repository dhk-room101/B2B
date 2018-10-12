namespace TESVSnip.Domain.Model
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Windows.Forms;

    using TESVSnip.Domain.Data.Strings;
    using TESVSnip.Framework;
    using TESVSnip.Framework.Persistence;
    using TESVSnip.Framework.Services;

    using TESVSnip.Domain.Services;
    using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
    using System.Xml.Linq;
    using TESVSnip.Domain.Data.RecordStructure;

    public static class RecursiveSelectExtensions
    {
        public static IEnumerable<TSource> RecursiveSelect<TSource>(this IEnumerable<TSource> source,
    Func<TSource, IEnumerable<TSource>> childSelector)
        {
            return RecursiveSelect(source, childSelector, element => element);
        }

        public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
           Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, TResult> selector)
        {
            return RecursiveSelect(source, childSelector, (element, index, depth) => selector(element));
        }

        public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
           Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, int, TResult> selector)
        {
            return RecursiveSelect(source, childSelector, (element, index, depth) => selector(element, index));
        }

        public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
           Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, int, int, TResult> selector)
        {
            return RecursiveSelect(source, childSelector, selector, 0);
        }

        private static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
           Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, int, int, TResult> selector, int depth)
        {
            return source.SelectMany((element, index) => Enumerable.Repeat(selector(element, index, depth), 1)
               .Concat(RecursiveSelect(childSelector(element) ?? Enumerable.Empty<TSource>(),
                  childSelector, selector, depth + 1)));
        }


    }

    public struct ValuePair
    {
        public string sName { get; set; }
        public string sGroup { get; set; }
        public uint uFormID { get; set; }
    }

    //[Persistable(Flags = PersistType.DeclaredOnly)]
    [Serializable]
    public sealed class Plugin : BaseRecord, IDeserializationCallback, IGroupRecord
    {
        // Hash tables for quick FormID lookups
        public readonly Dictionary<uint, Record> FormIDLookup = new Dictionary<uint, Record>();

        public List<Element> lElements = new List<Element>();
        public Dictionary<string, ValuePair> dForms = new Dictionary<string, ValuePair>();
        public IEnumerable<XElement> xElem = null;
        public GroupRecord DialogViews = new GroupRecord("NEW_");
        public GroupRecord DialogBranches = new GroupRecord("NEW_");
        public GroupRecord DialogTopics = new GroupRecord("NEW_");
        public string sDialogOwner = "";
        public XDocument dDialogOwner = null;
        public XDocument docStringsSkyrim = null;
        public string sCurrentFormID = "";
        public GroupRecord Quests = new GroupRecord("NEW_");
        public Dictionary<uint, List<Element>> dialogLines = new Dictionary<uint, List<Element>>();
        public Record rRec = null;
        public int nIndex = 0;
        public List<string> arrays = new List<string>();
        public Dictionary<string, Group> groups = new Dictionary<string, Group>();
        public string sGroupName = "";
        public Dictionary<int, string> tlk = new Dictionary<int, string>();
        public Dictionary<int, string> tlkDao = new Dictionary<int, string>();
        
        public LocalizedStringDict DLStrings = new LocalizedStringDict();

        // Whether the file was filtered on load
        public bool Filtered;

        public uint[] Fixups = new uint[0];

        public LocalizedStringDict ILStrings = new LocalizedStringDict();

        public Plugin[] Masters = new Plugin[0];

        public LocalizedStringDict Strings = new LocalizedStringDict();

        [Persistable]
        private readonly List<Rec> records = new List<Rec>();

        private FileSystemWatcher fsw;

        private BaseRecord parent;

        public Plugin(byte[] data, string name)
        {
            throw new NotImplementedException();

            //Name = name;
            //var br = new BinaryReader(new MemoryStream(data));
            //try
            //{
            //    this.LoadPluginData(br, false, null);

            //    this.FileName = Path.GetFileNameWithoutExtension(name);
            //}
            //finally
            //{
            //    br.Close();
            //}
        }

        public Plugin()
        {
            Name = "New plugin.esp";
        }

        internal Plugin(string FilePath, bool headerOnly)
            : this(FilePath, headerOnly, null)
        {
        }

        internal Plugin(string FilePath, bool headerOnly, string[] recFilter)
        {
            Name = Path.GetFileName(FilePath);
            PluginPath = Path.GetDirectoryName(FilePath);
            FileInfo fi = new FileInfo(FilePath);
            //TODO: Cr�ation du BinaryReader ou FileStream
            //using (var br = new BinaryReader(fi.OpenRead()))
            //{
            //    this.LoadPluginData(br, headerOnly, recFilter);
            //}

            /*byte[] data = File.ReadAllBytes(fi.FullName).Skip(0x94BBDA).ToArray();
            byte[] decompressedData = new byte[10000];
            int decompressedLength = 0;
            using (MemoryStream memory = new MemoryStream(data))
            using (InflaterInputStream inflater = new InflaterInputStream(memory))
                decompressedLength = inflater.Read(decompressedData, 0, decompressedData.Length);

            var str1 = System.Text.Encoding.Default.GetString(decompressedData);

            File.WriteAllBytes("string.txt", decompressedData);
            Console.WriteLine(str1);*/

            FileStream fs = new FileStream(fi.FullName, FileMode.Open);
            try
            {
                this.LoadPluginData(fs, headerOnly, recFilter);
            }
            finally
            {
                fs.Close();
            }

            this.FileName = Path.GetFileNameWithoutExtension(FilePath);
            if (!headerOnly)
            {
                this.StringsFolder = Path.Combine(Path.GetDirectoryName(FilePath), "Strings");
            }

            this.ReloadStrings();

            var res = records[1].Flatten(records.OfType<Rec>());
            Console.WriteLine(res.ToList().Count);

            /*foreach (Rec node in records.RecursiveSelect(node => node.Records))
            {
                Console.WriteLine(node.Name);
            }*/

            //List<Rec> result = records.SelectMany(x => x.Records).ToList();

            var stringwriter = new System.IO.StringWriter();
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Data.RecordStructure.Records));
            /*serializer.Serialize(stringwriter, this.Records as Data.RecordStructure.Records);
            var sString = stringwriter.ToString();
            Console.WriteLine(sString);*/

            var path = "SerializationOverview.xml";
            System.IO.FileStream file = System.IO.File.Create(path);

            serializer.Serialize(file, this.Records[1] as Data.RecordStructure.Records);
            file.Close();

            /*System.Xml.Serialization.XmlSerializer writer =
               new System.Xml.Serialization.XmlSerializer(typeof(Data.RecordStructure.Records));

            var path = //Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + 
                "SerializationOverview.xml";
            System.IO.FileStream file = System.IO.File.Create(path);

            writer.Serialize(file, records);
            file.Close();*/
        }

        /*public string ToXML()
        {
            var stringwriter = new System.IO.StringWriter();
            var serializer = new XmlSerializer(this.GetType());
            serializer.Serialize(stringwriter, this);
            return stringwriter.ToString();
        }

        public static YourClass LoadFromXMLString(string xmlText)
        {
            var stringReader = new System.IO.StringReader(xmlText);
            var serializer = new XmlSerializer(typeof(YourClass));
            return serializer.Deserialize(stringReader) as YourClass;
        }*/

        private Plugin(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public override BaseRecord Parent
        {
            get
            {
                return this.parent;
            }

            internal set
            {
                this.parent = value;
            }
        }

        public override IList Records
        {
            get
            {
                return this.records;
            }
        }

        public override long Size
        {
            get
            {
                long size = 0;
                foreach (Rec rec in this.Records)
                {
                    size += rec.Size2;
                }

                return size;
            }
        }

        public override long Size2
        {
            get
            {
                return this.Size;
            }
        }

        public bool StringsDirty { get; set; }

        private string FileName { get; set; }

        private string StringsFolder { get; set; }

        public static bool GetIsEsm(string FilePath)
        {
            var br = new BinaryReader(File.OpenRead(FilePath));
            try
            {
                string s = ReadRecName(br);
                if (s != "TES4")
                {
                    return false;
                }

                br.ReadInt32();
                return (br.ReadInt32() & 1) != 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                br.Close();
            }
        }

        public bool AddMaster(string masterName)
        {
            Record brcTES4 = this.Records.OfType<Record>().FirstOrDefault(x => x.Name == "TES4");
            if (brcTES4 == null)
            {
                throw new ApplicationException("Plugin lacks a valid TES4 record. Cannot continue.");
            }

            // find existing if already present
            foreach (var mast in brcTES4.SubRecords.Where(x => x.Name == "MAST"))
            {
                var path = mast.GetStrData();
                if (string.Compare(path, masterName, true) == 0)
                {
                    return false;
                }
            }

            int idx = brcTES4.SubRecords.IndexOf(brcTES4.SubRecords.FirstOrDefault(x => x.Name == "INTV"));
            if (idx < 0)
            {
                idx = brcTES4.SubRecords.Count;
            }

            var sbrMaster = new SubRecord();
            sbrMaster.Name = "DATA";
            sbrMaster.SetData(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            brcTES4.InsertRecord(idx, sbrMaster);

            sbrMaster = new SubRecord();
            sbrMaster.Name = "MAST";
            int intCount = Encoding.Instance.GetByteCount(masterName);
            var bteData = new byte[intCount + 1];
            Array.Copy(Encoding.Instance.GetBytes(masterName), bteData, intCount);
            sbrMaster.SetData(bteData);
            brcTES4.InsertRecord(idx, sbrMaster);

            // Fix Masters
            // Update IDs for current record to be +1
            return true;
        }

        public override void AddRecord(BaseRecord br)
        {
            try
            {
                var r = br as Rec;
                if (r == null)
                {
                    throw new TESParserException("Record to add was not of the correct type." + Environment.NewLine + "Plugins can only hold Groups or Records.");
                }

                r.Parent = this;
                this.records.Add(r);
                this.InvalidateCache();
                FireRecordListUpdate(this, this);
            }
            catch (Exception)
            {

                throw;
            }

        }

        public override void AddRecords(IEnumerable<BaseRecord> br)
        {
            try
            {
                if (br.Count(r => !(r is Record || r is GroupRecord)) > 0)
                {
                    throw new TESParserException("Record to add was not of the correct type.\nPlugins can only hold records or other groups.");
                }

                foreach (var r in br)
                {
                    r.Parent = this;
                }

                this.records.AddRange(br.OfType<Rec>());
                FireRecordListUpdate(this, this);
                this.InvalidateCache();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public void Clear()
        {
            foreach (var r in this.records)
            {
                r.Parent = null;
            }

            this.records.Clear();
        }

        public override BaseRecord Clone()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override bool DeleteRecord(BaseRecord br)
        {
            var r = br as Rec;
            if (r == null)
            {
                return false;
            }

            bool result = this.records.Remove(r);
            if (result)
            {
                r.Parent = null;
            }

            this.InvalidateCache();
            FireRecordDeleted(this, r);
            FireRecordListUpdate(this, this);
            return result;
        }

        public override bool DeleteRecords(IEnumerable<BaseRecord> br)
        {
            if (br.Count(r => !(r is Record || r is GroupRecord)) > 0)
            {
                throw new TESParserException("Record to delete was not of the correct type.\nPlugins can only hold records or other groups.");
            }

            var ok = false;
            foreach (Rec r in from Rec r in br where this.records.Remove(r) select r)
            {
                ok = true;
                r.Parent = null;
                FireRecordDeleted(this, r);
            }

            FireRecordListUpdate(this, this);
            this.InvalidateCache();
            return ok;
        }

        public override IEnumerable<BaseRecord> Enumerate(Predicate<BaseRecord> match)
        {
            if (!match(this))
            {
                yield break;
            }

            foreach (BaseRecord r in this.Records)
            {
                foreach (var itm in r.Enumerate(match))
                {
                    yield return itm;
                }
            }
        }

        public override void ForEach(Action<BaseRecord> action)
        {
            base.ForEach(action);
            foreach (BaseRecord r in this.Records)
            {
                r.ForEach(action);
            }
        }

        public override string GetDesc()
        {
            return "[Skyrim plugin]" + Environment.NewLine + "Filename: " + Name + Environment.NewLine + "File size: " + this.Size + Environment.NewLine + "Records: " + this.records.Count;
        }

        public string[] GetMasters()
        {
            Record brcTES4 = this.Records.OfType<Record>().FirstOrDefault(x => x.Name == "TES4");
            if (brcTES4 == null)
            {
                return new string[0];
            }

            return brcTES4.SubRecords.Where(x => x.Name == "MAST").Select(x => x.GetStrData()).ToArray();
        }

        public override int IndexOf(BaseRecord br)
        {
            return this.records.IndexOf(br as Rec);
        }

        public override void InsertRecord(int idx, BaseRecord br)
        {
            var r = br as Rec;
            if (r == null)
            {
                throw new TESParserException("Record to add was not of the correct type." + Environment.NewLine + "Plugins can only hold Groups or Records.");
            }

            r.Parent = this;
            if (idx < 0 || idx > this.records.Count)
            {
                idx = this.records.Count;
            }

            this.records.Insert(idx, r);
            this.InvalidateCache();
            FireRecordListUpdate(this, this);
        }

        public override void InsertRecords(int index, IEnumerable<BaseRecord> br)
        {
            if (br.Count(r => !(r is Record || r is GroupRecord)) > 0)
            {
                throw new TESParserException("Record to add was not of the correct type.\nPlugins can only hold records or other groups.");
            }

            this.records.InsertRange(index, br.OfType<Rec>());
            FireRecordListUpdate(this, this);
            this.InvalidateCache();
        }

        /// <summary>
        ///   Invalidate the FormID Cache.
        /// </summary>
        public void InvalidateCache()
        {
            this.FormIDLookup.Clear();
        }

        public void ReloadStrings()
        {
            if (string.IsNullOrEmpty(this.StringsFolder) || string.IsNullOrEmpty(this.FileName) || !Directory.Exists(this.StringsFolder))
            {
                return;
            }

            string locName = Properties.Settings.Default.LocalizationName;

            if (Directory.GetFiles(this.StringsFolder, this.FileName + "_" + locName + "*").Count() == 0)
            {
                if (locName == "English")
                {
                    return;
                }

                locName = "English";
            }

            string prefix = Path.Combine(this.StringsFolder, this.FileName);
            prefix += "_" + Properties.Settings.Default.LocalizationName;

            System.Text.Encoding enc = Encoding.Instance;
            FontLangInfo fontInfo;
            if (Encoding.TryGetFontInfo(locName, out fontInfo))
            {
                if (fontInfo.CodePage != 1252)
                {
                    enc = System.Text.Encoding.GetEncoding(fontInfo.CodePage);
                }
            }

            this.Strings = this.LoadPluginStrings(enc, LocalizedStringFormat.Base, prefix + ".STRINGS");
            this.ILStrings = this.LoadPluginStrings(enc, LocalizedStringFormat.IL, prefix + ".ILSTRINGS");
            this.DLStrings = this.LoadPluginStrings(enc, LocalizedStringFormat.DL, prefix + ".DLSTRINGS");

            if (Properties.Settings.Default.MonitorStringsFolderForChanges)
            {
                if (this.fsw == null)
                {
                    this.fsw = new FileSystemWatcher(this.StringsFolder, this.FileName + "*");
                    this.fsw.EnableRaisingEvents = true;
                    this.fsw.Changed += delegate { this.ReloadStrings(); };
                }
            }
            else
            {
                if (this.fsw != null)
                {
                    this.fsw.Dispose();
                }

                this.fsw = null;
            }
        }

        public byte[] Save()
        {
            throw new NotImplementedException();

            //var ms = new MemoryStream();
            //var bw = new BinaryWriter(ms);
            //this.SaveData(bw);
            //byte[] b = ms.ToArray();
            //bw.Close();
            //return b;
        }

        public bool TryGetRecordByID(uint key, out Record value)
        {
            this.RebuildCache();
            return this.FormIDLookup.TryGetValue(key, out value);
        }

        /// <summary>
        /// </summary>
        /// <param name="plugins">
        /// </param>
        /// <remarks>
        /// Rules:  order
        /// </remarks>
        public void UpdateReferences(IList<Plugin> plugins)
        {
            var masters = this.GetMasters();
            this.Masters = new Plugin[masters.Length + 1];
            this.Fixups = new uint[masters.Length + 1];
            for (int i = 0; i < masters.Length; ++i)
            {
                var master = plugins.FirstOrDefault(x => string.Compare(masters[i], x.Name, true) == 0);
                this.Masters[i] = master;
                this.Fixups[i] = (uint)((master != null) ? master.GetMasters().Length : 0);
            }

            this.Masters[masters.Length] = this;
            this.Fixups[masters.Length] = (uint)masters.Length;
            this.InvalidateCache();
        }

        public override bool While(Predicate<BaseRecord> action)
        {
            if (!base.While(action))
            {
                return false;
            }

            foreach (BaseRecord r in this.Records)
            {
                if (!r.While(action))
                {
                    return false;
                }
            }

            return true;
        }

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            foreach (BaseRecord rec in this.Records)
            {
                rec.Parent = this;
            }
        }

        internal IEnumerable<KeyValuePair<uint, Record>> EnumerateRecords(string type)
        {
            var list = new Dictionary<uint, string>();

            // search each master reference.  Override any 
            for (int i = 0; i < this.Masters.Length - 1; i++)
            {
                if (this.Masters[i] == null)
                {
                    continue; // missing master
                }

                uint match = this.Fixups[i];
                match <<= 24;
                uint mask = (uint)i << 24;

                // This enumerate misses any records that are children of masters
                foreach (var r in this.Masters[i].Enumerate(
                    r =>
                    {
                        if (r is Record)
                        {
                            if ((type == null || r.Name == type) && (((Record)r).FormID & 0xFF000000) == match)
                            {
                                return true;
                            }
                        }
                        else if (r is GroupRecord)
                        {
                            var gr = (GroupRecord)r;
                            if (gr.groupType != 0 || gr.ContentsType == type)
                            {
                                return true;
                            }
                        }
                        else if (r is Plugin)
                        {
                            return true;
                        }

                        return false;
                    }))
                {
                    if (r is Record)
                    {
                        var r2 = r as Record;
                        yield return new KeyValuePair<uint, Record>((r2.FormID & 0xffffff) | mask, r2);
                    }
                }
            }

            // finally add records of self in to the list
            foreach (var r in this.Enumerate(
                r =>
                {
                    if (r is Record)
                    {
                        if (type == null || r.Name == type)
                        {
                            return true;
                        }
                    }
                    else if (r is GroupRecord)
                    {
                        var gr = (GroupRecord)r;
                        if (gr.groupType != 0 || type == null || gr.ContentsType == type)
                        {
                            return true;
                        }
                    }
                    else if (r is Plugin)
                    {
                        return true;
                    }

                    return false;
                }))
            {
                if (r is Record)
                {
                    var r2 = r as Record;
                    yield return new KeyValuePair<uint, Record>(r2.FormID, r2);
                }
            }
        }

        internal override List<string> GetIDs(bool lower)
        {
            var list = new List<string>();
            foreach (Rec r in this.Records)
            {
                list.AddRange(r.GetIDs(lower));
            }

            return list;
        }

        internal Record GetRecordByID(uint id)
        {
            uint pluginid = (id & 0xff000000) >> 24;
            if (pluginid > this.Masters.Length)
            {
                return null;
            }

            Record r;

            // first check self for exact match
            if (this.TryGetRecordByID(id, out r))
            {
                return r;
            }

            id &= 0xffffff;
            if (pluginid >= this.Masters.Length || this.Masters[pluginid] == null)
            {
                return null;
            }

            // find the reference master and search it for reference
            // TODO: in theory another master could override the first master
            id += this.Fixups[pluginid] << 24;
            if (this.Masters[pluginid].TryGetRecordByID(id, out r))
            {
                return r;
            }

            return null;
        }

        /// <summary>
        /// Lookup FormID by index.  Search via defined masters
        /// </summary>
        /// <param name="id">
        /// </param>
        /// <returns>
        /// The System.String.
        /// </returns>
        internal string LookupFormID(uint id)
        {
            uint pluginid = (id & 0xff000000) >> 24;
            if (pluginid > this.Masters.Length)
            {
                return "FormID was invalid";
            }

            Record r;

            // First search self for exact match
            if (this.TryGetRecordByID(id, out r))
            {
                return r.DescriptiveName;
            }

            id &= 0xffffff;
            if (pluginid < this.Masters.Length && this.Masters[pluginid] != null)
            {
                // find the reference master and search it for reference
                // TODO: in theory another master could override the first master
                var p = this.Masters[pluginid];
                id |= this.Fixups[pluginid] << 24;
                if (p.TryGetRecordByID(id, out r))
                {
                    return r.DescriptiveName;
                }

                return "No match";
            }
            else
            {
                return "Master not loaded";
            }
        }

        internal string LookupFormIDS(string sid)
        {
            uint id;
            if (!uint.TryParse(sid, NumberStyles.AllowHexSpecifier, null, out id))
            {
                return "FormID was invalid";
            }

            return this.LookupFormID(id);
        }

        internal string LookupFormStrings(uint id)
        {
            string value = default(string);
            foreach (var plugin in this.Masters.Reverse())
            {
                if (plugin == null)
                {
                    continue;
                }

                if (plugin.Strings.TryGetValue(id, out value))
                {
                    break;
                }

                if (plugin.DLStrings.TryGetValue(id, out value))
                {
                    break;
                }

                if (plugin.ILStrings.TryGetValue(id, out value))
                {
                    break;
                }
            }

            return value;
        }

        internal void Save(string filePath)
        {
            UpdateRecordCount();
            string extension = string.Empty;
            //BinaryWriter bw;
            SnipStreamWrapper snipStreamWrapper = null;

            string tmpFile = filePath + ".new";

            //if (File.Exists(filePath))
            //{
            //    //bw = new BinaryWriter(File.OpenWrite(filePath + ".new"));
            //    //extension = ".new";
            //}
            //else
            //{
            //    //bw = new BinaryWriter(File.OpenWrite(filePath));
            //    fs = new FileStream(filePath, FileMode.Open, FileAccess.Write);
            //}

            //fs = new FileStream(filePath + extension, FileMode.Open, FileAccess.Write);
            //bw = new BinaryWriter(File.OpenWrite(filePath + extension));

            //bw = new BinaryWriter(File.OpenWrite(tmpFile));
            FileStream fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None);

            try
            {
                ZLibWrapper.AllocateBuffers();
                snipStreamWrapper = new SnipStreamWrapper(fs);
                snipStreamWrapper.AllocateBuffers();

                this.SaveData(snipStreamWrapper);
                Name = Path.GetFileName(filePath);
                PluginPath = Path.GetDirectoryName(filePath);
            }
            finally
            {
                //bw.Close();                
                snipStreamWrapper.CloseAndDisposeFileStream();
                snipStreamWrapper.ReleaseBuffers();
                snipStreamWrapper = null;
                fs = null;
            }

            try
            {
                // ** Create Backup
                bool backupExists = true;
                int backupVersion = 0;
                string backupFolder = CreateBackupFolder(filePath);
                while (backupExists && backupVersion < 999)
                {
                    backupExists =
                        File.Exists(Path.Combine(backupFolder, Name) + string.Format(".{0,3:D3}.bak", backupVersion));
                    if (backupExists)
                    {
                        backupVersion++;
                    }
                }
                string backupFile = Path.Combine(backupFolder, Name) + string.Format(".{0,3:D3}.bak", backupVersion);
                File.Copy(tmpFile, backupFile, true);

                //if (existed)
                //{
                ////string newFile = filePath;
                //string backupFile = Path.Combine(backupFolder, Name) + string.Format(".{0,3:D3}.bak", backupVersion);
                //File.Copy(tmpFile, backupFile, true);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tmpFile, filePath);
                //}

            }
            catch (Exception ex)
            {
                string msg = string.Format(ex.Message);
                MessageBox.Show(
                    msg,
                    TranslateUI.TranslateUiGlobalization.ResManager.GetString("Application_Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Clipboard.SetText(msg);
            }

            var tes4 = this.Records.OfType<Record>().FirstOrDefault(x => x.Name == "TES4");
            if (tes4 != null && (tes4.Flags1 & 0x80) != 0)
            {
                if (Properties.Settings.Default.SaveStringsFiles)
                {
                    string prefix = Path.Combine(Path.Combine(Path.GetDirectoryName(filePath), "Strings"), Path.GetFileNameWithoutExtension(filePath));
                    prefix += "_" + Properties.Settings.Default.LocalizationName;
                    this.SaveStrings(prefix);
                }
            }

            StringsDirty = false;
        }

        private string CreateBackupFolder(string filePath)
        {
            string dir = Options.Value.ApplicationDirectory; //Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            dir = Path.Combine(dir, "Backup", fileName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }


        //internal override void SaveData(BinaryWriter writer)
        //{
        //    foreach (Rec r in this.Records)
        //    {
        //        r.SaveData(writer);
        //    }
        //}

        internal override void SaveData(SnipStreamWrapper snipStreamWrapper)
        {

            foreach (Rec r in this.Records)
            {
                r.SaveData(snipStreamWrapper);
            }
        }

        internal void SaveStrings(string FilePath)
        {
            System.Text.Encoding enc = Encoding.Instance;
            FontLangInfo fontInfo;
            if (Encoding.TryGetFontInfo(Properties.Settings.Default.LocalizationName, out fontInfo))
            {
                if (fontInfo.CodePage != 1252)
                {
                    enc = System.Text.Encoding.GetEncoding(fontInfo.CodePage);
                }
            }

            this.SavePluginStrings(enc, LocalizedStringFormat.Base, this.Strings, FilePath + ".STRINGS");
            this.SavePluginStrings(enc, LocalizedStringFormat.IL, this.ILStrings, FilePath + ".ILSTRINGS");
            this.SavePluginStrings(enc, LocalizedStringFormat.DL, this.DLStrings, FilePath + ".DLSTRINGS");
        }

        internal void UpdateRecordCount()
        {
            int reccount = -1 + this.Records.Cast<Rec>().Sum(r => r.CountRecords());
            var tes4 = this.Records.OfType<Record>().FirstOrDefault(x => x.Name == "TES4");
            if (tes4 != null)
            {
                if (tes4.SubRecords.Count > 0 && tes4.SubRecords[0].Name == "HEDR" && tes4.SubRecords[0].Size >= 8)
                {
                    byte[] data = tes4.SubRecords[0].GetData();
                    byte[] reccountbytes = TypeConverter.si2h(reccount);
                    for (int i = 0; i < 4; i++)
                    {
                        data[4 + i] = reccountbytes[i];
                    }

                    tes4.SubRecords[0].SetData(data);
                }
            }
        }

        private void LoadPluginData(FileStream fs, bool headerOnly, string[] recFilter) //LoadPluginData(BinaryReader br, bool headerOnly, string[] recFilter)
        {
            bool oldHoldUpdates = HoldUpdates;
            SnipStreamWrapper snipStreamWrapper = null;

            try
            {
                ZLibWrapper.AllocateBuffers();
                RecordsTace.InitListOfRecords();
                snipStreamWrapper = new SnipStreamWrapper(fs);
                //BinaryReader br = new BinaryReader(fs);

                string s;
                uint recsize;
                bool IsOblivion = false;

                this.Filtered = recFilter != null && recFilter.Length > 0;

                HoldUpdates = true;

                s = ReadRecName(snipStreamWrapper.ReadBytes(4)); //s = ReadRecName(br);
                if (s != "TES4")
                {
                    throw new Exception("File is not a valid TES4 plugin (Missing TES4 record)");
                }

                // Check for file version by checking the position of the HEDR field in the file. (ie. how big are the record header.)
                snipStreamWrapper.JumpTo(20, SeekOrigin.Begin); //br.BaseStream.Position = 20;
                s = ReadRecName(snipStreamWrapper.ReadBytes(4)); //s = ReadRecName(br);
                if (s == "HEDR")
                {
                    // Record Header is 20 bytes
                    IsOblivion = true;
                }
                else
                {
                    s = ReadRecName(snipStreamWrapper.ReadBytes(4)); //s = ReadRecName(br);
                    if (s != "HEDR")
                    {
                        throw new Exception("File is not a valid TES4 plugin (Missing HEDR subrecord in the TES4 record)");
                    }

                    // Record Header is 24 bytes. Or the file is illegal
                }

                snipStreamWrapper.JumpTo(4, SeekOrigin.Begin); //br.BaseStream.Position = 4;
                recsize = snipStreamWrapper.ReadUInt32(); //recsize = br.ReadUInt32();
                try
                {
                    this.AddRecord(new Record("TES4", recsize, snipStreamWrapper, IsOblivion));
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }

                if (!headerOnly)
                {
                    while (!snipStreamWrapper.Eof())  //while (br.PeekChar() != -1)
                    {
                        s = ReadRecName(snipStreamWrapper.ReadBytes(4)); //s = ReadRecName(br);
                        recsize = snipStreamWrapper.ReadUInt32(); //recsize = br.ReadUInt32();
                        if (s == "GRUP")
                        {
                            try
                            {
                                this.AddRecord(new GroupRecord(recsize, snipStreamWrapper, IsOblivion, recFilter, false)); //this.AddRecord(new GroupRecord(recsize, br, IsOblivion, recFilter, false));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(e.Message);
                            }
                        }
                        else
                        {
                            bool skip = recFilter != null && Array.IndexOf(recFilter, s) >= 0;
                            if (skip)
                            {
                                long size = recsize + (IsOblivion ? 8 : 12);
                                if ((snipStreamWrapper.ReadUInt32() & 0x00040000) > 0) //if ((br.ReadUInt32() & 0x00040000) > 0)
                                {
                                    size += 4; // Add 4 bytes for compressed record since the decompressed size is not included in the record size.
                                }

                                snipStreamWrapper.JumpTo((int)size, SeekOrigin.Current);  //br.BaseStream.Position += size; // just position past the data
                            }
                            else
                            {
                                try
                                {
                                    this.AddRecord(new Record(s, recsize, snipStreamWrapper, IsOblivion)); //this.AddRecord(new Record(s, recsize, br, IsOblivion));
                                }
                                catch (Exception e)
                                {
                                    MessageBox.Show(e.Message);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                snipStreamWrapper.CloseAndDisposeFileStream();
                snipStreamWrapper = null;
                Clipboard.SetText("CompressedRecords:" + Environment.NewLine +
                                  string.Join<string>(string.Empty, RecordsTace.CompressedRecords) +
                                  "AllRecords:" + Environment.NewLine +
                                  string.Join<string>(string.Empty, RecordsTace.AllRecords) +
                                  "Max Size:" +
                                  ZLibWrapper.MaxOutputBufferPosition.ToString(CultureInfo.InvariantCulture));
                ZLibWrapper.ReleaseBuffers();
                ZLib.ReleaseInflater();
                HoldUpdates = oldHoldUpdates;
                FireRecordListUpdate(this, this);
            }
        }

        private LocalizedStringDict LoadPluginStrings(System.Text.Encoding encoding, LocalizedStringFormat format, string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (var reader = new BinaryReader(File.OpenRead(path))) return this.LoadPluginStrings(encoding, format, reader);
                }
            }
            catch
            {
            }

            return new LocalizedStringDict();
        }

        private LocalizedStringDict LoadPluginStrings(System.Text.Encoding encoding, LocalizedStringFormat format, BinaryReader reader)
        {
            if (encoding == null)
            {
                encoding = Encoding.Instance;
            }

            var dict = new LocalizedStringDict();
            int length = reader.ReadInt32();
            int size = reader.ReadInt32(); // size of data section
            var list = new List<Pair<uint, uint>>();
            for (uint i = 0; i < length; ++i)
            {
                uint id = reader.ReadUInt32();
                uint off = reader.ReadUInt32();
                list.Add(new Pair<uint, uint>(id, off));
            }

            long offset = reader.BaseStream.Position;
            var data = new byte[size];
            using (var stream = new MemoryStream(data, 0, size, true, false))
            {
                var buffer = new byte[65536];
                int left = size;
                while (left > 0)
                {
                    int read = Math.Min(left, buffer.Length);
                    int nread = reader.BaseStream.Read(buffer, 0, read);
                    if (nread == 0)
                    {
                        break;
                    }

                    stream.Write(buffer, 0, nread);
                    left -= nread;
                }
            }

            foreach (var kvp in list)
            {
                var start = (int)kvp.Value;
                int len = 0;
                switch (format)
                {
                    case LocalizedStringFormat.Base:
                        while (data[start + len] != 0)
                        {
                            ++len;
                        }

                        break;

                    case LocalizedStringFormat.DL:
                    case LocalizedStringFormat.IL:
                        len = BitConverter.ToInt32(data, start) - 1;
                        start = start + sizeof(int);
                        if (start + len > data.Length)
                        {
                            len = data.Length - start;
                        }

                        if (len < 0)
                        {
                            len = 0;
                        }

                        break;
                }

                string str = encoding.GetString(data, start, len);
                dict.Add(kvp.Key, str);
            }

            return dict;
        }

        private void RebuildCache()
        {
            if (this.FormIDLookup.Count == 0)
            {
                this.ForEach(
                    br =>
                    {
                        var r = br as Record;
                        if (r != null)
                        {
                            this.FormIDLookup[r.FormID] = r;
                        }
                    });
            }
        }

        private void SavePluginStrings(System.Text.Encoding enc, LocalizedStringFormat format, LocalizedStringDict strings, string path)
        {
            try
            {
                using (var writer = new BinaryWriter(File.Create(path))) this.SavePluginStrings(enc, format, strings, writer);
            }
            catch
            {
            }
        }

        private void SavePluginStrings(System.Text.Encoding enc, LocalizedStringFormat format, LocalizedStringDict strings, BinaryWriter writer)
        {
            if (enc == null)
            {
                enc = Encoding.Instance;
            }

            var list = new List<Pair<uint, uint>>();

            using (var stream = new MemoryStream())
            using (var memWriter = new BinaryWriter(stream))
            {
                foreach (var kvp in strings)
                {
                    list.Add(new Pair<uint, uint>(kvp.Key, (uint)stream.Position));
                    byte[] data = enc.GetBytes(kvp.Value);
                    switch (format)
                    {
                        case LocalizedStringFormat.Base:
                            memWriter.Write(data, 0, data.Length);
                            memWriter.Write((byte)0);
                            break;

                        case LocalizedStringFormat.DL:
                        case LocalizedStringFormat.IL:
                            memWriter.Write(data.Length + 1);
                            memWriter.Write(data, 0, data.Length);
                            memWriter.Write((byte)0);
                            break;
                    }
                }

                writer.Write(strings.Count);
                writer.Write((int)stream.Length);
                foreach (var item in list)
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }

                stream.Position = 0;
                var buffer = new byte[65536];
                var left = (int)stream.Length;
                while (left > 0)
                {
                    int read = Math.Min(left, buffer.Length);
                    int nread = stream.Read(buffer, 0, read);
                    if (nread == 0)
                    {
                        break;
                    }

                    writer.Write(buffer, 0, nread);
                    left -= nread;
                }
            }
        }
    }
}
