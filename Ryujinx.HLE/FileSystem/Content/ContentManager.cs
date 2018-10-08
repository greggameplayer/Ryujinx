﻿using System;
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
        private Dictionary<StorageId, LinkedList<LocationEntry>> LocationEntries;

        private Dictionary<string, long> SharedFontTitleDictionary;

        private SortedDictionary<(ulong, ContentType), string> ContentDictionary;

        private Switch Device;

        public ContentManager(Switch Device)
        {
            ContentDictionary = new SortedDictionary<(ulong, ContentType), string>();

            LocationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();

            SharedFontTitleDictionary = new Dictionary<string, long>()
            {
                {"FontStandard",                  0x0100000000000811 },
                {"FontChineseSimplified",         0x0100000000000814 },
                {"FontExtendedChineseSimplified", 0x0100000000000814 },
                {"FontKorean",                    0x0100000000000812 },
                {"FontChineseTraditional",        0x0100000000000813 },
                {"FontNintendoExtended" ,         0x0100000000000810 },
            };

            this.Device = Device;
        }

        public void LoadEntries()
        {
            ContentDictionary = new SortedDictionary<(ulong, ContentType), string>();

            foreach (StorageId StorageId in Enum.GetValues(typeof(StorageId)))
            {
                string ContentInstallationDirectory = null;
                string ContentPathString            = null;

                try
                {
                    ContentPathString            = LocationHelper.GetContentPath(StorageId);
                    ContentInstallationDirectory = LocationHelper.GetRealPath(Device.FileSystem, ContentPathString);
                }
                catch (NotSupportedException NEx)
                {
                    continue;
                }

                Directory.CreateDirectory(ContentInstallationDirectory);

                LinkedList<LocationEntry> LocationList = new LinkedList<LocationEntry>();

                void AddEntry(LocationEntry Entry)
                {
                    LocationList.AddLast(Entry);
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

                if(LocationEntries.ContainsKey(StorageId) && LocationEntries[StorageId]?.Count == 0)
                {
                    LocationEntries.Remove(StorageId);
                }

                if (!LocationEntries.ContainsKey(StorageId))
                {
                    LocationEntries.Add(StorageId, LocationList);
                }
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

            return Path.Combine(ContentPath, ContentDictionary[((ulong)TitleId, ContentType)],"00");
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

            if (VerifyContentType(LocationEntry, ContentType))
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
                if (File.Exists(InstalledPath))
                {
                    FileStream File = new FileStream(InstalledPath, FileMode.Open, FileAccess.Read);

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

        public bool TryGetFontTitle(string FontName, out long TitleId)
        {
            return SharedFontTitleDictionary.TryGetValue(FontName, out TitleId);
        }

        private LocationEntry GetLocation(long TitleId, ContentType ContentType,StorageId StorageId)
        {
            LinkedList<LocationEntry> LocationList = LocationEntries[StorageId];

            return LocationList.ToList().Find(x => x.TitleId == TitleId && x.ContentType == ContentType);
        }
    }
}
