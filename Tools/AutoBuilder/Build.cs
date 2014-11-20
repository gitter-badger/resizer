﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using BuildTools;
using System.Net;
using System.Collections.Specialized;
using Amazon.S3.Transfer;

namespace ImageResizer.ReleaseBuilder {
    public class Build :Interaction {
        FolderFinder f = new FolderFinder("Core" );
        MSBuild d = null;
        FsQuery q = null;
        VersionEditor v = null;
        GitManager g = null;
        NugetManager nuget = null;
        TransferUtility s3 = null;
        
        string bucketName = "resizer-downloads";
        string linkBase = "http://downloads.imageresizing.net/";
        public Build() {
            d = new MSBuild(Path.Combine(f.FolderPath,"ImageResizer.sln"));
            v = new VersionEditor(Path.Combine(f.FolderPath, "SharedAssemblyInfo.cs"));
            g = new GitManager(f.ParentPath);
            nuget = new NugetManager(Path.Combine(f.ParentPath, "nuget"));


            packages.Add(new PackageDescriptor("min", PackMin));
            packages.Add(new PackageDescriptor("full", PackFull));
            packages.Add(new PackageDescriptor("standard", PackStandard));
            packages.Add(new PackageDescriptor("allbinaries", PackAllBinaries));
        }

        public string getReleasePath(string packageBase, string ver,  string kind, string hotfix) {
            return Path.Combine(Path.Combine(f.ParentPath, "Releases"), packageBase + ver.Trim('-') + '-' + kind + "-" + (string.IsNullOrWhiteSpace(hotfix) ? "" : (hotfix.Trim('-') +  "-")) + DateTime.UtcNow.ToString("MMM-d-yyyy") + ".zip");
        }

        public NameValueCollection GetNugetVariables() {
            var nvc = new NameValueCollection();
            nvc["author"] = "Nathanael Jones, Imazen";
            nvc["owners"] = "nathanaeljones, imazen";
            nvc["pluginsdlldir"] = @"..\dlls\trial";
            nvc["coredlldir"] = @"..\dlls\release";
            nvc["iconurl"] = "http://imageresizing.net/images/logos/ImageIconPSD100.png";
 

            nvc["plugins"] = "## 30+ plugins available\n\n" + 
                    "Search 'ImageResizer' on nuget.org, or visit imageresizing.net to see 40+ plugins, including WPF, WIC, FreeImage, OpenCV, AForge &amp; Ghostscript (PDF) integrations. " + 
                    "You'll also find  plugins for disk caching, memory caching, Microsoft SQL blob support, Amazon CloudFront, S3, Azure Blob Storage, MongoDB GridFS, automatic whitespace trimming, " +
                    "automatic white balance, octree 8-bit gif/png quantization and transparency dithering, animated gif resizing, watermark &amp; text overlay support, content aware image resizing /" + 
                    " seam carving (based on CAIR), grayscale, sepia, histogram, alpha, contrast, saturation, brightness, hue, Guassian blur, noise removal, and smart sharpen filters, psd editing &amp; " +
                    "rendering, raw (CR2, NEF, DNG, etc.) file exposure, .webp (weppy) support, image batch processing &amp; compression into .zip archives, red eye auto-correction,  face detection, and " + 
                    "secure (signed!) remote HTTP image processing. Most datastore plugins support the Virtual Path Provider system, and can be used for non-image files as well.\n\n";
                    

            return nvc;
        }

        List<PackageDescriptor> packages = new List<PackageDescriptor>();

