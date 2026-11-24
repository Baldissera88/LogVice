namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // Made-up logs for one made-up laptop, CONTOSO-LT0417: about two hours of ConfigMgr client, Intune and
    // Windows servicing activity that tell one story, so every view has something to show. Nothing here is read
    // from this PC. The same seed gives the same lines, so the logs only change with the date.
    public static class Demo
    {
        public const string Computer = "CONTOSO-LT0417";

        public static readonly string[] CmFiles =
        {
            "AppEnforce.log", "AppDiscovery.log", "AppIntentEval.log", "CAS.log",
            "IntuneManagementExtension.log", "AppWorkload.log", "AgentExecutor.log", "smsts.log"
        };

        public const string CbsFile = "CBS.log";
        public const string JsonFile = "contoso-agent.jsonl";

        public static string Folder
        {
            get { return Path.Combine(Path.GetTempPath(), "LogVice-demo"); }
        }

        // Writes the logs for today into %TEMP%\LogVice-demo and returns every path.
        public static string[] Write()
        {
            return Write(Folder, DateTime.Today);
        }

        // The CMTrace logs only, in the order of CmFiles. These are the ones --demo opens.
        public static string[] CmPaths(string[] all)
        {
            List<string> list = new List<string>();
            foreach (string f in CmFiles)
                foreach (string p in all)
                    if (string.Equals(Path.GetFileName(p), f, StringComparison.OrdinalIgnoreCase)) list.Add(p);
            return list.ToArray();
        }

        public static string[] Write(string folder, DateTime day)
        {
            Directory.CreateDirectory(folder);
            foreach (string old in Directory.GetFiles(folder))
            {
                string n = Path.GetFileName(old);
                if (n.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(old); } catch (Exception) { }
                }
            }
            Maker m = new Maker(day.Date.AddHours(8).AddMinutes(12));
            m.Run();
            List<string> paths = new List<string>();
            foreach (string f in CmFiles) paths.Add(m.SaveCm(folder, f));
            paths.Add(m.SaveCbs(folder));
            paths.Add(m.SaveJson(folder));
            return paths.ToArray();
        }

        sealed class Entry
        {
            public DateTime T;
            public int Seq;
            public string Comp;
            public int Thread, Type;
            public string Msg;
        }

        sealed class App
        {
            public string Name, Version, Dt, Content, Pkg;
            public string Scope = "ScopeId_8B1F6A2C-4D3E-4F50-9A6B-7C8D9E0F1A2B";

            public string Id { get { return Scope + "/DeploymentType_" + Dt; } }
        }

        sealed class Maker
        {
            readonly DateTime start;
            readonly Random rnd = new Random(417);
            readonly Dictionary<string, List<Entry>> files = new Dictionary<string, List<Entry>>();
            readonly List<Entry> jsonEntries = new List<Entry>();
            int seq;

            readonly App fabrikam = new App { Name = "Fabrikam PDF Viewer 3.1", Version = "3.1.0", Dt = "5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f", Content = "Content_0a7e9c11-52d4-4b8a-9f36-1d2c3b4a5e6f", Pkg = "PS100112" };
            readonly App timesheet = new App { Name = "Contoso Timesheet 4.2", Version = "4.2.1", Dt = "6d2e3f4a-5b6c-4d7e-8f90-a1b2c3d4e5f6", Content = "Content_1b8fad22-63e5-4c9b-8a47-2e3d4c5b6f70", Pkg = "PS100113" };
            readonly App reporter = new App { Name = "Northwind Reporter 12.0", Version = "12.0.4", Dt = "7e3f4a5b-6c7d-4e8f-9a01-b2c3d4e5f607", Content = "Content_2c90be33-74f6-4daf-9b58-3f4e5d6c7a81", Pkg = "PS100114" };
            readonly App printer = new App { Name = "Woodgrove Printer Driver 2.5", Version = "2.5.0", Dt = "8f4a5b6c-7d8e-4f90-8b12-c3d4e5f60718", Content = "Content_3da1cf44-8507-4eb0-8c69-4a5f6e7d8b92", Pkg = "PS100115" };
            readonly App expenses = new App { Name = "Contoso Expenses 2.0", Version = "2.0.3", Dt = "9a5b6c7d-8e9f-4a01-9c23-d4e5f6071829", Content = "Content_4eb2d055-9618-4fc1-9d7a-5b6a7f8e9ca3", Pkg = "PS100116" };
            readonly App fonts = new App { Name = "Fabrikam Font Pack", Version = "1.8.0", Dt = "ab6c7d8e-9fa0-4b12-8d34-e5f607182930", Content = "Content_5fc3e166-a729-40d2-8e8b-6c7b8a9fadb4", Pkg = "PS100117" };
            readonly App badge = new App { Name = "Woodgrove Badge Reader", Version = "3.3.2", Dt = "bc7d8e9f-a0b1-4c23-9e45-f60718293a41", Content = "Content_60d4f277-b83a-41e3-9f9c-7d8c9bab0ec5", Pkg = "PS100118" };
            readonly App notesCm = new App { Name = "Northwind Notes 2.1", Version = "2.1.0", Dt = "cd8e9fa0-b1c2-4d34-8f56-0718293a4b52", Content = "Content_71e50388-c94b-42f4-8aad-8e9dacbc1fd6", Pkg = "PS100119" };

            public Maker(DateTime start)
            {
                this.start = start;
            }

            DateTime At(double s)
            {
                return start.AddMilliseconds(Math.Round(s * 1000.0));
            }

            void Cm(string file, double s, string comp, int thread, int type, string msg)
            {
                Add(file, At(s), comp, thread, type, msg);
            }

            void Add(string file, DateTime t, string comp, int thread, int type, string msg)
            {
                List<Entry> l;
                if (!files.TryGetValue(file, out l))
                {
                    l = new List<Entry>();
                    files[file] = l;
                }
                Entry e = new Entry();
                e.T = t;
                e.Seq = seq++;
                e.Comp = comp;
                e.Thread = thread;
                e.Type = type;
                e.Msg = msg;
                l.Add(e);
            }

            double Jitter(double max)
            {
                return rnd.NextDouble() * max;
            }

            string Guid(int n)
            {
                byte[] b = new byte[16];
                rnd.NextBytes(b);
                b[0] = (byte)n;
                b[7] = (byte)((b[7] & 0x0F) | 0x40);
                b[8] = (byte)((b[8] & 0x3F) | 0x80);
                return new System.Guid(b).ToString();
            }

            public void Run()
            {
                TaskSequence();
                Fabrikam();
                Timesheet();
                Reporter();
                Printer();
                EvalCycles();
                IntuneSync();
                IntuneApps();
                Scripts();
                Servicing();
                Agent();
            }

            // ---------- ConfigMgr client ----------

            void Enforce(App a, double s, int exitCode, bool detectFails, string tag)
            {
                const string f = "AppEnforce.log";
                const string c = "AppEnforce";
                int th = 4128;
                bool exe = a == reporter;
                string cmd = exe ? "\"" + "setup.exe" + "\" /S /norestart" : "msiexec /i \"" + a.Name.Replace(" ", "").Replace(".", "") + ".msi\" /qn /norestart";
                string full = exe ? "\"C:\\Windows\\ccmcache\\" + a.Pkg.Substring(5) + "\\setup.exe\" /S /norestart" : "\"C:\\Windows\\system32\\msiexec.exe\" /i \"" + a.Name.Replace(" ", "").Replace(".", "") + ".msi\" /qn /norestart";
                int secs = 18 + rnd.Next(30);
                string desc = "App DT \"" + a.Name + "\" ApplicationDeliveryType - " + a.Id + ", Revision - " + (2 + rnd.Next(3)) + ", ContentPath - C:\\Windows\\ccmcache\\" + a.Pkg.Substring(5) + ", Execution Context - System";
                double t = s;
                Cm(f, t, c, th, 1, "+++ Starting Install enforcement for " + desc);
                Cm(f, t += 0.1, c, th, 1, "    A user is not logged on to the system.");
                Cm(f, t += 0.2, c, th, 1, "    Performing detection of app deployment type " + a.Name + "(" + a.Id + ", revision 3) for system.");
                Cm(f, t += 0.6, c, th, 1, "+++ Application not discovered. [AppDT Id: " + a.Id + ", Revision: 3]");
                Cm(f, t += 0.1, c, th, 1, "    App enforcement environment:");
                Cm(f, t += 0.0, c, th, 1, "\tContext: Machine\r\n\tCommand line: " + cmd + "\r\n\tAllow user interaction: No\r\n\tUI mode: 1\r\n\tSession Id: 4294967295\r\n\tContent path: C:\\Windows\\ccmcache\\" + a.Pkg.Substring(5) + "\r\n\tWorking directory: ");
                Cm(f, t += 0.3, c, th, 1, "    Prepared working directory: C:\\Windows\\ccmcache\\" + a.Pkg.Substring(5));
                Cm(f, t += 0.1, c, th, 1, "    Prepared command line: " + full);
                Cm(f, t += 0.4, c, th, 1, "    Valid MSI exit codes: " + (exe ? "(not an MSI)" : "0, 1707, 3010, 1641"));
                int pid = 3000 + rnd.Next(5000);
                Cm(f, t += 0.5, c, th, 1, "    Process " + pid + " created for the command line.");
                t += secs;
                Cm(f, t, c, th, 1, "    Process " + pid + " terminated with exitcode: " + exitCode);
                Cm(f, t += 0.2, c, th, 1, "    Looking for exit code " + exitCode + " in exit codes table...");
                if (exitCode == 0)
                {
                    Cm(f, t += 0.1, c, th, 1, "    Matched exit code 0 to a Success entry in exit codes table.");
                }
                else
                {
                    Cm(f, t += 0.1, c, th, 3, "    Unmatched exit code (" + exitCode + ") is considered an execution failure.");
                }
                if (exitCode == 0)
                {
                    if (detectFails)
                    {
                        Cm(f, t += 0.5, c, th, 1, "    Performing detection of app deployment type " + a.Name + "(" + a.Id + ", revision 3) for system.");
                        Cm(f, t += 0.6, c, th, 3, "+++ Application not discovered after installation. Detection rule not satisfied. [AppDT Id: " + a.Id + ", Revision: 3]");
                        Cm(f, t += 0.1, c, th, 3, "    Post-install detection failed. Reporting error 0x87D00324 (Application not detected after installation).");
                    }
                    else
                    {
                        Cm(f, t += 0.5, c, th, 1, "    Performing detection of app deployment type " + a.Name + "(" + a.Id + ", revision 3) for system.");
                        Cm(f, t += 0.6, c, th, 1, "+++ Discovered application [AppDT Id: " + a.Id + ", Revision: 3]");
                    }
                }
                else
                {
                    Cm(f, t += 0.2, c, th, 2, "    Install of " + a.Name + " failed. The next attempt is " + (tag ?? "in 60 minutes") + ".");
                }
                Cm(f, t += 0.2, c, th, detectFails || exitCode != 0 ? 2 : 1, "++++++ App enforcement completed (" + secs + " seconds) for App DT \"" + a.Name + "\" [" + a.Id + "], Revision: 3, User SID: ] ++++++");

                // The same run as the evaluation and discovery logs see it.
                Cm("AppIntentEval.log", s - 0.4, "AppIntentEval", 4128, 1, a.Id + ".3 :- Current State = NotInstalled, Applicable = Applicable, ResolvedState = Available, Wanted = Installed");
                string after = exitCode != 0 ? "Failed" : detectFails ? "Failed" : "Installed";
                Cm("AppIntentEval.log", t + 0.3, "AppIntentEval", 4128, exitCode != 0 || detectFails ? 3 : 1, a.Id + ".3 :- Current State = " + after + ", Applicable = Applicable, ResolvedState = " + (exitCode != 0 || detectFails ? "Failed" : "Installed") + ", Wanted = Installed");
                Cm("AppDiscovery.log", s + 0.3, "AppDiscovery", 4128, 1, "Entering ExecQueryAsync for " + a.Id + ".3");
                Cm("AppDiscovery.log", s + 0.9, "AppDiscovery", 4128, 1, "+++ Application not discovered. [AppDT Id: " + a.Id + ", Revision: 3]");
                Cm("AppDiscovery.log", s + secs + 1.5, "AppDiscovery", 4128, detectFails ? 3 : 1, detectFails ? "+++ Application not discovered. [AppDT Id: " + a.Id + ", Revision: 3] (0x87D00324)" : exitCode != 0 ? "+++ Application not discovered. [AppDT Id: " + a.Id + ", Revision: 3]" : "+++ Discovered application [AppDT Id: " + a.Id + ", Revision: 3]");
            }

            void Content(App a, double s, bool retry)
            {
                const string f = "CAS.log";
                const string c = "ContentAccess";
                int th = 2216;
                string loc = "http://dp01.contoso.example/SMS_DP_SMSPKG$/" + a.Content.Substring(8) + ".1";
                Cm(f, s, c, th, 1, "Requesting locations synchronously for content " + a.Content + ".1 with priority Foreground");
                Cm(f, s + 0.4, c, th, 1, "Download started for content " + a.Content + ".1");
                Cm(f, s + 0.9, c, th, 1, "Location update from CTM for content " + a.Content + ".1");
                double t = s + 3;
                if (retry)
                {
                    Cm(f, t, c, th, 2, "Download of content " + a.Content + ".1 from " + loc + " failed with error 0x80072EE7 (the server name or address could not be resolved).");
                    Cm(f, t + 0.2, c, th, 2, "Content " + a.Content + ".1 will be retried in 2 minutes. Attempt 1 of 3.");
                    t += 122;
                    Cm(f, t, c, th, 1, "Retrying download of content " + a.Content + ".1");
                    Cm(f, t + 0.5, c, th, 3, "Download of content " + a.Content + ".1 failed again with error 0x80072EE7 on distribution point dp01.contoso.example.");
                    Cm(f, t + 0.8, c, th, 1, "Trying the next distribution point: dp02.contoso.example");
                    t += 4;
                }
                int parts = 4 + rnd.Next(3);
                for (int i = 1; i <= parts; i++)
                {
                    Cm(f, t + i * 3.2, c, th, 1, "Content " + a.Content + ".1 download progress: " + (i * 100 / parts) + "% (" + (i * 100 / parts * 3) + " MB of 300 MB)");
                }
                Cm(f, t + parts * 3.2 + 1, c, th, 1, "Download completed for content " + a.Content + ".1");
                Cm(f, t + parts * 3.2 + 1.4, c, th, 1, "Content " + a.Content + ".1 is available in cache C:\\Windows\\ccmcache\\" + a.Pkg.Substring(5));
            }

            void Fabrikam()
            {
                Cm("AppIntentEval.log", 1398, "AppIntentEval", 4128, 1, "Policy for " + fabrikam.Id + " arrived, revision 3.");
                Content(fabrikam, 1400, true);
                Enforce(fabrikam, 1565, 0, false, null);
            }

            void Timesheet()
            {
                Content(timesheet, 1900, false);
                Enforce(timesheet, 1952, 1603, false, "in 60 minutes");
                Cm("AppIntentEval.log", 2030, "AppIntentEval", 4128, 2, timesheet.Id + ".3 :- Retry scheduled, attempt 2 of 3 in 60 minutes");
                Cm("AppEnforce.log", 5600, "AppEnforce", 4128, 1, "Retry timer elapsed for " + timesheet.Name);
                Enforce(timesheet, 5620, 0, false, null);
            }

            void Reporter()
            {
                Content(reporter, 2300, false);
                Enforce(reporter, 2360, 0, false, null);
                Content(expenses, 3300, false);
                Enforce(expenses, 3360, 0, false, null);
            }

            void Printer()
            {
                Content(printer, 3020, false);
                Enforce(printer, 3090, 0, true, null);
            }

            void EvalCycles()
            {
                App[] apps = { fabrikam, timesheet, reporter, printer, expenses, fonts, badge, notesCm };
                double[] cycles = { 1380, 2100, 2820, 3540, 4260, 4980, 5700, 6420 };
                foreach (double c0 in cycles)
                {
                    Cm("AppIntentEval.log", c0 - 4, "AppIntentEval", 5740, 1, "Starting evaluation of " + apps.Length + " application deployments for machine");
                    double t = c0;
                    foreach (App a in apps)
                    {
                        string state = StateAt(a, c0);
                        bool bad = state == "Failed";
                    string resolved = state == "Installed" ? "Installed" : state == "Failed" ? "Failed" : "Available";
                        Cm("AppIntentEval.log", t, "AppIntentEval", 5740, bad ? 2 : 1, a.Id + ".3 :- Current State = " + state + ", Applicable = Applicable, ResolvedState = " + resolved + ", Wanted = Installed");
                        Cm("AppDiscovery.log", t + 0.2, "AppDiscovery", 5740, 1, "Entering ExecQueryAsync for " + a.Id + ".3");
                        Cm("AppDiscovery.log", t + 0.7, "AppDiscovery", 5740, 1, state == "Installed" ? "+++ Discovered application [AppDT Id: " + a.Id + ", Revision: 3]" : "+++ Application not discovered. [AppDT Id: " + a.Id + ", Revision: 3]");
                        Cm("AppDiscovery.log", t + 1.0, "AppDiscovery", 5740, 1, "Requirement check for " + a.Name + ": " + (a == badge ? "a user must be logged on" : "operating system and disk space are fine"));
                        Cm("AppDiscovery.log", t + 0.9, "AppDiscovery", 5740, 1, "Detection method for " + a.Name + ": " + (a == reporter || a == fonts ? "file version" : "MSI product code") + " check completed in " + (40 + rnd.Next(300)) + " ms");
                        t += 1.2 + Jitter(1.0);
                    }
                    Cm("AppIntentEval.log", t + 0.3, "AppIntentEval", 5740, 1, "Evaluation finished. " + apps.Length + " application deployments evaluated.");
                    Cm("CAS.log", c0 - 2, "ContentAccess", 2216, 1, "Cache check: " + (4 + rnd.Next(4)) + " content items in cache, 12.4 GB free of 16 GB");
                    for (int q = 0; q < 6; q++)
                    {
                        string[] cas =
                        {
                            "Content location refresh for " + apps[(q + 1) % apps.Length].Content + ".1 returned " + (2 + q % 2) + " distribution points",
                            "Checking the cache hash of " + apps[q % apps.Length].Content + ".1: matches",
                            "Distribution point dp0" + (1 + q % 2) + ".contoso.example answered in " + (14 + rnd.Next(60)) + " ms",
                            "Boundary group lookup for " + Computer + " returned BG-HQ-Wired (priority 1)",
                            "Peer source check: " + (q % 3) + " peers in the subnet have " + apps[q % apps.Length].Content + ".1",
                            "Cache cleanup skipped: no content is older than 7 days"
                        };
                        Cm("CAS.log", c0 + 20 + q * 6 + Jitter(3), "ContentAccess", 2216, 1, cas[q % cas.Length]);
                    }
                }
                Cm("AppDiscovery.log", 4010, "AppDiscovery", 5740, 2, "Detection for " + badge.Name + " took longer than expected (" + (2400 + rnd.Next(900)) + " ms)");
                Cm("AppIntentEval.log", 2850, "AppIntentEval", 5740, 2, badge.Id + ".3 :- The deployment is not applicable until the user logs on");
            }

            string StateAt(App a, double c)
            {
                if (a == fabrikam) return c > 1620 ? "Installed" : "NotInstalled";
                if (a == timesheet) return c > 5700 ? "Installed" : c > 2000 ? "Failed" : "NotInstalled";
                if (a == reporter) return c > 2420 ? "Installed" : "NotInstalled";
                if (a == printer) return c > 3150 ? "Failed" : "NotInstalled";
                if (a == expenses) return c > 3420 ? "Installed" : "NotInstalled";
                if (a == badge) return "NotInstalled";
                return "Installed";
            }

            void TaskSequence()
            {
                const string f = "smsts.log";
                double t = 0;
                string[] steps =
                {
                    "Set Windows Computer Name", "Apply Windows Settings", "Apply Network Settings", "Setup Windows and Configuration Manager",
                    "Install Updates Offline", "Install Applications", "Run Compliance Check", "Restart Computer", "Enable BitLocker"
                };
                Cm(f, t, "TSManager", 3724, 1, "Executing task sequence \"Contoso Standard Laptop 2026\" (package PS100020, deployment PS120004)");
                Cm(f, t += 0.4, "TSManager", 3724, 1, "Policy for the task sequence loaded: " + steps.Length + " steps in 3 groups");
                Cm(f, t += 0.3, "TSManager", 3724, 1, "Computer name: " + Computer);
                for (int i = 0; i < steps.Length; i++)
                {
                    double s0 = 20 + i * 150;
                    int th = 3724;
                    Cm(f, s0, "TSManager", th, 1, "Start executing the action \"" + steps[i] + "\" (" + (i + 1) + " of " + steps.Length + ")");
                    Cm(f, s0 + 0.2, "TSManager", th, 1, "Executing command line: " + CommandFor(steps[i]));
                    int lines = 40 + rnd.Next(20);
                    for (int k = 0; k < lines; k++)
                    {
                        int type = 1;
                        string msg = TsDetail(steps[i], k, ref type);
                        Cm(f, s0 + 1 + k * (110.0 / lines) + Jitter(2), i == 5 ? "InstallApplication" : "TSManager", th, type, msg);
                    }
                    if (steps[i] == "Install Updates Offline")
                    {
                        Cm(f, s0 + 60, "TSManager", th, 2, "Update KB5099001 is already superseded by a newer cumulative update. Skipping it. (0x80240017)");
                    }
                    if (steps[i] == "Run Compliance Check")
                    {
                        Cm(f, s0 + 70, "TSManager", th, 2, "Compliance check found 1 item to fix: Windows Firmware Update service is stopped. Continuing.");
                    }
                    if (steps[i] == "Enable BitLocker")
                    {
                        Cm(f, s0 + 50, "TSManager", th, 3, "Failed to enable the TPM protector on drive C: (0x80284008). The step is set to continue on error.");
                        Cm(f, s0 + 51, "TSManager", th, 1, "Continue on error is set. The task sequence goes on with the next step.");
                    }
                    Cm(f, s0 + 130, "TSManager", th, 1, "Successfully completed the action \"" + steps[i] + "\" with the exit code 0" + (steps[i] == "Enable BitLocker" ? " (continued after an error)" : ""));
                }
                Cm(f, 20 + steps.Length * 150, "TSManager", 3724, 1, "Task sequence completed 0x00000000");
                Cm(f, 21 + steps.Length * 150, "TSManager", 3724, 1, "Cleaning up the task sequence environment. Removing the temporary files in C:\\Windows\\CCM\\Logs\\SMSTSLog");
            }

            static string CommandFor(string step)
            {
                switch (step)
                {
                    case "Set Windows Computer Name": return "\"C:\\_SMSTaskSequence\\Packages\\PS100020\\SetName.exe\" /name " + Computer;
                    case "Install Applications": return "\"C:\\Windows\\CCM\\CcmExec.exe\" /install-apps Contoso-Base";
                    case "Restart Computer": return "shutdown.exe /r /t 5";
                    case "Enable BitLocker": return "\"C:\\Windows\\System32\\manage-bde.exe\" -on C: -used";
                    default: return "\"C:\\_SMSTaskSequence\\Packages\\PS100020\\Run.exe\" \"" + step + "\"";
                }
            }

            string TsDetail(string step, int k, ref int type)
            {
                string[] gen =
                {
                    "Setting the variable _SMSTSCurrentActionName to \"{0}\"",
                    "Checking the conditions of the step \"{0}\"",
                    "The condition evaluated to true",
                    "Resolved the working directory to C:\\_SMSTaskSequence\\WDPackage",
                    "Loading the step settings from the task sequence policy",
                    "Process completed with the exit code 0",
                    "Saving the task sequence variables to the local data store",
                    "Updating the progress to the management point: step {0}",
                    "Sent the status message to the management point",
                    "Checked in with the state store, nothing to do",
                    "Verified the signature of the step package",
                    "Cleaned up the temporary files of the step",
                    "Cached the content of the step package in C:\\_SMSTaskSequence\\Packages",
                    "Wrote the step result to the local state"
                };
                if (step == "Install Applications")
                {
                    string[] apps =
                    {
                        "Installing application \"Contoso Base Tools 1.0\" (model name ScopeId_8B1F6A2C/Application_0e1f2a3b)",
                        "Installing application \"Fabrikam Font Pack 1.8\" (model name ScopeId_8B1F6A2C/Application_ab6c7d8e)",
                        "Application \"Contoso Base Tools 1.0\" installed with the exit code 0",
                        "Waiting for the install of \"Fabrikam Font Pack 1.8\" to finish",
                        "Application \"Fabrikam Font Pack 1.8\" installed with the exit code 0",
                        "Requirement check for \"Contoso Timesheet 4.2\" failed: needs Contoso Runtime 8.1. Skipped by the step."
                    };
                    if (k < apps.Length)
                    {
                        if (k == 5) type = 2;
                        return apps[k];
                    }
                }
                return string.Format(CultureInfo.InvariantCulture, gen[(k + step.Length) % gen.Length], step) + (k % 3 == 0 ? " [" + (k + 1) + "]" : "");
            }

            // ---------- Intune ----------

            void IntuneSync()
            {
                const string f = "IntuneManagementExtension.log";
                const string c = "IntuneManagementExtension";
                double[] syncs = { 45, 1845, 3645, 5445 };
                int th = 7120;
                string userId = "11111111-2222-4333-8444-555555555555";
                for (int n = 0; n < syncs.Length; n++)
                {
                    double t = syncs[n];
                    Cm(f, t, c, th, 1, "[StatusService] Check-in started for device " + Computer + " (session " + (n + 1) + ")");
                    Cm(f, t += 0.6, c, th, 1, "[Win32App] ---- Start of policy check ----");
                    Cm(f, t += 0.8, c, th, 1, "[Win32App] Get policies = [{\"Id\":\"" + Guid(1) + "\",\"Name\":\"Woodgrove VPN Client 5.2\",\"Version\":2,\"Intent\":3},{\"Id\":\"" + Guid(2) + "\",\"Name\":\"Northwind Notes 2.1\",\"Version\":1,\"Intent\":3},{\"Id\":\"" + Guid(3) + "\",\"Name\":\"Contoso Edge Settings\",\"Version\":4,\"Intent\":3}]");
                    for (int k = 0; k < 50; k++)
                    {
                        string[] pool =
                        {
                            "[Win32App] Processing policy " + Guid(4) + " for user " + userId,
                            "[Win32App] Policy " + Guid(5) + " has not changed since the last check, revision " + (2 + k % 3),
                            "[StatusService] Reporting state for app " + Guid(6) + ": Installed",
                            "[Win32App][ReportingManager] Reporting state saved for " + (3 + k % 3) + " apps.",
                            "[Win32App] Detection result for app " + Guid(7) + " is Installed",
                            "[Win32App] Checking requirement rules for app " + Guid(8) + ": all rules are met",
                            "[HealthScripts] Next run of script " + Guid(9) + " is due in " + (20 + k * 3) + " minutes",
                            "[Win32App] Dependency graph for app " + Guid(10) + " has no dependencies",
                            "[PowerShell] Processing policy with id = " + Guid(11) + " for user " + userId,
                            "[PowerShell] Policy " + Guid(12) + " result Success is already reported"
                        };
                        Cm(f, t += 0.3 + Jitter(0.8), c, th, 1, pool[(k * 7 + n) % pool.Length]);
                    }
                    Cm(f, t += 1, c, th, 1, "[Win32App] ---- End of policy check, " + (3 + n % 2) + " apps and 3 scripts evaluated ----");
                    Cm(f, t += 0.4, c, th, 1, "[StatusService] Check-in finished, next check-in in 30 minutes");
                }
                for (int i = 0; i < 220; i++)
                {
                    double t = 120 + i * 32 + Jitter(12);
                    if (t > 7150) break;
                    string[] idle =
                    {
                        "[StatusService] Heartbeat to the Intune service succeeded (" + (90 + rnd.Next(80)) + " ms)",
                        "[Win32App] No pending app work. Sleeping for 60 seconds",
                        "[HealthScripts] No scripts are due. Next check in " + (5 + rnd.Next(25)) + " minutes",
                        "[Win32App] The user session of " + Computer + " is idle, user apps are not evaluated"
                    };
                    Cm(f, t, c, th, 1, idle[i % idle.Length]);
                }
                Cm(f, 1900, c, th, 2, "[Win32App] Policy " + Guid(13) + " could not be downloaded in 30 seconds. The next check-in will try again.");
                Cm(f, 5470, c, th, 2, "[StatusService] The local reporting store is " + (62 + rnd.Next(9)) + "% full. Old entries will be removed.");
            }

            void IntuneApps()
            {
                const string f = "AppWorkload.log";
                const string c = "AppWorkload";
                int th = 7120;
                string vpnId = "d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5f6";
                string notesId = "e2f3a4b5-c6d7-4e8f-9a01-b2c3d4e5f607";
                // Woodgrove VPN Client: download with one retry, install, detected.
                double t = 205;
                Cm(f, t, c, th, 1, "[Win32App][V3Processor] Processing app " + vpnId + " (Woodgrove VPN Client 5.2), intent Required");
                Cm(f, t += 0.4, c, th, 1, "[Win32App] Requirement rules for app " + vpnId + " are met");
                Cm(f, t += 0.3, c, th, 1, "[Win32App][DownloadManager] Download of app " + vpnId + " started, 84 MB, through Delivery Optimization");
                Cm(f, t += 6, c, th, 2, "[Win32App][DownloadManager] Delivery Optimization download timed out (0x80D02002). Attempt 1 of 3 failed. Retrying in 15 seconds.");
                Cm(f, t += 15, c, th, 1, "[Win32App][DownloadManager] Download of app " + vpnId + " restarted");
                for (int i = 1; i <= 8; i++)
                    Cm(f, t += 3.5 + Jitter(1.5), c, th, 1, "[Win32App][DownloadManager] Downloaded " + (i * 10 + i) + " MB of 84 MB (" + (int)(i * 12.5) + "%)");
                Cm(f, t += 2, c, th, 1, "[Win32App][DownloadManager] Download complete. Hash check of the content passed.");
                Cm(f, t += 0.6, c, th, 1, "[Win32App][Decryptor] Decrypting and unpacking the app content to C:\\Windows\\IMECache\\" + vpnId + "_2");
                Cm(f, t += 3, c, th, 1, "[Win32App][Installer] Running: \"C:\\Windows\\IMECache\\" + vpnId + "_2\\setup.exe\" /quiet /norestart");
                Cm(f, t += 22, c, th, 1, "[Win32App][Installer] Process exited with code 0. This code means success.");
                Cm(f, t += 0.6, c, th, 1, "[Win32App][Detection] Detection after install: the app is detected (file exists: C:\\Program Files\\Woodgrove\\VPN\\vpn.exe)");
                Cm(f, t += 0.4, c, th, 1, "[Win32App][ReportingManager] Reporting app " + vpnId + " as Installed");
                Cm("IntuneManagementExtension.log", t + 0.8, "IntuneManagementExtension", th, 1, "[StatusService] App " + vpnId + " reported as Installed.");

                // Northwind Notes: installs but is not detected afterwards.
                double n0 = 2105;
                Cm(f, n0, c, th, 1, "[Win32App][V3Processor] Processing app " + notesId + " (Northwind Notes 2.1), intent Required");
                Cm(f, n0 += 0.5, c, th, 1, "[Win32App] Requirement rules for app " + notesId + " are met");
                Cm(f, n0 += 0.3, c, th, 1, "[Win32App][DownloadManager] Download of app " + notesId + " started, 41 MB, through Delivery Optimization");
                for (int i = 1; i <= 5; i++)
                    Cm(f, n0 += 3 + Jitter(1), c, th, 1, "[Win32App][DownloadManager] Downloaded " + (i * 8) + " MB of 41 MB");
                Cm(f, n0 += 1.5, c, th, 1, "[Win32App][Installer] Running: msiexec /i \"NorthwindNotes21.msi\" /qn /norestart");
                Cm(f, n0 += 17, c, th, 1, "[Win32App][Installer] Process exited with code 0. This code means success.");
                Cm(f, n0 += 0.7, c, th, 3, "[Win32App][Detection] Detection after install failed. The app was not detected. Error 0x87D1041C (the application was not detected after installation completed successfully).");
                Cm(f, n0 += 0.3, c, th, 3, "[Win32App][ReportingManager] Reporting app " + notesId + " as Failed, error 0x87D1041C");
                Cm("IntuneManagementExtension.log", n0 + 0.5, "IntuneManagementExtension", th, 3, "[StatusService] App " + notesId + " reported as Failed (0x87D1041C). The next attempt is in 4 hours or at the next check-in with a changed policy.");
                Cm("AgentExecutor.log", n0 + 1.2, "AgentExecutor", 6012, 2, "Detection script for app " + notesId + " returned no output and exit code 0. Treating the app as not detected.");

                // Idle noise in AppWorkload between the two stories.
                string[] idle =
                {
                    "[Win32App][V3Processor] Processing of the app list finished, nothing to do",
                    "[Win32App] Delivery Optimization is idle. Peer cache is " + (120 + rnd.Next(80)) + " MB",
                    "[Win32App][ReportingManager] Reporting state saved for " + (3 + rnd.Next(3)) + " apps.",
                    "[Win32App] Checking the IMECache folder, " + (2 + rnd.Next(4)) + " old content folders can be removed",
                    "[Win32App][GRSManager] Evaluated the retry state of " + (1 + rnd.Next(3)) + " apps, none are due",
                    "[Win32App][V3Processor] Skipped app " + Guid(14) + ": the assignment has not changed since the last check",
                    "[Win32App][DownloadManager] No downloads are waiting. Bandwidth limit: none",
                    "[Win32App][Detection] Detection result for app " + Guid(18) + " is Installed (cached for " + (5 + rnd.Next(25)) + " minutes)",
                    "[Win32App] Requirement rules for app " + Guid(19) + " are met (cached)",
                    "[Win32App][ReportingManager] No state changes to report"
                };
                for (int i = 0; i < 900; i++)
                {
                    double ti = 10 + i * 8 + Jitter(5);
                    if (ti > 7150) break;
                    Cm(f, ti, c, th, 1, idle[(i * 5 + 1 + rnd.Next(2)) % idle.Length]);
                }
                // One longer retry story after the failure.
                double r0 = 6000;
                Cm(f, r0, c, th, 1, "[Win32App][GRSManager] App " + notesId + " retry is due. Attempt 2 of 3");
                Cm(f, r0 + 1, c, th, 1, "[Win32App][Installer] Running: msiexec /i \"NorthwindNotes21.msi\" /qn /norestart");
                Cm(f, r0 + 14, c, th, 1, "[Win32App][Installer] Process exited with code 0. This code means success.");
                Cm(f, r0 + 14.6, c, th, 1, "[Win32App][Detection] Detection after install: the app is detected (registry key: HKLM\\SOFTWARE\\Northwind\\Notes)");
                Cm(f, r0 + 15, c, th, 1, "[Win32App][ReportingManager] Reporting app " + notesId + " as Installed");
            }

            void Scripts()
            {
                const string f = "AgentExecutor.log";
                const string c = "AgentExecutor";
                int th = 6012;
                string s1 = Guid(15), s2 = Guid(16);
                double t = 410;
                Cm(f, t, c, th, 1, "Adding argument powershell with value C:\\Program Files (x86)\\Microsoft Intune Management Extension\\Policies\\Scripts\\" + s1 + ".ps1 to the named argument list.");
                Cm(f, t += 0.4, c, th, 1, "Running script Map-NetworkDrives.ps1 as the system account, execution policy Bypass");
                Cm(f, t += 3.2, c, th, 1, "Script output: Mapped drive H: to \\\\fs01.contoso.example\\home");
                Cm(f, t += 0.2, c, th, 1, "Script Map-NetworkDrives.ps1 finished with exit code 0 in 3.4 seconds");
                // A health script with remediation.
                t = 1915;
                Cm(f, t, c, th, 1, "[HS] ProcessScript PolicyId: " + s2 + " PolicyType: 6 (Remediation)");
                Cm(f, t += 0.5, c, th, 1, "[HS] Running the detection script Detect-DiskSpace.ps1");
                Cm(f, t += 2.1, c, th, 1, "[HS] std output = Free space on C: is 11 GB, the limit is 15 GB");
                Cm(f, t += 0.2, c, th, 2, "[HS] The detection script reported the device as not compliant (exit code 1). Starting the remediation script.");
                Cm(f, t += 0.8, c, th, 1, "[HS] Running the remediation script Clear-DiskSpace.ps1");
                Cm(f, t += 14, c, th, 1, "[HS] std output = Removed 6.2 GB of temporary files");
                Cm(f, t += 0.3, c, th, 1, "[HS] Remediation script finished with exit code 0");
                Cm(f, t += 2, c, th, 1, "[HS] Detection script ran again: compliant. Free space on C: is 17 GB");
                Cm("IntuneManagementExtension.log", t + 0.6, "IntuneManagementExtension", 7120, 1, "[HealthScripts] Result for policy " + s2 + " reported: remediated");
                // A script that fails.
                t = 3710;
                string s3 = Guid(17);
                Cm(f, t, c, th, 1, "Adding argument powershell with value C:\\Program Files (x86)\\Microsoft Intune Management Extension\\Policies\\Scripts\\" + s3 + ".ps1 to the named argument list.");
                Cm(f, t += 0.4, c, th, 1, "Running script Set-BitLockerPolicy.ps1 as the system account, execution policy Bypass");
                Cm(f, t += 2.5, c, th, 3, "Script error: Access is denied. (Exception from HRESULT: 0x80070005 (E_ACCESSDENIED))\r\nAt Set-BitLockerPolicy.ps1:14 char:5\r\n+     Set-ItemProperty -Path $key -Name RecoveryKey -Value 1\r\n+     ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                Cm(f, t += 0.3, c, th, 3, "Script Set-BitLockerPolicy.ps1 finished with exit code 1 in 2.8 seconds");
                Cm("IntuneManagementExtension.log", t + 0.7, "IntuneManagementExtension", 7120, 3, "[PowerShell] User Id = 00000000-0000-0000-0000-000000000000, Policy id = " + s3 + ", policy result = Failed (exit code 1)");
                Cm(f, t + 1.5, c, th, 2, "Will retry script Set-BitLockerPolicy.ps1 at the next check-in");
                for (int i = 0; i < 230; i++)
                {
                    double ti = 30 + i * 31 + Jitter(12);
                    if (ti > 7150) break;
                    string[] idle =
                    {
                        "Checked the script queue: no scripts are waiting",
                        "[HS] No remediation scripts are due in the next 10 minutes",
                        "The agent executor is idle and will exit in 60 seconds",
                        "Cleaned up the output of " + (1 + rnd.Next(3)) + " finished scripts"
                    };
                    Cm(f, ti, c, th, 1, idle[i % idle.Length]);
                }
            }

            // ---------- Windows servicing and the agent ----------

            void Servicing()
            {
                double t = 40;
                Cb(ref t, "Info", "CBS", "Starting TrustedInstaller initialization.");
                Cb(ref t, "Info", "CBS", "Loaded Servicing Stack v10.0.26100.1 with Core: C:\\Windows\\WinSxS\\amd64_microsoft-windows-servicingstack_31bf3856ad364e35_10.0.26100.1_none_a1b2c3d4e5f60718\\cbscore.dll");
                Cb(ref t, "Info", "CSI", "00000001@" + At(t).ToString("yyyy/MM/dd:HH:mm:ss.fff", CultureInfo.InvariantCulture) + " CSI Servicing Stack v10.0.26100.1 initialized");
                Cb(ref t, "Info", "CBS", "Ending TrustedInstaller initialization.");
                string session = "30881245_" + rnd.Next(100000000, 999999999);
                t = 2400;
                Cb(ref t, "Info", "CBS", "Starting the TrustedInstaller main loop.");
                Cb(ref t, "Info", "CBS", "Session: " + session + " initialized by client WindowsUpdateAgent.");
                Cb(ref t, "Info", "CBS", "Appl: detect Parent, Package: Package_for_KB5099001~31bf3856ad364e35~amd64~~26100.2400.1.3, Parent: Microsoft-Windows-Foundation-Package~31bf3856ad364e35~amd64~~10.0.26100.1, Disposition = Detect, VersionComp: Newer, ApplicableState: Staged, CurrentState: Staged");
                Cb(ref t, "Info", "CBS", "Exec: Processing package KB5099001 (2026-11 Cumulative Update for Windows 11 24H2).");
                for (int i = 0; i < 220; i++)
                {
                    string[] pool =
                    {
                        "Mark store corruption flag because of package: Package_" + i + "_for_KB5099001. [HRESULT = 0x00000000 - S_OK]",
                        "Appl: Applying component amd64_microsoft-windows-shell32_31bf3856ad364e35_10.0.26100." + (2400 + i) + "_none",
                        "Exec: Staging component " + (i + 1) + " of 220 for KB5099001",
                        "Verifying the signature of the manifest for component " + (i + 1),
                        "Plan: Resolved the dependencies of component " + (i + 1) + " with no conflicts",
                        "Exec: Committing the transaction step " + (i + 1) + " of 220"
                    };
                    t += 1.4 + Jitter(1.2);
                    Cb(ref t, i % 40 == 17 ? "Warning" : "Info", "CBS", i % 40 == 17 ? "Component " + (i + 1) + " is in use by another process. Pending until the next restart." : pool[i % pool.Length]);
                }
                t += 5;
                Cb(ref t, "Error", "CBS", "Failed to commit the package KB5099001 [HRESULT = 0x800f0922 - CBS_E_INSTALLERS_FAILED]");
                Cb(ref t, "Error", "CBS", "Failed to process single phase execution. [HRESULT = 0x800f0922 - CBS_E_INSTALLERS_FAILED]");
                Cb(ref t, "Info", "CBS", "Rolling back to the state before the package. Session: " + session);
                Cb(ref t, "Info", "CBS", "Rollback completed. The package KB5099001 stays staged.");
                Cb(ref t, "Info", "CBS", "Session: " + session + " finalized. Reboot required: no");
                t = 6090;
                string session2 = "30881311_" + rnd.Next(100000000, 999999999);
                Cb(ref t, "Info", "CBS", "Session: " + session2 + " initialized by client WindowsUpdateAgent.");
                Cb(ref t, "Info", "CBS", "Exec: Processing package KB5099001 (2026-11 Cumulative Update for Windows 11 24H2), second attempt.");
                for (int i = 0; i < 140; i++)
                {
                    t += 1.2 + Jitter(1.0);
                    Cb(ref t, "Info", "CBS", i % 3 == 0 ? "Exec: Staging component " + (i + 1) + " of 140 for KB5099001" : i % 3 == 1 ? "Appl: Applying component amd64_microsoft-windows-kernel_31bf3856ad364e35_10.0.26100." + (2400 + i) + "_none" : "Verifying the signature of the manifest for component " + (i + 1));
                }
                Cb(ref t, "Info", "CBS", "Exec: The package KB5099001 is installed. Restart required to finish.");
                Cb(ref t, "Info", "CBS", "Session: " + session2 + " finalized. Reboot required: yes");
            }

            void Cb(ref double t, string level, string comp, string msg)
            {
                Add("CBS.log", At(t), comp, 0, level == "Error" ? 3 : level == "Warning" ? 2 : 1, level + "\u0001" + msg);
            }

            void Agent()
            {
                double t = 12;
                int n = 0;
                while (t < 7180)
                {
                    int th = 14 + (n % 3);
                    Json(t, "Information", "Contoso.Agent.Heartbeat", th, "Heartbeat sent to the agent service (" + (28 + rnd.Next(60)) + " ms)");
                    if (n % 15 == 2) Json(t + 4, "Information", "Contoso.Agent.Policy", th, "Policy " + (100 + n / 15) + " is current. Next check at " + At(t + 600).ToString("HH:mm", CultureInfo.InvariantCulture));
                    if (n % 30 == 5)
                    {
                        Json(t + 8, "Information", "Contoso.Agent.Inventory", 21, "Inventory scan started");
                        Json(t + 21, "Information", "Contoso.Agent.Inventory", 21, "Inventory scan finished: " + (310 + rnd.Next(25)) + " items, " + (1 + rnd.Next(4)) + " changed");
                        Json(t + 22, "Information", "Contoso.Agent.Upload", 22, "Uploading inventory to https://agent.contoso.example/v2/inventory");
                        if (n == 35 || n == 125)
                        {
                            Json(t + 32, "Warning", "Contoso.Agent.Upload", 22, "Upload timed out after 10 s. Retrying in 30 s (attempt 1 of 3)");
                            Json(t + 64, "Error", "Contoso.Agent.Upload", 22, "Upload failed: 503 Service Unavailable from agent.contoso.example. Attempt 2 of 3");
                            Json(t + 96, "Information", "Contoso.Agent.Upload", 22, "Upload succeeded on attempt 3");
                        }
                        else Json(t + 24, "Information", "Contoso.Agent.Upload", 22, "Upload finished: 200 OK, " + (38 + rnd.Next(12)) + " KB");
                    }
                    if (n == 150) Json(t + 2, "Warning", "Contoso.Agent.Disk", th, "Free space on C: is 11 GB, below the 15 GB the agent asks for");
                    if (n == 151) Json(t + 2, "Information", "Contoso.Agent.Disk", th, "Free space on C: is 17 GB");
                    if (n == 330) Json(t + 3, "Error", "Contoso.Agent.Certificate", th, "The device certificate for agent.contoso.example expires in 6 days. Renewal failed: 0x80090016");
                    t += 14 + Jitter(3);
                    n++;
                }
            }

            void Json(double s, string level, string logger, int thread, string message)
            {
                DateTime t = At(s);
                Entry e = new Entry();
                e.T = t;
                e.Seq = seq++;
                e.Comp = logger;
                e.Thread = thread;
                e.Msg = level + "\u0001" + message;
                jsonEntries.Add(e);
            }

            // ---------- writing ----------

            static string Bias(DateTime t)
            {
                int offset = (int)TimeZoneInfo.Local.GetUtcOffset(t).TotalMinutes;
                int b = -offset;
                return (b < 0 ? "-" : "+") + Math.Abs(b).ToString("000", CultureInfo.InvariantCulture);
            }

            static List<Entry> Sorted(List<Entry> l)
            {
                List<Entry> c = new List<Entry>(l);
                c.Sort(delegate (Entry a, Entry b)
                {
                    int r = a.T.CompareTo(b.T);
                    return r != 0 ? r : a.Seq.CompareTo(b.Seq);
                });
                return c;
            }

            public string SaveCm(string folder, string name)
            {
                string path = Path.Combine(folder, name);
                StringBuilder sb = new StringBuilder();
                List<Entry> l;
                if (!files.TryGetValue(name, out l)) l = new List<Entry>();
                foreach (Entry e in Sorted(l))
                {
                    sb.Append("<![LOG[").Append(e.Msg).Append("]LOG]!><time=\"")
                      .Append(e.T.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(Bias(e.T))
                      .Append("\" date=\"").Append(e.T.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture))
                      .Append("\" component=\"").Append(e.Comp).Append("\" context=\"\" type=\"").Append(e.Type)
                      .Append("\" thread=\"").Append(e.Thread).Append("\" file=\"\">\r\n");
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return path;
            }

            public string SaveCbs(string folder)
            {
                string path = Path.Combine(folder, CbsFile);
                StringBuilder sb = new StringBuilder();
                List<Entry> l;
                if (!files.TryGetValue(CbsFile, out l)) l = new List<Entry>();
                foreach (Entry e in Sorted(l))
                {
                    int cut = e.Msg.IndexOf('\u0001');
                    string level = e.Msg.Substring(0, cut);
                    sb.Append(e.T.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(", ")
                      .Append(level.PadRight(20)).Append(e.Comp.PadRight(7)).Append(e.Msg.Substring(cut + 1)).Append("\r\n");
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return path;
            }

            public string SaveJson(string folder)
            {
                string path = Path.Combine(folder, JsonFile);
                StringBuilder sb = new StringBuilder();
                foreach (Entry e in Sorted(jsonEntries))
                {
                    int cut = e.Msg.IndexOf('\u0001');
                    TimeSpan o = TimeZoneInfo.Local.GetUtcOffset(e.T);
                    string zone = (o < TimeSpan.Zero ? "-" : "+") + Math.Abs(o.Hours).ToString("00", CultureInfo.InvariantCulture) + ":" + Math.Abs(o.Minutes).ToString("00", CultureInfo.InvariantCulture);
                    sb.Append("{\"timestamp\":\"").Append(e.T.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(zone)
                      .Append("\",\"level\":\"").Append(e.Msg.Substring(0, cut))
                      .Append("\",\"logger\":\"").Append(e.Comp)
                      .Append("\",\"thread\":").Append(e.Thread)
                      .Append(",\"host\":\"").Append(Computer)
                      .Append("\",\"message\":\"").Append(Esc(e.Msg.Substring(cut + 1))).Append("\"}\n");
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return path;
            }

            static string Esc(string s)
            {
                StringBuilder sb = new StringBuilder();
                foreach (char c in s)
                {
                    if (c == '"') sb.Append("\\\"");
                    else if (c == '\\') sb.Append("\\\\");
                    else if (c == '\n') sb.Append("\\n");
                    else if (c == '\r') sb.Append("\\r");
                    else if (c == '\t') sb.Append("\\t");
                    else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                }
                return sb.ToString();
            }
        }
    }
}
