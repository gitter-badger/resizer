using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace ImageResizer.ReleaseBuilder {
    class Program {
        [STAThread]
        static int Main(string[] args) {
            Build b = new Build();
            return b.Run();
        }
    }
}
