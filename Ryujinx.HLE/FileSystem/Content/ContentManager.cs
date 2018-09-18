using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using LibHac;
using System.Linq;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.Loaders.Npdm;

namespace Ryujinx.HLE.FileSystem.Content
{
    internal class ContentManager
    {
        Dictionary<StorageId,LinkedList<LocationEntry>> LocationEntries { get; set; }

        public SortedDictionary<(ulong,ContentType),string> ContentDictionary { get; private set; }

        public ContentStorageId DefaultInstallationStorage { get; private set; }

        private Switch Device;

        public ContentManager(Switch Device)
        {
            ContentDictionary = new SortedDictionary<(ulong, ContentType), string>();

            LocationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();

            this.Device = Device;
        }

        public void LoadEntries()
        {
            ContentDictionary = new SortedDictionary<(ulong, ContentType), string>();

            foreach (StorageId StorageId in Enum.GetValues(typeof(StorageId)))
            {
                string ContentInstallationDirectory = null;

                string ContentPathString = null;

                try
                {
                    ContentPathString = LocationHelper.GetContentPath(StorageId);

                    ContentInstallationDirectory = LocationHelper.GetRealPath(Device.FileSystem, ContentPathString);
                }
                catch (NotSupportedException NEx)
                {
                    continue;
                }

                Directory.CreateDirectory(ContentInstallationDirectory);

                LinkedList<LocationEntry> LocationList = new LinkedList<LocationEntry>();

                List<long> ReadTitleIds = new List<long>();

                void AddEntry(LocationEntry Entry)
                {
                    LocationList.AddLast(Entry);

                    if (!ReadTitleIds.Contains(Entry.TitleId))
                    {
                        ReadTitleIds.Add(Entry.TitleId);
                    }
                }

                foreach (string DirectoryPath in Directory.EnumerateDirectories(ContentInstallationDirectory))
                {
                    if (Directory.GetFiles(DirectoryPath).Length > 0)
                    {
                        string NcaName = new DirectoryInfo(DirectoryPath).Name;

                        using (FileStream NcaFile = new FileStream(Directory.GetFiles(DirectoryPath)[0], FileMode.Open, FileAccess.Read))
                        {

                            Nca Nca = new Nca(Device.System.KeySet, NcaFile, false);

                            LocationEntry Entry = new LocationEntry(ContentPathString,
                                0,
                                (long)Nca.Header.TitleId,
                                Nca.Header.ContentType);
                            

                            AddEntry(Entry);

                            ContentDictionary.Add((Nca.Header.TitleId, Nca.Header.ContentType), NcaName);

                            NcaFile.Close();

                            Nca.Dispose();

                            NcaFile.Dispose();
                        }
                    }
                }

                foreach (string FilePath in Directory.EnumerateFiles(ContentInstallationDirectory))
                {
                    if (Path.GetExtension(FilePath) == ".nca")
                    {
                        string NcaName = Path.GetFileName(FilePath);

                        using (FileStream NcaFile = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                        {
                            Nca Nca = new Nca(Device.System.KeySet, NcaFile, false);

                            LocationEntry Entry = new LocationEntry(ContentPathString,
                                0,
                                (long)Nca.Header.TitleId,
                                Nca.Header.ContentType);

                            AddEntry(Entry);

                            ContentDictionary.Add((Nca.Header.TitleId, Nca.Header.ContentType), NcaName);

                            NcaFile.Close();

                            Nca.Dispose();

                            NcaFile.Dispose();
                        }
                    }
                }

                LocationEntries.Add(StorageId, LocationList);
            }
        }

        public void ClearEntry(long TitleId, ContentType ContentType,StorageId StorageId)
        {
            RemoveLocationEntry(TitleId, ContentType, StorageId);
        }

        public void RefreshEntries(StorageId StorageId, int Flag)
        {
            LinkedList<LocationEntry> LocationList = LocationEntries[StorageId];

            LinkedListNode<LocationEntry> LocationEntry = LocationList.First;

            while (LocationEntry != null)
            {
                LinkedListNode<LocationEntry> NextLocationEntry = LocationEntry.Next;

                if (LocationEntry.Value.Flag == Flag)
                {
                    LocationList.Remove(LocationEntry.Value);
                }

                LocationEntry = NextLocationEntry;
            }
        }

        /*public string GetProgramPath(long TitleId)
        {
            LocationEntry LocationEntry = GetLocation(TitleId);
        }*/

        public void InstallContent(string NcaPath, StorageId StorageId)
        {
            if (File.Exists(NcaPath))
            {
                FileStream NcaStream = new FileStream(NcaPath, FileMode.Open, FileAccess.Read);

                Nca Nca = new Nca(Device.System.KeySet, NcaStream, false);

                string Filename = Path.GetFileName(NcaPath);

                InstallContent(Nca, Filename, StorageId);

                NcaStream.Close();

                NcaStream.Dispose();

                Nca.Dispose();
            }
        }

        public void InstallContent(Nca Nca, string Filename, StorageId StorageId)
        {
            if (Nca.Header.Distribution == DistributionType.Download)
            {
                string ContentStoragePath = LocationHelper.GetContentPath(StorageId);

                string RealContentPath = LocationHelper.GetRealPath(Device.FileSystem, ContentStoragePath);

                string NcaName = Filename.Substring(0, Filename.IndexOf("."));

                if (!NcaName.EndsWith(".nca"))
                {
                    NcaName += ".nca";
                }

                string InstallationPath = Path.Combine(RealContentPath, NcaName);

                string FilePath = Path.Combine(InstallationPath, "00");

                if (File.Exists(FilePath))
                {
                    FileInfo FileInfo = new FileInfo(FilePath);

                    if (FileInfo.Length == (long)Nca.Header.NcaSize)
                    {
                        return;
                    }
                }

                if (ContentDictionary.ContainsKey((Nca.Header.TitleId, Nca.Header.ContentType)))
                {
                    string InstalledPath = GetInstalledPath((long)Nca.Header.TitleId, Nca.Header.ContentType, StorageId);

                    if (File.Exists(InstalledPath))
                    {
                        File.Delete(InstalledPath);
                    }
                    if (Directory.Exists(InstalledPath))
                    {
                        Directory.Delete(InstalledPath, true);
                    }
                }

                if (!Directory.Exists(InstallationPath))
                {
                    Directory.CreateDirectory(InstallationPath);
                }

                using (FileStream FileStream = File.Create(FilePath))
                {
                    Stream NcaStream = Nca.GetStream();

                    NcaStream.CopyStream(FileStream, NcaStream.Length);

                    Nca.Dispose();

                    NcaStream.Close();

                    FileStream.Close();
                }
            }
        }

        public NcaId GetInstalledNcaId(long TitleId, ContentType ContentType)
        {
            if (ContentDictionary.ContainsKey(((ulong)TitleId,ContentType)))
            {
                return new NcaId(ContentDictionary[((ulong)TitleId,ContentType)]);
            }

            return null;
        }

        public string GetInstalledPath(long TitleId, ContentType ContentType, StorageId StorageId)
        {
            LocationEntry LocationEntry = GetLocation(TitleId, ContentType, StorageId);

            string ContentPath = LocationHelper.GetRealPath(Device.FileSystem, LocationEntry.ContentPath);

            return Path.Combine(ContentPath, ContentDictionary[((ulong)TitleId, ContentType)]);
        }

        public StorageId GetInstalledStorage(long TitleId, ContentType ContentType, StorageId StorageId)
        {
            LocationEntry LocationEntry = GetLocation(TitleId, ContentType, StorageId);

            return LocationEntry.ContentPath != null ?
                LocationHelper.GetStorageId(LocationEntry.ContentPath) : StorageId.None;
        }

        public string GetInstalledContentStorage(long TitleId, StorageId StorageId, ContentType ContentType)
        {
            LocationEntry LocationEntry = GetLocation(TitleId, ContentType, StorageId);

            if(VerifyContentType(LocationEntry,ContentType))
            {
                return LocationEntry.ContentPath;
            }

            return string.Empty;
        }

        public void RedirectLocation(LocationEntry NewEntry, StorageId StorageId)
        {
            LocationEntry LocationEntry = GetLocation(NewEntry.TitleId, NewEntry.ContentType, StorageId);

            if (LocationEntry.ContentPath != null)
            {
                RemoveLocationEntry(NewEntry.TitleId, NewEntry.ContentType, StorageId);
            }

            AddLocationEntry(NewEntry, StorageId);
        }

        private bool VerifyContentType(LocationEntry LocationEntry, ContentType ContentType)
        {
            StorageId StorageId = LocationHelper.GetStorageId(LocationEntry.ContentPath);

            string InstalledPath = GetInstalledPath(LocationEntry.TitleId, ContentType, StorageId);

            if (!string.IsNullOrWhiteSpace(InstalledPath))
            {
                string NcaPath = Path.Combine(InstalledPath, "00");

                if (File.Exists(NcaPath))
                {
                    FileStream File = new FileStream(NcaPath, FileMode.Open, FileAccess.Read);

                    Nca Nca = new Nca(Device.System.KeySet, File, false);

                    return Nca.Header.ContentType == ContentType;
                }
            }

            return false;
        }

        private void AddLocationEntry(LocationEntry Entry, StorageId StorageId)
        {
            LinkedList<LocationEntry> LocationList = null;

            if (LocationEntries.ContainsKey(StorageId))
            {
                LocationList = LocationEntries[StorageId];
            }

            if (LocationList != null)
            {
                if (LocationList.Contains(Entry))
                {
                    LocationList.Remove(Entry);
                }

                LocationList.AddLast(Entry);
            }
        }

        private void RemoveLocationEntry(long TitleId, ContentType ContentType, StorageId StorageId)
        {
            LinkedList<LocationEntry> LocationList = null;

            if (LocationEntries.ContainsKey(StorageId))
            {
                LocationList = LocationEntries[StorageId];
            }

            if (LocationList != null)
            {
                LocationEntry Entry =
                    LocationList.ToList().Find(x => x.TitleId == TitleId && x.ContentType == ContentType);


                if (Entry.ContentPath != null)
                {
                    LocationList.Remove(Entry);
                }
            }
        }

        private LocationEntry GetLocation(long TitleId, ContentType ContentType,StorageId StorageId)
        {
            LinkedList<LocationEntry> LocationList = LocationEntries[StorageId];

            return LocationList.ToList().Find(x => x.TitleId == TitleId && x.ContentType == ContentType);
        }
    }
}
