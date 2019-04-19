using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XakepFS
{
    class Program
    {
        static void Main(string[] args)
        {
            DokanNet.Dokan.Unmount('M');
            DokanNet.Dokan.Mount(new XakepFSClass(), "M:\\");
            //DokanNet.Dokan.Mount(new HelloWorldFSClass(), "M:\\");
        }
    }
}
