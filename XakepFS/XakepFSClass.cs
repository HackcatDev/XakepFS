using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace XakepFS
{
    class XakepFSClass : IDokanOperations
    {
        private XakepFSTree FSTree = new XakepFSTree(Environment.CurrentDirectory, "filesystem.json");
        private const DokanNet.FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                              FileAccess.Execute |
                                              FileAccess.GenericExecute | FileAccess.GenericWrite |
                                              FileAccess.GenericRead;

        private String GenerateName(String tgt)
        {
            string res = "";
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            var b = md5.ComputeHash(Encoding.UTF8.GetBytes(tgt + DateTime.Now.ToBinary().ToString()));
            foreach (var cb in b) { res += cb.ToString("X2"); }
            return "\\" + res;
        }
        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;
        public void Cleanup(string fileName, DokanFileInfo info)
        {
        }

        public void CloseFile(string fileName, DokanFileInfo info)
        {
        }

        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            var filePath = "";
            if (fileName == "\\")
            {
                return NtStatus.Success;
            }
            if (mode == FileMode.OpenOrCreate)
            {
                if (info.IsDirectory) return NtStatus.Error;
                if (FSTree.GetFSObject(fileName) == null)
                {
                    var gn = GenerateName(fileName);
                    FSTree.CreateFile(fileName, gn);
                    File.Create(Environment.CurrentDirectory + "\\storage" + gn);
                    return NtStatus.Success;
                }
            }
            if (FSTree.reverse_search.ContainsKey(fileName))
            {
                filePath = Environment.CurrentDirectory  + "\\storage" + FSTree._fstree[FSTree.reverse_search[fileName]].DataLocation;
            }
            else
            {
                if (mode == FileMode.Create || mode == FileMode.CreateNew)
                {
                    var gn = GenerateName(fileName);
                    filePath = Environment.CurrentDirectory + "\\storage" + gn;
                    if (info.IsDirectory)
                    {
                        if (Directory.Exists(filePath)) return NtStatus.ObjectNameCollision;
                        try
                        {
                            File.GetAttributes(filePath).HasFlag(FileAttributes.Directory);
                            return NtStatus.ObjectNameCollision;
                        }
                        catch (IOException)
                        {
                        }
                        Directory.CreateDirectory(filePath);
                        FSTree.CreateDirectory(fileName, gn);
                        return NtStatus.Success;
                    }
                    if (File.Exists(filePath)) return NtStatus.ObjectNameCollision;
                    File.Create(filePath);
                    FSTree.CreateFile(fileName, gn);
                }
                else
                    return NtStatus.ObjectNameNotFound;
            }
            if (info.IsDirectory)
            {
                try
                {
                    switch (mode)
                    {
                        case FileMode.Open:
                            if (!Directory.Exists(filePath) && (FSTree.GetFSObject(fileName) == null || FSTree.GetFSObject(fileName).IsDirectory == false))
                            {
                                try
                                {
                                    if (!FSTree.GetFSObject(fileName).Attributes.HasFlag(FileAttributes.Directory)) return NtStatus.NotADirectory;
                                }
                                catch (Exception)
                                {
                                    return NtStatus.NoSuchFile;
                                }
                                return NtStatus.ObjectPathNotFound;
                            }
                            break;

                        case FileMode.CreateNew:
                            //if (Directory.Exists(filePath)) return NtStatus.ObjectNameCollision;
                            //try
                            //{
                            //    File.GetAttributes(filePath).HasFlag(FileAttributes.Directory);
                            //    return NtStatus.ObjectNameCollision;
                            //}
                            //catch (IOException)
                            //{
                            //}
                            //Directory.CreateDirectory(filePath);
                            //FSTree.CreateDirectory(fileName);
                            break;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return NtStatus.AccessDenied;
                }
            }
            else
            {
                var pathExists = true;
                var pathIsDirectory = false;

                var readWriteAttributes = (access & DataAccess) == 0;
                var readAccess = (access & DataWriteAccess) == 0;

                try
                {
                    pathExists = (Directory.Exists(filePath) || File.Exists(filePath));
                    pathIsDirectory = pathExists ? File.GetAttributes(filePath).HasFlag(FileAttributes.Directory) : false;
                }
                catch (IOException)
                {
                }

                switch (mode)
                {
                    case FileMode.Open:

                        if (pathExists)
                        {
                            // check if driver only wants to read attributes, security info, or open directory
                            if (readWriteAttributes || pathIsDirectory)
                            {
                                if (pathIsDirectory && (access & FileAccess.Delete) == FileAccess.Delete
                                    && (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                                    //It is a DeleteFile request on a directory
                                    return NtStatus.AccessDenied;

                                info.IsDirectory = pathIsDirectory;
                                info.Context = new object();
                                // must set it to someting if you return DokanError.Success

                                return NtStatus.Success;
                            }
                        }
                        else
                        {
                            return NtStatus.ObjectNameNotFound;
                        }
                        break;

                    case FileMode.CreateNew:
                        //if (pathExists)
                            //return NtStatus.ObjectNameCollision;
                        //FSTree.CreateFile(fileName);
                        break;

                    case FileMode.Truncate:
                        if (!pathExists)
                            return NtStatus.ObjectNameNotFound;
                        break;
                }

                try
                {
                    info.Context = null;//new FileStream(filePath, FileMode.OpenOrCreate,
                        //readAccess ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite, share, 4096, options);

                    if (pathExists && (mode == FileMode.OpenOrCreate
                                       || mode == FileMode.Create))
                        return NtStatus.ObjectNameCollision;

                    if (mode == FileMode.CreateNew || mode == FileMode.Create) //Files are always created as Archive
                        attributes |= FileAttributes.Archive;
                    File.SetAttributes(filePath, attributes);
                }
                catch (UnauthorizedAccessException) // don't have access rights
                {
                    if (info.Context is FileStream fileStream)
                    {
                        // returning AccessDenied cleanup and close won't be called,
                        // so we have to take care of the stream now
                        fileStream.Dispose();
                        info.Context = null;
                    }
                    return NtStatus.AccessDenied;
                }
                catch (DirectoryNotFoundException)
                {
                    return NtStatus.ObjectPathNotFound;
                }
            }
            return NtStatus.Success;
        }

        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            FSTree.DeleteDirectory(fileName);
            return NtStatus.Success;
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            FSTree.DeleteFile(fileName);
            return NtStatus.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            if (fileName == "\\")
            {
                files = new List<FileInformation>();
                var _rf = FSTree._fstree.Values.ToList().FindAll(n => n.Parent == 0 && n.ObjectID != 0);
                foreach (var ci in _rf)
                {
                    var co = new FileInformation()
                    {
                        FileName = ci.Name,
                        Attributes = ci.Attributes,
                        CreationTime = ci.CreatedTime,
                        LastAccessTime = ci.LastAccessTime,
                        LastWriteTime = ci.LastWriteTime,
                        Length = ci.Length
                    };
                    files.Add(co);
                }
                return NtStatus.Success;
            }
            var o = FSTree._fstree[FSTree.reverse_search[fileName]];
            files = new List<FileInformation>();
            if (o == null || o.IsDirectory || o.IsDeleted)
            {
                return NtStatus.Error;
            }
            var l = FSTree._fstree.Values.ToList().FindAll(n => n.Parent == o.ObjectID);
            foreach (var ci in l)
            {
                var co = new FileInformation()
                {
                    FileName = ci.Name,
                    Attributes = ci.Attributes,
                    CreationTime = ci.CreatedTime,
                    LastAccessTime = ci.LastAccessTime,
                    LastWriteTime = ci.LastWriteTime,
                    Length = ci.Length
                };
                files.Add(co);
            }
            return NtStatus.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {
            if (searchPattern == "*") return FindFiles(fileName, out files, info);
            var fso0 = FSTree.GetFSObject(searchPattern);
            var fso1 = FSTree.GetFSObject("\\" + searchPattern);
            var fso2 = FSTree.GetFSObject("/" + searchPattern);
            var l = new List<XakepFSObject>() { fso0, fso1, fso2 };
            foreach (var c in l.FindAll(e => e != null))
            {
                files = new List<FileInformation>()
                {
                    new FileInformation
                    {
                        CreationTime = c.CreatedTime,
                        FileName = c.Name,
                        Attributes = c.Attributes,
                        LastAccessTime = c.LastAccessTime,
                        LastWriteTime = c.LastWriteTime,
                        Length = c.Length
                    }
                };
                return NtStatus.Success;
            }
            return FindFiles(fileName, out files, info);
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            streams = new List<FileInformation>();
            return NtStatus.Error;
        }

        public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
        {
            try
            {
                if (info.Context != null && (info.Context as FileStream) != null)
                {
                    ((FileStream)(info.Context)).Flush();
                }
                return NtStatus.Success;
            }
            catch (IOException)
            {
                return NtStatus.DiskFull;
            }
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
        {
            freeBytesAvailable = new DriveInfo("C:\\").AvailableFreeSpace;
            totalNumberOfFreeBytes = new DriveInfo("C:\\").TotalFreeSpace;
            totalNumberOfBytes = new DriveInfo("C:\\").TotalSize;
            return NtStatus.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {
            if (fileName == "\\" || fileName == "/")
            {
                fileInfo = new FileInformation
                {
                    FileName = "\\",
                    Length = 0,
                    Attributes = FileAttributes.Directory,
                    LastAccessTime = DateTime.Now
                };
                return NtStatus.Success;
            }
            //Тут нужно вернуть информацию по заданному файлу или каталогу. Не пытайся что-нибудь поменять, если тебе дорога жизнь
            var o = FSTree.GetFSObject(fileName);
            fileInfo = new FileInformation();
            if (o == null) return NtStatus.ObjectNameNotFound;
            if (o.Parent == 0)
            {

            }
            else if (!FSTree._fstree[o.Parent].IsDirectory) return NtStatus.Error;
            fileInfo.Attributes = o.Attributes;
            fileInfo.CreationTime = o.CreatedTime;
            fileInfo.FileName = o.Name;
            fileInfo.LastAccessTime = o.LastAccessTime;
            fileInfo.LastWriteTime = o.LastWriteTime;
            try
            {
                o.Length = new FileInfo(Environment.CurrentDirectory + "\\storage" + o.DataLocation.Replace("\\\\\\", "\\")).Length;
            }
            catch { o.Length = 0; }
            fileInfo.Length = o.Length;
            if (new Random().Next(10) == 5) GC.Collect();
            return NtStatus.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            security = null;
            return NtStatus.NotImplemented;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, DokanFileInfo info)
        {
            volumeLabel = "XakepFS JSON storage";
            features = FileSystemFeatures.None;
            fileSystemName = "XakepFS JSON (xakep.ru)";
            maximumComponentLength = 512;
            return NtStatus.Success;
        }

        public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus Mounted(DokanFileInfo info)
        {
            //Тут обработчик события, которое возникает при успешном монтировании. У меня тут ничего
            return NtStatus.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            var n_n = newName.Split("\\".ToCharArray());
            String nn = n_n[n_n.Length - 1];
            var o = FSTree._fstree[FSTree.reverse_search[oldName]];
            o.Name = nn;
            FSTree.reverse_search.Remove(oldName);
            FSTree.reverse_search.Add(newName, o.ObjectID);
            FSTree._fspaths.Remove(oldName);
            FSTree._fspaths.Add(newName);
            o.LastAccessTime = DateTime.Now;
            o.LastWriteTime = DateTime.Now;
            return NtStatus.Success;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            //Этот метод - просто копипаст из другого проекта. Могу сказать только то, что он работает.
            bytesRead = 0;
            //if (info.Context == null)
            //{
                var fn = "";
                if (FSTree.reverse_search.ContainsKey(fileName))
                {
                    fn = FSTree._fstree[FSTree.reverse_search[fileName]].DataLocation;
                }
                else
                {
                    bytesRead = 0;
                    return NtStatus.ObjectNameNotFound;
                }
                bool success = false;
                while (!success)
                {
                    try
                    {
                        using (var stream = new FileStream(Environment.CurrentDirectory + "\\storage" + fn, FileMode.Open, System.IO.FileAccess.Read))
                        {
                            stream.Position = offset;
                            bytesRead = stream.Read(buffer, 0, buffer.Length);
                        }
                        success = true;
                    } catch { success = false; }
                }
            //}
            //else
            //{
            //    var stream = info.Context as FileStream;
            //    lock (stream)
            //    {
            //        stream.Position = offset;
            //        bytesRead = stream.Read(buffer, 0, buffer.Length);
            //    }
            //}
            return NtStatus.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            return NtStatus.NotImplemented;
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            FSTree._fstree[FSTree.reverse_search[fileName]].Attributes = attributes;
            return NtStatus.Success;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            //Тут обработчики события отмонтирования. Можно, например, сбросить готовый JSON на диск. Но я этого делать не буду)
            return NtStatus.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            bytesWritten = 0;
            //if (info.Context == null)
            //{
                bool success = false;
                while (!success)
                {
                    try
                    {
                        using (var stream = new FileStream(Environment.CurrentDirectory + "\\storage" + FSTree._fstree[FSTree.reverse_search[fileName]].DataLocation, FileMode.Open, System.IO.FileAccess.Write))
                        {
                            stream.Position = offset;
                            stream.Write(buffer, 0, buffer.Length);
                            bytesWritten = buffer.Length;
                        }
                        success = true;
                    }
                    catch { success = false; }
                }
            //}
            //else
            //{
            //    var stream = info.Context as FileStream;
            //    lock (stream) 
            //    {
            //        stream.Position = offset;
            //        stream.Write(buffer, 0, buffer.Length);
            //    }
            //    bytesWritten = buffer.Length;
            //}
            return NtStatus.Success;
        }
    }
}