        [STAThread]
        public void Run() {
            say("Project root: " + f.ParentPath);
            nl();
            //The base name for creating zip packags.
            string packageBase = v.get("PackageName"); //    // [assembly: PackageName("Resizer")]

            // Load env vars
            nuget.apiKey = Environment.GetEnvironmentVariable("ab_nugetkey");
            string s3ID = Environment.GetEnvironmentVariable("ab_s3id");
            string s3Key = Environment.GetEnvironmentVariable("ab_s3key");
            string ab_s3bucket = Environment.GetEnvironmentVariable("ab_s3bucket");

            if (ab_s3bucket != null)
                bucketName = ab_s3bucket;

            string ab_version = Environment.GetEnvironmentVariable("ab_version");
            string ab_hotfix = Environment.GetEnvironmentVariable("ab_hotfix");
            string ab_downloadserver = Environment.GetEnvironmentVariable("ab_downloadserver");
            string ab_nugetserver = Environment.GetEnvironmentVariable("ab_nugetserver");


            if(s3ID == null || s3Key == null || nuget.apiKey == null)
            {
                say("Env vars ab_s3id, ab_s3key and ab_nugetkey must be specified.");
                return;
            }


            string fileVer = v.get("AssemblyFileVersion").TrimEnd('.', '*');
            string assemblyVer = v.get("AssemblyVersion").TrimEnd('.', '*');
            string infoVer = v.get("AssemblyInformationalVersion").TrimEnd('.', '*');
            string nugetVer = v.get("NugetVersion").TrimEnd('.', '*');


            // if set, replace assemnly version info
            if (ab_version != null)
            {
                fileVer = ab_version;
                assemblyVer = ab_version;
                nugetVer = ab_version;
                infoVer  = ab_version.Replace(".", "-");
            }
            
            list("FileVersion", fileVer);
            list("AssemblyVersion", assemblyVer);
            list("InfoVersion", infoVer);
            list("NugetVersion", nugetVer);

            
            // if set, assume hotfix
            bool isHotfix = (ab_hotfix != null);
            string packageHotfix = isHotfix ? ("-hotfix-" + DateTime.Now.ToString("htt").ToLower()) : "";


            //Get the download server from env or SharedAssemblyInfo.cs
            string downloadServer = v.get("DownloadServer");
            if (ab_downloadserver != null) downloadServer = ab_downloadserver;
            else if (downloadServer == null) downloadServer = "http://downloads.imageresizing.net/";



            StringBuilder downloadPaths = new StringBuilder();
            foreach (PackageDescriptor desc in packages) {
                desc.Path = getReleasePath(packageBase, infoVer, desc.Kind, packageHotfix);
                string opts = "cu";
                
                desc.Options = opts;
                if (desc.Upload) {
                    downloadPaths.AppendLine(downloadServer + Path.GetFileName(desc.Path));
                }
            }

            if (downloadPaths.Length > 0){
                say("Once complete, your files will be available at");
                say(downloadPaths.ToString());
            }



            //Get all the .nuspec packages on in the /nuget directory.
            IList<NPackageDescriptor> npackages =NPackageDescriptor.GetPackagesIn(Path.Combine(f.ParentPath,"nuget"));

            bool isMakingNugetPackage = false;

            foreach (NPackageDescriptor desc in npackages) {
                desc.VariableSubstitutions = GetNugetVariables();
                desc.VariableSubstitutions["version"] = nugetVer;
                desc.Version = nugetVer;
                desc.OutputDirectory = Path.Combine(Path.Combine(f.ParentPath, "Releases", "nuget-packages"));

                if (!Directory.Exists(desc.OutputDirectory)) Directory.CreateDirectory(desc.OutputDirectory);

                say(Path.GetFileName(desc.PackagePath) + (desc.PackageExists ?  " exists" : " not found"), desc.PackageExists ? ConsoleColor.Green : ConsoleColor.Gray);
                say(Path.GetFileName(desc.SymbolPackagePath) + (desc.SymbolPackageExists ? " exists" : " not found"), desc.SymbolPackageExists ? ConsoleColor.Green : (desc.PackageExists ? ConsoleColor.Red : ConsoleColor.Gray));

            }



           

            //Set the default for every package
            string selection = "cu";
            foreach (NPackageDescriptor desc in npackages) desc.Options = selection;
            isMakingNugetPackage = npackages.Any(desc => desc.Build);

            var s3config = new Amazon.S3.AmazonS3Config();
            s3config.Timeout = TimeSpan.FromHours(12);
            s3config.RegionEndpoint = Amazon.RegionEndpoint.USEast1;
            var s3client = new Amazon.S3.AmazonS3Client(s3ID, s3Key,s3config);
            s3 = new TransferUtility(s3client);



            //1 (moved execution to 8a)
            bool cleanAll = true;

            //2 - Set version numbers (with *, if missing)
            string originalContents = v.Contents; //Save for checking changes.
            v.set("AssemblyFileVersion", v.join(fileVer, "*"));
            v.set("AssemblyVersion", v.join(assemblyVer, "*"));
            v.set("AssemblyInformationalVersion", infoVer);
            v.set("NugetVersion", nugetVer);
            v.set("Commit", "git-commit-guid-here");
            v.Save();
            //Save contents for reverting later
            string fileContents = v.Contents;

                
            //Generate hard revision number for building (so all dlls use the same number)
            short revision = (short)(DateTime.UtcNow.TimeOfDay.Milliseconds % short.MaxValue); //the part under 32767. Can actually go up to, 65534, but what's the point.
            string exactVersion = v.join(fileVer, revision.ToString());
            string fullInfoVer = infoVer + (isHotfix ? ("-temp-hotfix-" + DateTime.Now.ToString("MMM-d-yyyy-htt").ToLower()) : "");
            string tag = "resizer" + v.join(infoVer, revision.ToString()) + (isHotfix ? "-hotfix": "");

            //4b - change to hard version number for building
                
            v.set("AssemblyFileVersion", exactVersion);
            v.set("AssemblyVersion", exactVersion);
            //Add hotfix suffix for hotfixes
            v.set("AssemblyInformationalVersion", fullInfoVer);
            v.Save();


            //Prepare searchersq
            PrepareForPackaging();

            bool success = false;
            //Allows use to temporarily edit all the sample project files
            using (RestorePoint rp = new RestorePoint(q.files(new Pattern("^/Plugins/*/*.(cs|vb)proj$"), new Pattern("^/Contrib/*/*.(cs|vb)proj$")))) {

                //Replace all project references temporarily
                foreach (string pf in rp.Paths) {
                    new ProjectFileEditor(pf).RemoveStrongNameRefs();
                }

                //8a Clean projects if specified
                if (cleanAll) {
                    CleanAll();
                }

                //6 - if (c) was specified for any package, build all.
                success = BuildAll(true); //isMakingNugetPackage);

                //7 - Revert file to state at commit (remove 'full' version numbers and 'commit' value)
                v.Contents = fileContents;
                q.Rescan(); //Rescan filesystem to prevent errors building the archive (since we delete stuff in CleanAll())
                v.Save();

                if (!success) return; //If the build didn't go ok, pause and exit

                //8b - run cleanup routine
                RemoveUselessFiles();
            }

            //Allows use to temporarily edit all the sample project files
            using (RestorePoint rp = new RestorePoint(q.files(new Pattern("^/Samples/*/*.(cs|vb)proj$")))) {

                //Replace all project references temporarily
                foreach (string pf in q.files(new Pattern("^/Samples/[^/]+/*.(cs|vb)proj$"))) {
                    new ProjectFileEditor(pf).ReplaceAllProjectReferencesWithDllReferences("..\\..\\dlls\\release").RemoveStrongNameRefs();
                }


                //9 - Pacakge all selected zip configurations
                foreach (PackageDescriptor pd in packages) {
                    if (pd.Skip || !pd.Build) continue;
                    if (pd.Exists && pd.Build) {
                        File.Delete(pd.Path);
                        say("Deleted " + pd.Path);
                    }
                    pd.Builder(pd);
                    //Copy to a 'tozip' version for e-mailing
                    //File.Copy(pd.Path, pd.Path.Replace(".zip", ".tozip"), true);
                }
            }




            //10 - Pacakge all nuget configurations
            foreach (NPackageDescriptor pd in npackages) {
                if (pd.Skip) continue;
                
                if (pd.Build) nuget.Pack(pd);

            }

            //11 - Upload all selected zip configurations
            foreach (PackageDescriptor pd in packages) {
                if (pd.Skip) continue;
                if (pd.Upload) {
                    if (!pd.Exists) {
                        say("Can't upload, file missing: " + pd.Path);
                        continue;
                    }
                    var request = new TransferUtilityUploadRequest();
                    request.CannedACL = pd.Private ? Amazon.S3.S3CannedACL.Private : Amazon.S3.S3CannedACL.PublicRead;
                    request.BucketName = bucketName;
                    request.ContentType = "application/zip";
                    request.Key = Path.GetFileName(pd.Path);
                    request.FilePath = pd.Path;


                    say("Uploading " + Path.GetFileName(pd.Path) + " to " + bucketName + " with CannedAcl:" + request.CannedACL.ToString());
                    int retry = 3;
                    do {
                        //Upload
                        try {
                            s3.Upload(request);
                            retry = -1;
                        } catch (Exception ex) {
                            retry--;
                        }
                    } while (retry >= 0);

                    say("Finished uploading " + Path.GetFileName(pd.Path));
                } 
            }


            //2 - Upload all nuget configurations
            foreach (NPackageDescriptor pd in npackages) {
                if (pd.Skip || !pd.Upload) continue;
                nuget.Push(pd, ab_nugetserver);

            }



            //12 - Generate template for release notes article

            say("Everything is done.");
            
        }

