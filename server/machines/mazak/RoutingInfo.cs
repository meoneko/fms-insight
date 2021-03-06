/* Copyright (c) 2019, John Lenz

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
using System.Linq;
using BlackMaple.MachineWatchInterface;

namespace MazakMachineInterface
{
  public class RoutingInfo : IJobControl, IOldJobDecrement
  {
    private static Serilog.ILogger Log = Serilog.Log.ForContext<RoutingInfo>();

    private IWriteData writeDb;
    private IReadDataAccess readDatabase;
    private IMazakLogReader logReader;
    private BlackMaple.MachineFramework.JobDB jobDB;
    private BlackMaple.MachineFramework.JobLogDB log;
    private IWriteJobs _writeJobs;
    private IMachineGroupName _machineGroupName;
    private IDecrementPlanQty _decr;
    private readonly IQueueSyncFault queueFault;
    private System.Timers.Timer _copySchedulesTimer;
    private readonly BlackMaple.MachineFramework.FMSSettings fmsSettings;

    public bool CheckPalletsUsedOnce;

    public event NewCurrentStatus OnNewCurrentStatus;
    public void RaiseNewCurrentStatus(CurrentStatus s) => OnNewCurrentStatus?.Invoke(s);

    public RoutingInfo(
      IWriteData d,
      IMachineGroupName machineGroupName,
      IReadDataAccess readDb,
      IMazakLogReader logR,
      BlackMaple.MachineFramework.JobDB jDB,
      BlackMaple.MachineFramework.JobLogDB jLog,
      IWriteJobs wJobs,
      IQueueSyncFault queueSyncFault,
      IDecrementPlanQty decrement,
      bool check,
      BlackMaple.MachineFramework.FMSSettings settings)
    {
      writeDb = d;
      readDatabase = readDb;
      fmsSettings = settings;
      jobDB = jDB;
      logReader = logR;
      log = jLog;
      _writeJobs = wJobs;
      _decr = decrement;
      _machineGroupName = machineGroupName;
      queueFault = queueSyncFault;
      CheckPalletsUsedOnce = check;

      _copySchedulesTimer = new System.Timers.Timer(TimeSpan.FromMinutes(4.5).TotalMilliseconds);
      _copySchedulesTimer.Elapsed += (sender, args) => RecopyJobsToSystem();
      _copySchedulesTimer.Start();
    }

    public void Halt()
    {
      _copySchedulesTimer.Stop();
    }

    #region Reading
    public CurrentStatus GetCurrentStatus()
    {
      MazakAllData mazakData;
      if (!OpenDatabaseKitDB.MazakTransactionLock.WaitOne(TimeSpan.FromMinutes(2), true))
      {
        throw new Exception("Unable to obtain mazak database lock");
      }
      try
      {
        mazakData = readDatabase.LoadAllData();
      }
      finally
      {
        OpenDatabaseKitDB.MazakTransactionLock.ReleaseMutex();
      }

      return BuildCurrentStatus.Build(jobDB, log, fmsSettings, _machineGroupName, queueFault, readDatabase.MazakType, mazakData, DateTime.UtcNow);
    }

    #endregion

    #region "Write Routing Info"

    public List<string> CheckValidRoutes(IEnumerable<JobPlan> jobs)
    {
      var logMessages = new List<string>();
      MazakAllData mazakData;

      if (!OpenDatabaseKitDB.MazakTransactionLock.WaitOne(TimeSpan.FromMinutes(2), true))
      {
        throw new Exception("Unable to obtain mazak database lock");
      }
      try
      {
        mazakData = readDatabase.LoadAllData();
      }
      finally
      {
        OpenDatabaseKitDB.MazakTransactionLock.ReleaseMutex();
      }

      try
      {
        Log.Debug("Check valid routing info");

        BlackMaple.MachineFramework.JobDB.ProgramRevision lookupProg(string prog, long? rev)
        {
          if (rev.HasValue)
          {
            return jobDB.LoadProgram(prog, rev.Value);
          }
          else
          {
            return jobDB.LoadMostRecentProgram(prog);
          }
        }

        //The reason we create the clsPalletPartMapping is to see if it throws any exceptions.  We therefore
        //need to ignore the warning that palletPartMap is not used.
#pragma warning disable 168, 219
        var mazakJobs = ConvertJobsToMazakParts.JobsToMazak(
          jobs,
          1,
          mazakData,
          new HashSet<string>(),
          writeDb.MazakType,
          CheckPalletsUsedOnce,
          fmsSettings,
          lookupProg,
          logMessages);
#pragma warning restore 168, 219

      }
      catch (Exception ex)
      {
        if (ex.Message.StartsWith("Invalid pallet->part mapping"))
        {
          logMessages.Add(ex.Message);
        }
        else
        {
          throw;
        }
      }

      return logMessages;
    }

    public void AddJobs(NewJobs newJ, string expectedPreviousScheduleId)
    {
      if (!OpenDatabaseKitDB.MazakTransactionLock.WaitOne(TimeSpan.FromMinutes(2), true))
      {
        throw new Exception("Unable to obtain mazak database lock");
      }
      try
      {
        _writeJobs.AddJobs(newJ, expectedPreviousScheduleId);
      }
      finally
      {
        OpenDatabaseKitDB.MazakTransactionLock.ReleaseMutex();
      }
    }

    public void RecopyJobsToSystem()
    {
      try
      {
        if (!OpenDatabaseKitDB.MazakTransactionLock.WaitOne(TimeSpan.FromMinutes(2), true))
        {
          throw new Exception("Unable to obtain mazak database lock");
        }
        try
        {
          _writeJobs.RecopyJobsToMazak();
        }
        finally
        {
          OpenDatabaseKitDB.MazakTransactionLock.ReleaseMutex();
        }
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Error recopying job schedules to mazak");
      }
    }
    #endregion

    #region "Decrement Plan Quantity"
    public List<JobAndDecrementQuantity> DecrementJobQuantites(long loadDecrementsStrictlyAfterDecrementId)
    {
      if (!OpenDatabaseKitDB.MazakTransactionLock.WaitOne(TimeSpan.FromMinutes(2), true))
      {
        throw new Exception("Unable to obtain mazak database lock");
      }
      try
      {
        _decr.Decrement();
      }
      finally
      {
        OpenDatabaseKitDB.MazakTransactionLock.ReleaseMutex();
      }
      logReader.RecheckQueues();
      return jobDB.LoadDecrementQuantitiesAfter(loadDecrementsStrictlyAfterDecrementId);
    }
    public List<JobAndDecrementQuantity> DecrementJobQuantites(DateTime loadDecrementsAfterTimeUTC)
    {
      if (!OpenDatabaseKitDB.MazakTransactionLock.WaitOne(TimeSpan.FromMinutes(2), true))
      {
        throw new Exception("Unable to obtain mazak database lock");
      }
      try
      {
        _decr.Decrement();
      }
      finally
      {
        OpenDatabaseKitDB.MazakTransactionLock.ReleaseMutex();
      }
      logReader.RecheckQueues();
      return jobDB.LoadDecrementQuantitiesAfter(loadDecrementsAfterTimeUTC);
    }

    public Dictionary<JobAndPath, int> OldDecrementJobQuantites()
    {
      throw new NotImplementedException();
    }

    public void OldFinalizeDecrement()
    {
      throw new NotImplementedException();
    }
    #endregion

    #region Queues
    public void AddUnallocatedCastingToQueue(string part, string queue, int position, string serial)
    {
      if (!fmsSettings.Queues.ContainsKey(queue))
      {
        throw new BlackMaple.MachineFramework.BadRequestException("Queue " + queue + " does not exist");
      }
      // num proc will be set later once it is allocated inside the MazakQueues thread
      var matId = log.AllocateMaterialIDForCasting(part, 1);

      Log.Debug("Adding unprocessed casting for part {part} to queue {queue} in position {pos} with serial {serial}. " +
                "Assigned matId {matId}",
        part, queue, position, serial, matId
      );

      if (!string.IsNullOrEmpty(serial))
      {
        log.RecordSerialForMaterialID(
          new BlackMaple.MachineFramework.JobLogDB.EventLogMaterial()
          {
            MaterialID = matId,
            Process = 0,
            Face = ""
          },
          serial);
      }
      // the add to queue log entry will use the process, so later when we lookup the latest completed process
      // for the material in the queue, it will be correctly computed.
      log.RecordAddMaterialToQueue(matId, 0, queue, position);
      logReader.RecheckQueues();
      RaiseNewCurrentStatus(GetCurrentStatus());
    }

    public void AddUnprocessedMaterialToQueue(string jobUnique, int process, string queue, int position, string serial)
    {
      if (!fmsSettings.Queues.ContainsKey(queue))
      {
        throw new BlackMaple.MachineFramework.BadRequestException("Queue " + queue + " does not exist");
      }

      Log.Debug("Adding unprocessed material for job {job} proc {proc} to queue {queue} in position {pos} with serial {serial}",
        jobUnique, process, queue, position, serial
      );

      var job = jobDB.LoadJob(jobUnique);
      if (job == null) throw new BlackMaple.MachineFramework.BadRequestException("Unable to find job " + jobUnique);
      var matId = log.AllocateMaterialID(jobUnique, job.PartName, job.NumProcesses);
      if (!string.IsNullOrEmpty(serial))
      {
        log.RecordSerialForMaterialID(
          new BlackMaple.MachineFramework.JobLogDB.EventLogMaterial()
          {
            MaterialID = matId,
            Process = process,
            Face = ""
          },
          serial);
      }
      // the add to queue log entry will use the process, so later when we lookup the latest completed process
      // for the material in the queue, it will be correctly computed.
      log.RecordAddMaterialToQueue(matId, process, queue, position);
      logReader.RecheckQueues();
      RaiseNewCurrentStatus(GetCurrentStatus());
    }

    public void SetMaterialInQueue(long materialId, string queue, int position)
    {
      if (!fmsSettings.Queues.ContainsKey(queue))
      {
        throw new BlackMaple.MachineFramework.BadRequestException("Queue " + queue + " does not exist");
      }
      Log.Debug("Adding material {matId} to queue {queue} in position {pos}",
        materialId, queue, position
      );
      var proc =
        log.GetLogForMaterial(materialId)
        .SelectMany(e => e.Material)
        .Where(m => m.MaterialID == materialId)
        .Select(m => m.Process)
        .DefaultIfEmpty(0)
        .Max();
      log.RecordAddMaterialToQueue(materialId, proc, queue, position);
      logReader.RecheckQueues();
      RaiseNewCurrentStatus(GetCurrentStatus());
    }

    public void RemoveMaterialFromAllQueues(long materialId)
    {
      Log.Debug("Removing {matId} from all queues", materialId);
      log.RecordRemoveMaterialFromAllQueues(materialId);
      logReader.RecheckQueues();
      RaiseNewCurrentStatus(GetCurrentStatus());
    }
    #endregion
  }
}
