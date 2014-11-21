using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace BuildTools
{
    public class MSBuild
    {
        protected string solutionPath = null;

        public MSBuild(string solutionPath)
        {
            this.solutionPath = solutionPath;
        }

        public static string MSBuildPath
        {
            get
            {
                object test = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\MSBuild\\ToolsVersions\\4.0", "MSBuildToolsPath", null);

                return Path.Combine(test.ToString(), "msbuild.exe");
            }
        }
        

        public int Run(string args, string solutionPath = null)
        {
            if (solutionPath == null) solutionPath = this.solutionPath;
            var psi = new ProcessStartInfo(MSBuildPath);
            psi.Arguments = '"' + solutionPath + "\" /v:m /clp:ErrorsOnly " + args;
            psi.WorkingDirectory = Path.GetDirectoryName(solutionPath);

            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = false;
            psi.RedirectStandardError = false;

            Console.WriteLine("Executing " + psi.FileName + " " + psi.Arguments);
            var p = Process.Start(psi);
            p.WaitForExit();
            
            if (p.ExitCode != 0) Console.WriteLine("Visual Studio may have encountered errors during the build.");

            Console.WriteLine("Exit code " + p.ExitCode.ToString());
            return p.ExitCode;
        }


    }
}