        public void CleanAll(){
            try { System.IO.Directory.Delete(Path.Combine(f.ParentPath, "dlls\\trial"), true); } catch { }
            try { System.IO.Directory.Delete(Path.Combine(f.ParentPath, "dlls\\release"), true); } catch { }
            try { System.IO.Directory.Delete(Path.Combine(f.ParentPath, "dlls\\debug"), true); } catch { }

            d.Run("/t:Clean /p:Configuration=Debug");
            d.Run("/t:Clean /p:Configuration=Release");
            d.Run("/t:Clean /p:Configuration=Trial");

        }

        public bool BuildAll(bool buildDebug) {
            int result = d.Run("/p:Configuration=Release") + //Have to run Release first, since ImageResizerGUI includes the DLLs.
            d.Run("/p:Configuration=Trial");
            if (buildDebug) result += d.Run("/p:Configuration=Debug");

            if (result > 0)
            {
                say("There may have been build errors.");
                return false;
            }
            return true;
        }


        public void RemoveUselessFiles() {
            var f = new Futile(Console.Out);
            var q = new FsQuery(this.f.ParentPath, new string[]{"/.git","^/Releases", "^/Tests/Builder"});


            //delete /Tests/binaries  (*.pdb, *.xml, *.dll)
            //delete /Core/obj folder
            //Deleate all bin,obj,imageacache,uploads, and results folders under /Samples, /Tests, and /Plugins
            f.DelFiles(q.files("^/(Tests|Plugins|Samples)/*/(bin|obj|imagecache|uploads|results)/*",
                       "^/Core/obj/*","^/Core.Mvc/obj/*"));


            f.DelFiles(q.files("^/Samples/MvcSample/App_Data/*"));

            //delete .xml and .pdb files for third-party libs
            f.DelFiles(q.files("^/dlls/*/(Aforge|LitS3|Ionic)*.(pdb|xml)$"));

            //delete Thumbs.db
            //delete */.DS_Store
            f.DelFiles(q.files("/Thumbs.db$",
                                "/.DS_Store$"));
            q = null;
            
        }


