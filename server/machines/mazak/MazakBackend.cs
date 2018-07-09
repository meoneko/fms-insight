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
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using BlackMaple.MachineWatchInterface;
using BlackMaple.MachineFramework;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BlackMaple.MachineFramework.Tests")]

namespace MazakMachineInterface
{
  public class MazakBackend : IFMSBackend, IFMSImplementation
  {
    private TransactionDatabaseAccess database;
    private RoutingInfo routing;
    private HoldPattern hold;
    private MazakQueues queues;
    private LoadOperations loadOper;
    private ILogEvents logDataLoader;

    private JobLogDB jobLog;
    private JobDB jobDB;

    //Settings
    public DatabaseAccess.MazakDbType MazakType;
    public bool UseStartingOffsetForDueDate;
    public bool DecrementPriorityOnDownload;
    public bool CheckPalletsUsedOnce;

    public TransactionDatabaseAccess Database
    {
      get
      {
        return database;
      }
    }

    public LoadOperations LoadOperations
    {
      get { return loadOper; }
    }

    public JobLogDB JobLog
    {
      get { return jobLog; }
    }

    public ILogEvents LogTranslation
    {
      get { return logDataLoader; }
    }

    FMSInfo IFMSImplementation.Info => new FMSInfo()
    {
      Name = "Mazak",
      Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()
    };
    IFMSBackend IFMSImplementation.Backend => this;
    IList<IBackgroundWorker> IFMSImplementation.Workers => new List<IBackgroundWorker>();

    public void Init(string dataDirectory, IConfig cfg, FMSSettings settings)
    {
      string localDbPath = cfg.GetValue<string>("Mazak", "Database Path");
      MazakType = DetectMazakType(cfg, localDbPath);

      // database settings
      string sqlConnectString = cfg.GetValue<string>("Mazak", "SQL ConnectionString");
      string dbConnStr;
      if (MazakType == DatabaseAccess.MazakDbType.MazakSmooth)
      {
        if (!string.IsNullOrEmpty(sqlConnectString))
        {
          dbConnStr = sqlConnectString;
        }
        else if (!string.IsNullOrEmpty(localDbPath))
        {
          // old installers put sql server computer name in localDbPath
          dbConnStr = "Server=" + localDbPath + "\\pmcsqlserver;" +
              "User ID=mazakpmc;Password=Fms-978";
        }
        else
        {
          var b = new System.Data.SqlClient.SqlConnectionStringBuilder();
          b.UserID = "mazakpmc";
          b.Password = "Fms-978";
          b.DataSource = "(local)";
          dbConnStr = b.ConnectionString;
        }
      }
      else
      {
        dbConnStr = localDbPath;
        if (string.IsNullOrEmpty(dbConnStr))
        {
          dbConnStr = "c:\\Mazak\\NFMS\\DB";
        }
      }

      // log csv
      string logPath = cfg.GetValue<string>("Mazak", "Log CSV Path");
      if (logPath == null || logPath == "")
        logPath = "c:\\Mazak\\FMS\\Log";

      // general config
      string useStartingForDue = cfg.GetValue<string>("Mazak", "Use Starting Offset For Due Date");
      string useStarting = cfg.GetValue<string>("Mazak", "Use Starting Offset");
      string decrPriority = cfg.GetValue<string>("Mazak", "Decrement Priority On Download");
      if (string.IsNullOrEmpty(useStarting))
      {
        //useStarting is an old setting, so if it is missing use the new settings
        if (string.IsNullOrEmpty(useStartingForDue))
        {
          UseStartingOffsetForDueDate = true;
        }
        else
        {
          UseStartingOffsetForDueDate = Convert.ToBoolean(useStartingForDue);
        }
        if (string.IsNullOrEmpty(decrPriority))
        {
          DecrementPriorityOnDownload = true;
        }
        else
        {
          DecrementPriorityOnDownload = Convert.ToBoolean(decrPriority);
        }
      }
      else
      {
        UseStartingOffsetForDueDate = Convert.ToBoolean(useStarting);
        DecrementPriorityOnDownload = UseStartingOffsetForDueDate;
      }
      //Perhaps this should be a new setting, but if you don't check for pallets used once
      //then you don't care if all faces on a pallet are full so might as well use priority
      //which causes pallet positions to go empty.
      CheckPalletsUsedOnce = !UseStartingOffsetForDueDate && !DecrementPriorityOnDownload;


      // serial settings
      string serialPerMaterial = cfg.GetValue<string>("Mazak", "Assign Serial Per Material");
      if (!string.IsNullOrEmpty(serialPerMaterial))
      {
        bool result;
        if (bool.TryParse(serialPerMaterial, out result))
        {
          if (!result)
            settings.SerialType = SerialType.AssignOneSerialPerCycle;
        }
      }

      if (!System.IO.Directory.Exists(logPath))
        logTrace.TraceEvent(TraceEventType.Error, 0, "Configured Log CSV directory " + logPath + " does not exist.");

      logTrace.TraceData(TraceEventType.Warning, 0,
          "Configured UseStartingOffsetForDueDate = " + UseStartingOffsetForDueDate.ToString() + " and " +
          "DecrementPriorityOnDownload = " + DecrementPriorityOnDownload.ToString());

      jobLog = new BlackMaple.MachineFramework.JobLogDB();
      jobLog.Open(
        System.IO.Path.Combine(dataDirectory, "log.db"),
        System.IO.Path.Combine(dataDirectory, "insp.db")
      );

      jobDB = new BlackMaple.MachineFramework.JobDB();
      var jobInspName = System.IO.Path.Combine(dataDirectory, "jobinspection.db");
      if (System.IO.File.Exists(jobInspName))
        jobDB.Open(jobInspName);
      else
        jobDB.Open(System.IO.Path.Combine(dataDirectory, "mazakjobs.db"));

      database = new TransactionDatabaseAccess(dbConnStr, MazakType);
      var readOnlyDb = new ReadonlyDatabaseAccess(dbConnStr, MazakType);
      if (MazakType == DatabaseAccess.MazakDbType.MazakWeb || MazakType == DatabaseAccess.MazakDbType.MazakSmooth)
        logDataLoader = new LogDataWeb(logPath, jobLog, jobDB, readOnlyDb, settings);
      else
      {
#if USE_OLEDB
				logDataLoader = new LogDataVerE(jobLog, jobDB, readOnlyDb, settings);
#else
        throw new Exception("Mazak Web and VerE are not supported on .NET core");
#endif
      }
      hold = new HoldPattern(dataDirectory, database, readOnlyDb, holdTrace, true);
      loadOper = new LoadOperations(loadOperTrace, cfg, readOnlyDb.SmoothDB);
      queues = new MazakQueues(jobLog, jobDB, loadOper, readOnlyDb, database);
      routing = new RoutingInfo(database,readOnlyDb, hold, queues, jobDB, jobLog, loadOper,
                                CheckPalletsUsedOnce, UseStartingOffsetForDueDate, DecrementPriorityOnDownload,
                                settings,
                                routingTrace);

      logDataLoader.NewEntries += OnNewLogEntries;
      loadOper.LoadActions += OnLoadActions;
    }

