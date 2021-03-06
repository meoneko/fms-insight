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
using System.Collections.Generic;
using System.Runtime.Serialization;
using BlackMaple.MachineWatchInterface;
using BlackMaple.MachineFramework;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DebugMachineWatchApiServer
{
  public static class DebugMockProgram
  {
    public static void Main()
    {
      System.Environment.SetEnvironmentVariable("FMS__InstructionFilePath",
      System.IO.Path.Combine(
          System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
          "../../../sample-instructions/"
      ));
      System.Environment.SetEnvironmentVariable("FMS__QuarantineQueue", "Initial Quarantine");
      BlackMaple.MachineFramework.Program.Run(false, (cfg, st) =>
      {
        var backend = new MockServerBackend();
        return new FMSImplementation()
        {
          Backend = backend,
          Name = "mock",
          Version = "1.2.3.4"
        };

      });
    }
  }

  public class MockServerBackend : IFMSBackend, IJobControl, IOldJobDecrement, IDisposable
  {
    public JobLogDB LogDB { get; private set; }
    public JobDB JobDB { get; private set; }

    private Dictionary<string, CurrentStatus> Statuses { get; } = new Dictionary<string, CurrentStatus>();
    private CurrentStatus CurrentStatus { get; set; }

    private JsonSerializerSettings _jsonSettings;

    public event NewCurrentStatus OnNewCurrentStatus;

    public MockServerBackend()
    {
      string path = null; // dataDir

      string dbFile(string f) => System.IO.Path.Combine(path, f + ".db");

      if (path != null)
      {
        if (System.IO.File.Exists(dbFile("log"))) System.IO.File.Delete(dbFile("log"));
        LogDB = new JobLogDB(new FMSSettings());
        LogDB.Open(dbFile("log"), dbFile("insp"));

        if (System.IO.File.Exists(dbFile("job"))) System.IO.File.Delete(dbFile("job"));
        JobDB = new JobDB();
        JobDB.Open(dbFile("job"));
      }
      else
      {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        LogDB = new JobLogDB(new FMSSettings(), conn);
        LogDB.CreateTables(firstSerialOnEmpty: null);

        conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        JobDB = new JobDB(conn);
        JobDB.CreateTables();
      }

      _jsonSettings = new JsonSerializerSettings();
      _jsonSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
      _jsonSettings.Converters.Add(new BlackMaple.MachineFramework.TimespanConverter());
      _jsonSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
      _jsonSettings.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;

      var sampleDataPath = System.IO.Path.Combine(
          System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
          "../../../sample-data/"
      );

      // sample data starts at Jan 1, 2018.  Need to offset to current month
      var jan1_18 = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      var offset = DateTime.UtcNow.AddDays(-28).Subtract(jan1_18);

      LoadEvents(sampleDataPath, offset);
      LoadJobs(sampleDataPath, offset);
      LoadStatus(sampleDataPath, offset);
    }

    public void Dispose()
    {
      JobDB.Close();
      LogDB.Close();
    }

    public IInspectionControl InspectionControl()
    {
      return LogDB;
    }

    public IJobControl JobControl()
    {
      return this;
    }

    public ILogDatabase LogDatabase()
    {
      return LogDB;
    }

    public IJobDatabase JobDatabase()
    {
      return JobDB;
    }

    private long _curStatusLoadCount = 0;
    public CurrentStatus GetCurrentStatus()
    {
      _curStatusLoadCount += 1;
      if (_curStatusLoadCount % 5 == 0)
      {
        if (CurrentStatus.Alarms.Count > 0)
        {
          CurrentStatus.Alarms.Clear();
        }
        else
        {
          CurrentStatus.Alarms.Add("Test alarm " + _curStatusLoadCount.ToString());
          CurrentStatus.Alarms.Add("Another alarm");
        }
      }

      return CurrentStatus;
    }

    public List<string> CheckValidRoutes(IEnumerable<JobPlan> newJobs)
    {
      return new List<string>();
    }

    public void AddJobs(NewJobs jobs, string expectedPreviousScheduleId)
    {
      JobDB.AddJobs(jobs, expectedPreviousScheduleId);
    }

    public void AddUnallocatedCastingToQueue(string part, string queue, int position, string serial)
    {
      Serilog.Log.Information("AddUnallocatedCastingToQueue: {part} {queue} {position} {serial}", part, queue, position, serial);
    }

    public void AddUnprocessedMaterialToQueue(string jobUnique, int lastCompletedProcess, string queue, int position, string serial)
    {
      Serilog.Log.Information("AddUnprocessedMaterialToQueue: {unique} {lastCompProcess} {queue} {position} {serial}",
        jobUnique, lastCompletedProcess, queue, position, serial);
    }
    public void SetMaterialInQueue(long materialId, string queue, int position)
    {
      Serilog.Log.Information("SetMaterialInQueue {matId} {queue} {position}", materialId, queue, position);

      var toMove = CurrentStatus.Material.FirstOrDefault(m => m.MaterialID == materialId && m.Location.Type == InProcessMaterialLocation.LocType.InQueue);
      if (toMove == null) return;

      // shift old downward
      foreach (var m in CurrentStatus.Material)
      {
        if (m.Location.Type == InProcessMaterialLocation.LocType.InQueue
            && m.Location.CurrentQueue == toMove.Location.CurrentQueue
            && m.Location.QueuePosition > toMove.Location.QueuePosition)
        {
          m.Location.QueuePosition -= 1;
        }
      }

      // shift new upward
      foreach (var m in CurrentStatus.Material)
      {
        if (m.Location.Type == InProcessMaterialLocation.LocType.InQueue
            && m.Location.CurrentQueue == queue
            && m.Location.QueuePosition >= position)
        {
          m.Location.QueuePosition += 1;
        }
      }

      toMove.Location = new InProcessMaterialLocation()
      {
        Type = InProcessMaterialLocation.LocType.InQueue,
        CurrentQueue = queue,
        QueuePosition = position
      };

      OnNewStatus(CurrentStatus);
    }
    public void RemoveMaterialFromAllQueues(long materialId)
    {
      Serilog.Log.Information("RemoveMaterialFromAllQueues {matId}", materialId);

      var toRemove = CurrentStatus.Material.FirstOrDefault(m => m.MaterialID == materialId && m.Location.Type == InProcessMaterialLocation.LocType.InQueue);
      if (toRemove == null) return;

      // shift downward
      foreach (var m in CurrentStatus.Material)
      {
        if (m.Location.Type == InProcessMaterialLocation.LocType.InQueue
            && m.Location.CurrentQueue == toRemove.Location.CurrentQueue
            && m.Location.QueuePosition < toRemove.Location.QueuePosition)
        {
          m.Location.QueuePosition -= 1;
        }
      }

      CurrentStatus.Material.Remove(toRemove);

      OnNewStatus(CurrentStatus);
    }

    public List<JobAndDecrementQuantity> DecrementJobQuantites(long loadDecrementsStrictlyAfterDecrementId)
    {
      throw new NotImplementedException();
    }

    public List<JobAndDecrementQuantity> DecrementJobQuantites(DateTime loadDecrementsAfterTimeUTC)
    {
      throw new NotImplementedException();
    }

    public IOldJobDecrement OldJobDecrement()
    {
      return this;
    }

    protected void OnNewStatus(CurrentStatus s)
    {
      OnNewCurrentStatus?.Invoke(s);
    }

    public Dictionary<JobAndPath, int> OldDecrementJobQuantites()
    {
      throw new NotImplementedException();
    }

    public void OldFinalizeDecrement()
    {
      throw new NotImplementedException();
    }

    private void LoadEvents(string sampleDataPath, TimeSpan offset)
    {
      var files = System.IO.Directory.GetFiles(sampleDataPath, "events-*.json");
      var evts = new List<BlackMaple.MachineWatchInterface.LogEntry>();
      foreach (var f in files)
      {
        using (var file = System.IO.File.OpenRead(f))
        {
          var reader = new System.IO.StreamReader(file);
          while (reader.Peek() >= 0)
          {
            var evtJson = reader.ReadLine();
            var e = (BlackMaple.MachineWatchInterface.LogEntry)JsonConvert.DeserializeObject(
              evtJson,
              typeof(BlackMaple.MachineWatchInterface.LogEntry),
              _jsonSettings
            );
            evts.Add(e);
          }
        }
      }

      foreach (var e in evts.OrderBy(e => e.EndTimeUTC))
      {
        foreach (var m in e.Material)
        {
          var matDetails = LogDB.GetMaterialDetails(m.MaterialID);
          if (matDetails == null && !string.IsNullOrEmpty(m.JobUniqueStr))
          {
            LogDB.CreateMaterialID(m.MaterialID, m.JobUniqueStr, m.PartName, m.NumProcesses);
          }
        }
        if (e.LogType == LogType.PartMark)
        {
          foreach (var m in e.Material)
            LogDB.RecordSerialForMaterialID(JobLogDB.EventLogMaterial.FromLogMat(m), e.Result, e.EndTimeUTC.Add(offset));
        }
        else if (e.LogType == LogType.OrderAssignment)
        {
          foreach (var m in e.Material)
            LogDB.RecordWorkorderForMaterialID(JobLogDB.EventLogMaterial.FromLogMat(m), e.Result, e.EndTimeUTC.Add(offset));
        }
        else if (e.LogType == LogType.FinalizeWorkorder)
        {
          LogDB.RecordFinalizedWorkorder(e.Result, e.EndTimeUTC.Add(offset));
        }
        else
        {
          if (e.LogType == LogType.InspectionResult && e.Material.Any(m => m.MaterialID == 2965))
          {
            // ignore inspection complete
            continue;
          }
          var e2 = new BlackMaple.MachineWatchInterface.LogEntry(
              cntr: e.Counter,
              mat: e.Material,
              pal: e.Pallet,
              ty: e.LogType,
              locName: e.LocationName,
              locNum: e.LocationNum,
              prog: e.Program,
              start: e.StartOfCycle,
              endTime: e.EndTimeUTC.Add(offset),
              result: e.Result,
              endOfRoute: e.EndOfRoute,
              elapsed: e.ElapsedTime,
              active: e.ActiveOperationTime
          );
          if (e.ProgramDetails != null)
          {
            foreach (var x in e.ProgramDetails)
              e2.ProgramDetails.Add(x.Key, x.Value);
          }
          if (e.Tools != null)
          {
            foreach (var x in e.Tools)
              e2.Tools[x.Key] = x.Value;
          }
          LogDB.AddLogEntryFromUnitTest(e2);
        }
      }
    }

    private void LoadJobs(string sampleDataPath, TimeSpan offset)
    {
      var newJobsJson = System.IO.File.ReadAllText(
        System.IO.Path.Combine(sampleDataPath, "newjobs.json"));
      var allNewJobs = (List<BlackMaple.MachineWatchInterface.NewJobs>)JsonConvert.DeserializeObject(
        newJobsJson,
        typeof(List<BlackMaple.MachineWatchInterface.NewJobs>),
        _jsonSettings
      );

      foreach (var newJobs in allNewJobs)
      {
        foreach (var j in newJobs.Jobs)
        {
          OffsetJob(j, offset);
        }
        foreach (var su in newJobs.StationUse)
        {
          su.StartUTC = su.StartUTC.Add(offset);
          su.EndUTC = su.EndUTC.Add(offset);
        }
        foreach (var w in newJobs.CurrentUnfilledWorkorders)
        {
          w.DueDate = w.DueDate.Add(offset);
        }

        JobDB.AddJobs(newJobs, null);
      }
    }

    private void LoadStatus(string sampleDataPath, TimeSpan offset)
    {
      var files = System.IO.Directory.GetFiles(sampleDataPath, "status-*.json");
      foreach (var f in files)
      {
        var name = System.IO.Path.GetFileNameWithoutExtension(f).Replace("status-", "");

        var statusJson = System.IO.File.ReadAllText(f);
        var curSt = (BlackMaple.MachineWatchInterface.CurrentStatus)JsonConvert.DeserializeObject(
          statusJson,
          typeof(BlackMaple.MachineWatchInterface.CurrentStatus),
          _jsonSettings
        );
        curSt.TimeOfCurrentStatusUTC = curSt.TimeOfCurrentStatusUTC.Add(offset);

        foreach (var uniq in curSt.Jobs.Keys)
        {
          MockServerBackend.OffsetJob(curSt.Jobs[uniq], offset);
        }
        Statuses.Add(name, curSt);
      }

      string statusFromEnv = System.Environment.GetEnvironmentVariable("BMS_CURRENT_STATUS");
      if (string.IsNullOrEmpty(statusFromEnv) || !Statuses.ContainsKey(statusFromEnv))
      {
        CurrentStatus = Statuses.OrderBy(st => st.Key).First().Value;
      }
      else
      {
        CurrentStatus = Statuses[statusFromEnv];
      }
    }

    public static void OffsetJob(JobPlan j, TimeSpan offset)
    {
      j.RouteStartingTimeUTC = j.RouteStartingTimeUTC.Add(offset);
      j.RouteEndingTimeUTC = j.RouteEndingTimeUTC.Add(offset);
      for (int proc = 1; proc <= j.NumProcesses; proc++)
      {
        for (int path = 1; path <= j.GetNumPaths(proc); path++)
        {
          j.SetSimulatedStartingTimeUTC(proc, path,
              j.GetSimulatedStartingTimeUTC(proc, path).Add(offset)
          );
          var prod = new List<JobPlan.SimulatedProduction>();
          foreach (var p in j.GetSimulatedProduction(proc, path))
          {
            prod.Add(new JobPlan.SimulatedProduction()
            {
              TimeUTC = p.TimeUTC.Add(offset),
              Quantity = p.Quantity,
            });
          }
        }
      }
      // not converted: hold patterns
    }
  }
}