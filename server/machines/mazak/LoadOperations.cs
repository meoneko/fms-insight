/* Copyright (c) 2018, John Lenz

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of John Lenz, Black Maple Software, SeedTactics,
      nor the names of other contributors may be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace MazakMachineInterface
{

  public class LoadAction
  {
    public readonly int LoadStation;
    public readonly bool LoadEvent;
    public readonly string Unique;
    public readonly string Part;
    public readonly int Process;
    public readonly int Path;
    public readonly int Qty;

    public LoadAction(bool l, int stat, string p, string comment, int proc, int q)
    {
      LoadStation = stat;
      LoadEvent = l;
      Part = p;
      Process = proc;
      Qty = q;
      bool manual;
      MazakPart.ParseComment(comment, out Unique, out Path, out manual);
    }

    public override string ToString()
    {
      return (LoadEvent ? "Load " : "Unload ") +
        Part + "-" + Process.ToString() + " qty: " + Qty.ToString();
    }
  }

  public class LoadOperations
  {
    public delegate void LoadActionsDel(int lds, IEnumerable<LoadAction> actions);
    public event LoadActionsDel LoadActions;
    private string mazakPath;

    public LoadOperations(TraceSource t, BlackMaple.MachineFramework.IConfig cfg)
    {
      trace = t;
      mazakPath = cfg.GetValue<string>("Mazak", "Load CSV Path");
      if (string.IsNullOrEmpty(mazakPath))
      {
#if DEBUG
        mazakPath = @"\\172.16.11.14\mazak\FMS\LDS";
#else
                mazakPath = "c:\\mazak\\FMS\\LDS\\";
#endif
      }

      if (!Directory.Exists(mazakPath))
      {
        mazakPath = "c:\\mazak\\LDS\\";

        if (!Directory.Exists(mazakPath))
        {
          trace.TraceEvent(TraceEventType.Error, 0,
                           "Unable to determine the path to the mazak load CSV files.  Please add/update a setting" +
                           " called 'Mazak Load CSV Path' in MachineWatch.exe.config to point to the correct directory");
          return;
        }
      }

      _watcher = new FileSystemWatcher(mazakPath, "*.csv");
      //_watcher.Created += watcher_Changed;
      _watcher.Changed += watcher_Changed;
      _watcher.EnableRaisingEvents = true;
    }

    public void Halt()
    {
      if (_watcher == null)
        return;
      _watcher.EnableRaisingEvents = false;
      //_watcher.Created -= watcher_Changed;
      _watcher.Changed -= watcher_Changed;
      _watcher = null;
    }


    private FileSystemWatcher _watcher;
    private object _lock = new object();
    private IDictionary<int, DateTime> lastWriteTime = new Dictionary<int, DateTime>();
    private TraceSource trace;


    private void watcher_Changed(object sender, FileSystemEventArgs e)
    {
      try
      {
        string file = e.FullPath;

        Match m = Regex.Match(Path.GetFileName(file).ToLower(),
                              "lds([0-9]*)_operation.*csv");

        if (!m.Success || m.Groups.Count < 2)
          return;

        int lds = int.Parse(m.Groups[1].Value);
        List<LoadAction> a = null;

        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));

        lock (_lock)
        {
          //it might no longer exist if the event fires multiple times for this file
          if (File.Exists(file))
          {

            var last = System.IO.File.GetLastWriteTime(file);

            if (lastWriteTime.ContainsKey(lds) && lastWriteTime[lds] == last)
            {
              trace.TraceEvent(TraceEventType.Information, 0,
                               "Skipping load " + lds.ToString() +
                               " file " + Path.GetFileName(file) + " because the file" +
                               " has not been modified.");
            }
            else
            {

              trace.TraceEvent(TraceEventType.Information, 0,
                               "Starting to process load station " + lds.ToString() +
                               " file " + Path.GetFileName(file));

              a = ReadFile(lds, file);

              lastWriteTime[lds] = last;
            }
          }
        }

        if (a == null || a.Count == 0)
          return;

        trace.TraceEvent(TraceEventType.Information, 0, a.ToString());

        if (LoadActions != null && a != null)
          LoadActions(lds, a);

      }
      catch (Exception ex)
      {
        trace.TraceEvent(TraceEventType.Error, 0,
                         "Unhandled error when reading mazak instructions" + Environment.NewLine + ex.ToString());
      }
    }

    public IEnumerable<LoadAction> CurrentLoadActions()
    {
      if (!Directory.Exists(mazakPath))
        return new List<LoadAction>();

      var ret = new List<LoadAction>();

      lock (_lock)
      {
        foreach (var f in Directory.GetFiles(mazakPath, "*.csv"))
        {
          Match m = Regex.Match(Path.GetFileName(f).ToLower(),
                              "lds([0-9]*)_operation.*csv");
          if (!m.Success || m.Groups.Count < 2)
            continue;
          int lds = int.Parse(m.Groups[1].Value);

          if (File.Exists(f))
            ret.AddRange(ReadFile(lds, f));
        }
      }

      return ret;
    }


    private List<LoadAction> ReadFile(int stat, string fName)
    {
      var ret = new List<LoadAction>();

      using (StreamReader f = File.OpenText(fName))
      {
        while (f.Peek() >= 0)
        {
          string[] split = f.ReadLine().Split(',');

          if (split.Length > 8 && !string.IsNullOrEmpty(split[1]))
          {
            string part = split[1];
            string comment = split[2];
            int idx = part.IndexOf(':');
            if (idx >= 0)
            {
              part = part.Substring(0, idx);
            }
            int proc = int.Parse(split[3]);
            int qty = int.Parse(split[5]);

            if (!string.IsNullOrEmpty(part))
            {

              bool load = false;
              if (split[0].StartsWith("FIX"))
                load = true;
              if (split[0].StartsWith("REM"))
                load = false;

              ret.Add(new LoadAction(load, stat, part, comment, proc, qty));
            }
          }
        }
      }

      return ret;
    }
  }
}
