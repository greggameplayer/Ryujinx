using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using LibHac;
using System.Linq;

using static Ryujinx.HLE.HOS.ErrorCode;

namespace Ryujinx.HLE.HOS.Services.FspSrv
{
    class RomFileSystemProvider : IFileSystemProvider
    {
        private Romfs RomFs;

        public RomFileSystemProvider(Stream StorageStream)
        {
            RomFs = new Romfs(StorageStream);
        }


        public int CreateDirectory(string Name)
        {
            throw new NotSupportedException();
        }

        public int CreateFile(string Name, long Size)
        {
            throw new NotSupportedException();
        }

        public int DeleteDirectory(string Name, bool Recursive)
        {
            throw new NotSupportedException();
        }

        public int DeleteFile(string Name)
        {
            throw new NotSupportedException();
        }

        public string[] GetDirectories(string Path)
        {
            List<string> Directories = new List<string>();

            foreach(RomfsDir Directory in RomFs.Directories)
            {
                Directories.Add(Directory.Name);
            }

            return Directories.ToArray();
        }

        public string[] GetEntries(string Path)
        {
            List<string> Entries = new List<string>();

            foreach (RomfsDir Directory in RomFs.Directories)
            {
                Entries.Add(Directory.Name);
            }

            foreach (RomfsFile File in RomFs.Files)
            {
                Entries.Add(File.Name);
            }

            return Entries.ToArray();
        }

        public string[] GetFiles(string Path)
        {
            List<string> Files = new List<string>();

            foreach (RomfsFile File in RomFs.Files)
            {
                Files.Add(File.Name);
            }

            return Files.ToArray();
        }

        public long GetFreeSpace(ServiceCtx Context)
        {
            return 0;
        }

        public string GetFullPath(string Name)
        {
            return Name;
        }

        public long GetTotalSpace(ServiceCtx Context)
        {
            return RomFs.Files.Sum(x => x.DataLength);
        }

        public bool IsDirectoryExists(string Name)
        {
            return RomFs.Directories.Exists(x=>x.Name == Name);
        }

        public bool IsFileExists(string Name)
        {
            return RomFs.FileExists(Name);
        }

        public int OpenDirectory(string Name, int FilterFlags, out IDirectory DirectoryInterface)
        {
            DirectoryInterface = null;

            RomfsDir Directory = RomFs.Directories.Find(x => x.Name == Name);

            if (Directory != null)
            {
                DirectoryInterface = new IDirectory(Name, FilterFlags, this);
            }

            return (int)MakeError(ErrorModule.Fs, FsErr.PathDoesNotExist);
        }

        public int OpenFile(string Name, out IFile FileInterface)
        {
            FileInterface = null;

            if (File.Exists(Name))
            {
                Stream Stream = RomFs.OpenFile(Name);

                FileInterface = new IFile(Stream, Name);

                return 0;
            }

            return (int)MakeError(ErrorModule.Fs, FsErr.PathDoesNotExist);
        }

        public int RenameDirectory(string OldName, string NewName)
        {
            throw new NotSupportedException();
        }

        public int RenameFile(string OldName, string NewName)
        {
            throw new NotSupportedException();
        }
    }
}
