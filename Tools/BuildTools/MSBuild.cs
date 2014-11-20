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
                //We're assuming that the latest visual studio (even partially installed) is fully installed. This can be a faulty assumption.
                object test = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\MSBuild\\ToolsVersions\\4.0", "MSBuildToolsPath", null);

                return Path.Combine(test.ToString(), "msbuild.exe");
            }
        }
        

        public int Run(string args, string solutionPath = null)
        {
            if (solutionPath == null) solutionPath = this.solutionPath;
            var psi = new ProcessStartInfo(MSBuildPath);
            psi.Arguments = '"' + solutionPath + "\" " + args;
            psi.WorkingDirectory = Path.GetDirectoryName(solutionPath);

            // msbuild seems to be failing withouth shellexec for some reason
            psi.UseShellExecute = true;
            psi.RedirectStandardOutput = false;
            psi.RedirectStandardError = false;

            Console.WriteLine("Executing " + psi.FileName + " " + psi.Arguments);
            var p = Process.Start(psi);
            p.WaitForExit();
            ConsoleColor original = Console.ForegroundColor;
            //Console.WriteLine(p.StandardOutput.ReadToEnd());
            Console.ForegroundColor = ConsoleColor.Red;
            //Console.Write(p.StandardError.ReadToEnd());
            if (p.ExitCode != 0) Console.WriteLine("Visual Studio may have encountered errors during the build.");
            Console.ForegroundColor = original;
            return p.ExitCode;
        }


    }
}
