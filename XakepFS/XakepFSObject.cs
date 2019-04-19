using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.AccessControl;
using Newtonsoft.Json;

namespace XakepFS
{
    class XakepFSObject
    {
        public String Name = "";
        public bool IsDirectory = false;
        public int Parent = 0;
        public int ObjectID = 0;
        public long Length = 0;
        public DateTime CreatedTime = DateTime.Now;
        public DateTime LastWriteTime = DateTime.Now;
        public DateTime LastAccessTime = DateTime.Now;
        public FileAttributes Attributes = FileAttributes.Normal;
        public FileSystemSecurity AccessControl;
        public String DataLocation = "";
        public bool IsDeleted = false;

        private String PackObject(Object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
        private Object UnpackObject(String s)
        {
            return JsonConvert.DeserializeObject(s);
        }
        public String PackJson()
        {
            var obj = new Dictionary<String, String>();
            obj.Add("Name", Name);
            obj.Add("IsDirectory", PackObject(IsDirectory));
            obj.Add("ParentID", PackObject(Parent));
            obj.Add("ObjectID", PackObject(ObjectID));
            obj.Add("Length", PackObject(Length));
            obj.Add("CreatedTime", PackObject(CreatedTime));
            obj.Add("AccessTime", PackObject(LastAccessTime));
            obj.Add("WriteTime", PackObject(LastWriteTime));
            obj.Add("Attributes", PackObject(Attributes));
            obj.Add("DataLocation", DataLocation);
            obj.Add("IsDeleted", PackObject(IsDeleted));
            return JsonConvert.SerializeObject(obj);
        }

        public void UnpackJson(String json)
        {
            var obj = JsonConvert.DeserializeObject<Dictionary<String, String>>(json);
            Name = obj["Name"];
            IsDirectory = (bool)UnpackObject(obj["IsDirectory"]);
            Parent = Convert.ToInt32(obj["ParentID"]);
            ObjectID = Convert.ToInt32(obj["ObjectID"]);
            Length = Convert.ToInt64(obj["Length"]);
            CreatedTime = (DateTime)UnpackObject(obj["CreatedTime"]);
            LastAccessTime = (DateTime)UnpackObject(obj["AccessTime"]);
            LastWriteTime = (DateTime)UnpackObject(obj["WriteTime"]);
            Attributes = (FileAttributes)Convert.ToInt32(obj["Attributes"]);
            DataLocation = obj["DataLocation"];
            IsDeleted = (bool)UnpackObject(obj["IsDeleted"]);
            AccessControl = null;
        }
    }
}