    public void Halt()
    {
      logDataLoader.NewEntries -= OnNewLogEntries;
      loadOper.LoadActions -= OnLoadActions;
      routing.Halt();
      hold.Shutdown();
      queues.Shutdown();
      logDataLoader.Halt();
      loadOper.Halt();
      jobDB.Close();
      jobLog.Close();
    }

    private TraceSource routingTrace = new TraceSource("Mazak", SourceLevels.All);
    private TraceSource holdTrace = new TraceSource("Hold Transitions", SourceLevels.All);
    private TraceSource loadOperTrace = new TraceSource("Load Operations", SourceLevels.All);
    private TraceSource logTrace = new TraceSource("Log", SourceLevels.All);

    public IEnumerable<System.Diagnostics.TraceSource> TraceSources()
    {
      return new TraceSource[] { routingTrace, holdTrace, loadOperTrace, logTrace };
    }

    public IInspectionControl InspectionControl()
    {
      return jobLog;
    }

    public IJobControl JobControl()
    {
      return routing;
    }

    public IOldJobDecrement OldJobDecrement()
    {
      return routing;
    }

    public IJobDatabase JobDatabase()
    {
      return jobDB;
    }

    public ILogDatabase LogDatabase()
    {
      return jobLog;
    }

    private void OnLoadActions(int lds, IEnumerable<LoadAction> actions)
    {
      routing.RaiseNewCurrentStatus(routing.GetCurrentStatus());
    }

    private void OnNewLogEntries(ReadOnlyDataSet dset)
    {
      routing.RaiseNewCurrentStatus(routing.GetCurrentStatus(dset));
    }

    private DatabaseAccess.MazakDbType DetectMazakType(IConfig cfg, string localDbPath)
    {
      var verE = cfg.GetValue<bool>("Mazak", "VersionE");
      var webver = cfg.GetValue<bool>("Mazak", "Web Version");
      var smoothVer = cfg.GetValue<bool>("Mazak", "Smooth Version");

      if (verE)
        return DatabaseAccess.MazakDbType.MazakVersionE;
      else if (webver)
        return DatabaseAccess.MazakDbType.MazakWeb;
      else if (smoothVer)
        return DatabaseAccess.MazakDbType.MazakSmooth;

      string testPath;
      if (string.IsNullOrEmpty(localDbPath))
      {
        testPath = "C:\\Mazak\\NFMS\\DB\\FCREADDAT01.mdb";
      }
      else
      {
        testPath = System.IO.Path.Combine(localDbPath, "FCREADDAT01.mdb");
      }

      if (System.IO.File.Exists(testPath))
      {
        //TODO: open database to check column existance for web vs E.
        logTrace.TraceEvent(TraceEventType.Warning, 0, "Assuming Mazak WEB version.  If this is incorrect it can be changed in the settings.");
        return DatabaseAccess.MazakDbType.MazakWeb;
      }
      else
      {
        logTrace.TraceEvent(TraceEventType.Warning, 0, "Assuming Mazak Smooth version.  If this is incorrect it can be changed in the settings.");
        return DatabaseAccess.MazakDbType.MazakSmooth;
      }
    }
  }

  public static class MazakProgram
  {
    public static void Main()
    {
#if DEBUG
      var useService = false;
#else
			var useService = true;
#endif
      Program.Run(useService, new MazakBackend());
    }
  }
}