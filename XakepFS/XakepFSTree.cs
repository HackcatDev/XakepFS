using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Timers;
using DokanNet;

namespace XakepFS
{
    class XakepFSTree
    {
        public String RootDataDirectory = Environment.CurrentDirectory;
        public String JsonPath = "";
        public List<String> _fspaths = new List<String>();
        public Dictionary<int, XakepFSObject> _fstree = new Dictionary<int, XakepFSObject>();
        public Dictionary<String, int> reverse_search = new Dictionary<string, int>();
        private XakepFSObject _FSRoot = new XakepFSObject();
        private Timer _filesystem_sync_timer = new Timer(10000); //Один раз в минуту
        public XakepFSTree(String root, String json_path)
        {
            RootDataDirectory = root;
            JsonPath = json_path;
            ParseTreeFromJson();
            _filesystem_sync_timer.AutoReset = true;
            _filesystem_sync_timer.Elapsed += delegate
            {
                List<String> _packed_fs_obj = new List<string>();
                foreach (var ck in _fstree.Keys)
                {
                    _packed_fs_obj.Add(_fstree[ck].PackJson());
                }
                String _json = JsonConvert.SerializeObject(_packed_fs_obj);
                File.WriteAllText(JsonPath, _json);
            };
            _filesystem_sync_timer.Start();
        }

        private void InitializeFS()
        {
            XakepFSObject fsobj = new XakepFSObject();
            fsobj.AccessControl = new DirectorySecurity();
            fsobj.Attributes = FileAttributes.Directory;
            fsobj.CreatedTime = DateTime.Now;
            fsobj.DataLocation = Environment.CurrentDirectory + "\\storage\\";
            fsobj.IsDeleted = false;
            fsobj.IsDirectory = true;
            fsobj.LastAccessTime = DateTime.Now;
            fsobj.LastWriteTime = DateTime.Now;
            fsobj.Length = 0;
            fsobj.Name = "\\";
            fsobj.ObjectID = 0;
            fsobj.Parent = 0;
            _fspaths.Add("\\");
            _fstree.Add(0, fsobj);
            reverse_search.Add("\\", 0);
            _FSRoot = fsobj;
        }

        private void ParseTreeFromJson()
        {
            String _jc = File.ReadAllText(JsonPath);
            if (String.IsNullOrWhiteSpace(_jc.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "")))
            {
                _fstree.Clear();
                _fspaths.Clear();
                reverse_search.Clear();
                InitializeFS();
                List<String> _packed_fs_obj = new List<string>();
                foreach (var ck in _fstree.Keys)
                {
                    _packed_fs_obj.Add(_fstree[ck].PackJson());
                }
                String _json = JsonConvert.SerializeObject(_packed_fs_obj);
                File.WriteAllText(JsonPath, _json);
                return;
            }
            List<String> _fsobjects = JsonConvert.DeserializeObject<List<String>>(_jc);
            //Каждая строка в списке - сериализованный элемент XakepFSObject

            List<XakepFSObject> _not_processed_objects = new List<XakepFSObject>();
            foreach (var ce in _fsobjects)
            {
                XakepFSObject fsObject = new XakepFSObject();
                fsObject.UnpackJson(ce);
                if (fsObject.Name == "/" && fsObject.Parent == 0)
                {
                    //Filesystem root
                    _FSRoot = fsObject;
                    _fstree.Add(0, fsObject); //У корневой папки ObjectID = 0 и Parent = 0
                    _fspaths.Add("\\");
                    continue;
                }
                //Regular object
                //if (_fstree.ContainsKey(fsObject.Parent))
                //{
                    //Всё в порядке, родитель уже найден
                    _fstree.Add(fsObject.ObjectID, fsObject);
                    String _ap = GetPathById(fsObject.ObjectID);
                    _fspaths.Add(_ap);
                    reverse_search.Add(_ap, fsObject.ObjectID);
                    continue;
                //}
                //else
                //{
                //    //Родитель объекта ещё не найден, объект потерян и будет обработан позже
                //    _not_processed_objects.Add(fsObject);
                //    continue;
                //}
            }
            //Process missing objects
            //bool _object_changed = true;
            //while (_object_changed)
            //{
            //    _object_changed = false;
            //    foreach (var ci in _not_processed_objects)
            //    {
            //        if (_fstree.ContainsKey(ci.Parent))
            //        {
            //            //Всё в порядке, родитель уже найден
            //            _fstree.Add(ci.ObjectID, ci);
            //            String _ap = GetPathById(ci.ObjectID);
            //            _fspaths.Add(_ap);
            //            reverse_search.Add(_ap, ci.ObjectID);
            //            _not_processed_objects.Remove(ci);
            //            _object_changed = true;
            //        }
            //    }
            //}
        }