        public string[] standardExclusions = new string[]{
                "/.git","^/Releases","/Hidden/","^/Legacy","^/Tools/(Builder|BuildTools|docu)", "^/submodules/docu",
                "^/Samples/Images/(extra|private)/","/Thumbs.db$","/.DS_Store$",".suo$",".cache$",".user$", "/._","/~$", 
                "^/Samples/MvcSample/App_Data/"

            };

        public void PrepareForPackaging() {
            if (q == null) q = new FsQuery(this.f.ParentPath, standardExclusions);
            //Don't copy XML or PDB files for the following libs:
            q.exclusions.Add(new Pattern("/(Newtonsoft.Json|DotNetZip|Aforge|LitS3|Ionic|NLog|MongoDB|Microsoft.|AWSSDK)*.(xml|pdb)$"));
            //Don't copy XML for these (but do keep pdb)
            q.exclusions.Add(new Pattern("/(OpenCvSharp|FreeImageNet)*.xml$"));
            //Exclude dependencies handled by NDP
            q.exclusions.Add(new Pattern("/(FreeImage|gsdll32|gsdll64).dll$")); 
            
            //Exclude infrequently used but easily buildable stuff
            q.exclusions.Add(new Pattern("/ImageResizerGUI.exe$"));
            
            //Exclude resharper junk
            q.exclusions.Add(new Pattern("_ReSharper"));

            //Exclude temorary files
            q.exclusions.Add(new Pattern("^/Contrib/*/(bin|obj|imagecache|uploads|results)/*"));
            q.exclusions.Add(new Pattern("^/(Tests|Plugins|Samples)/*/(bin|obj|imagecache|uploads|hidden|results)/"));
            q.exclusions.Add(new Pattern("^/Core(.Mvc)?/obj/"));
            q.exclusions.Add(new Pattern("^/Tests/binaries"));

            //Exclude stuff that is not used or generally useful
            q.exclusions.Add(new Pattern("^/Tests/LibDevCassini"));
            q.exclusions.Add(new Pattern("^/Tests/ComparisonBenchmark/Images"));
            q.exclusions.Add(new Pattern("^/Samples/SqlReaderSampleVarChar"));
            q.exclusions.Add(new Pattern(".config.transform$"));
            q.exclusions.Add(new Pattern("^/Plugins/Libs/FreeImage/Examples/")); //Exclude examples folder
            q.exclusions.Add(new Pattern("^/Plugins/Libs/FreeImage/Wrapper/(Delphi|VB6|FreeImagePlus)")); //Exclude everything except the FreeImage.NET folder
            q.exclusions.Add(new Pattern("^/Plugins/Libs/FreeImage/Wrapper/FreeImage.NET/cs/[^L]*/")); //Exclude everything except the library folder
            
        }
        public void PackMin(PackageDescriptor desc) {
            // 'min' - /dlls/release/ImageResizer.* - /
            // /*.txt
            using (var p = new Package(desc.Path, this.f.ParentPath)) {
                p.Add(q.files("^/dlls/release/ImageResizer.(Mvc.)?(dll|pdb|xml)$"), "/", "dlls/release");
                p.Add(q.files("^/readme.txt$"));
                p.Add(q.files("^/Core/license.txt$"), "");
                p.Add(q.files("^/Web.config$"));
            }
        }
        public void PackAllBinaries(PackageDescriptor desc) {
            using (var p = new Package(desc.Path, this.f.ParentPath)) {
                p.Add(q.files("^/dlls/release/*.(dll|pdb)$"), "/", "dlls/release");
                p.Add(q.files("^/[^/]+.txt$"));
            }
        }
        public void PackFull(PackageDescriptor desc) {
            // 'full'
            using (var p = new Package(desc.Path, this.f.ParentPath)) {
                p.Add(q.files("^/(core|contrib|core.mvc|plugins|samples|tests|studiojs)/"));
                p.Add(q.files("^/tools/COMInstaller"));
                p.Add(q.files("^/dlls/(debug|release)"));
                p.Add(q.files("^/dlls/release/ImageResizer.(Mvc.)?(dll|pdb|xml)$"), "/"); //Make a copy in the root
                p.Add(q.files("^/submodules/studiojs"), "/StudioJS"); //Copy submodules/studiojs -> /StudioJS
                p.Add(q.files("^/submodules/(lightresize|libwebp-net)")); 
                p.Add(q.files("^/[^/]+.txt$"));
                p.Add(q.files("^/Web.config$"));

                //Make a empty sample app for IIS
                p.Add(q.files("^/dlls/release/ImageResizer.(Mvc.)?(dll|pdb)$"), "/Samples/BasicIISSite/bin/");
                p.Add(q.files("^/dlls/release/ImageResizer.(Mvc.)?(dll|pdb)$"), "/Samples/SampleAspSite/bin/");
                p.Add(q.files("^/dlls/release/ImageResizer.Plugins.RemoteReader.(dll|pdb)$"), "/Samples/SampleAspSite/bin/"); 
                p.Add(q.files("^/Web.config$"),"/Samples/BasicIISSite/");
            }
        }
        public void PackStandard(PackageDescriptor desc) {
            // 'standard'
            List<Pattern> old = q.exclusions;
            q.exclusions = new List<Pattern>(old);
            q.exclusions.Add(new Pattern("^/Core/[^/]+.sln")); //Don't include the regular solution files, they won't load properly.
            using (var p = new Package(desc.Path, this.f.ParentPath)) {
                p.Add(q.files("^/dlls/release/ImageResizer.(Mvc.)?(dll|pdb|xml)$"), "/");
                p.Add(q.files("^/dlls/(debug|release)/"));
                p.Add(q.files("^/submodules/studiojs"), "/StudioJS"); //Copy submodules/studiojs -> /StudioJS
                p.Add(q.files("^/(core|samples)/"));
                p.Add(q.files("^/[^/]+.txt$"));
                p.Add(q.files("^/Web.config$"));
            }
            q.exclusions = old;
        }







    }
}