        private String GetPathById(int id)
        {
            if (id == 0)
            {
                return "\\";
            }
            return (GetPathById(_fstree[id].Parent) + "\\" + _fstree[id].Name).Replace("\\\\", "\\").Replace("\\\\", "\\");
        }

        public void CreateFile(String path, String gn)
        {
            String[] elements = path.Split("\\".ToCharArray());
            String _fname = elements[elements.Length - 1];
            String _p_dir = path.Remove(path.Length - _fname.Length - 1);
            XakepFSObject fsobj = new XakepFSObject();
            fsobj.AccessControl = new FileSecurity();
            fsobj.Attributes = FileAttributes.Normal;
            fsobj.CreatedTime = DateTime.Now;
            fsobj.IsDeleted = false;
            fsobj.IsDirectory = false;
            fsobj.LastAccessTime = DateTime.Now;
            fsobj.LastWriteTime = DateTime.Now;
            fsobj.Length = 0;
            fsobj.Name = _fname;
            int _objid = new Random().Next();
            while (_fstree.ContainsKey(_objid)) { _objid = new Random().Next(); }
            fsobj.ObjectID = _objid;
            if (_p_dir == "")
            {
                
            }
            else if (_p_dir != "\\")
            {
                //_p_dir = _p_dir.Remove(_p_dir.Length - 2);
            }
            fsobj.DataLocation = $"{_p_dir}\\{gn}";
            //File.Create(RootDataDirectory + ((_p_dir == "" || _p_dir == "\\") ? "" : _p_dir) + gn);
            if (_p_dir == "")
            {
                _p_dir = "\\";
                fsobj.Parent = 0;
            }
            else
            {
                fsobj.Parent = reverse_search[_p_dir];
            }
            _fstree.Add(_objid, fsobj);
            _fspaths.Add(GetPathById(_objid));
            reverse_search.Add(path, _objid);
            return;
        }

        public void CreateDirectory(String path, String gn)
        {
            String[] elements = path.Split("\\".ToCharArray());
            String _fname = elements[elements.Length - 1];
            String _p_dir = path.Remove(path.Length - _fname.Length - 1);
            XakepFSObject fsobj = new XakepFSObject();
            fsobj.AccessControl = new DirectorySecurity();
            fsobj.Attributes = FileAttributes.Normal;
            fsobj.CreatedTime = DateTime.Now;
            fsobj.IsDeleted = false;
            fsobj.IsDirectory = true;
            fsobj.LastAccessTime = DateTime.Now;
            fsobj.LastWriteTime = DateTime.Now;
            fsobj.Length = 0;
            fsobj.Name = _fname;
            int _objid = new Random().Next();
            while (_fstree.ContainsKey(_objid)) { _objid = new Random().Next(); }
            fsobj.ObjectID = _objid;
            if (_p_dir == "")
            {
                _p_dir = "\\";
                fsobj.Parent = 0;
            }
            if (_p_dir != "\\")
            {
                _p_dir = _p_dir.Remove(_p_dir.Length - 2);
            }
            fsobj.DataLocation = $"{_p_dir}\\{gn}";
            fsobj.Parent = reverse_search[_p_dir];
            //Directory.CreateDirectory(RootDataDirectory + ((_p_dir == "" || _p_dir == "\\") ? "" : _p_dir) + gn);
            _fstree.Add(_objid, fsobj);
            _fspaths.Add(GetPathById(_objid));
            reverse_search.Add(path, _objid);
            return;
        }

        public void DeleteFile(String path)
        {
            int id = reverse_search[path];
            _fstree[id].IsDeleted = true;
        }

        public void DeleteDirectory(String path)
        {
            DeleteFile(path);
        }

        public List<FileInformation> EnumerateFSEntries(String path)
        {
            var result = new List<FileInformation>();
            var fsobj = _fstree[reverse_search[path]];
            //Объект папки
            foreach (var ci in _fstree.Values.ToList().FindAll(n => n.Parent == fsobj.ObjectID && n.IsDeleted == false)) //Выбираем все объекты-наследники данного
            {
                FileInformation fi = new FileInformation();
                fi.Attributes = ci.Attributes;
                fi.CreationTime = ci.CreatedTime;
                fi.FileName = ci.Name;
                fi.LastAccessTime = ci.LastAccessTime;
                fi.LastWriteTime = ci.LastWriteTime;
                fi.Length = ci.Length;
                result.Add(fi);
            }
            return result;
        }

        public XakepFSObject GetFSObject(String path)
        {
            if (!reverse_search.ContainsKey(path)) return null;
            return _fstree[reverse_search[path]];
        }

        public FileSystemSecurity GetSecurity(int id)
        {
            return _fstree[id].AccessControl;
        }
    }
}
