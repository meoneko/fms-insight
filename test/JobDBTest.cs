/* Copyright (c) 2017, John Lenz

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
using BlackMaple.MachineFramework;
using BlackMaple.MachineWatchInterface;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MachineWatchTest
{
  public class JobDBTest : IDisposable
	{
		private JobDB _jobDB;

		public JobDBTest()
		{
			var connection = new SqliteConnection("Data Source=:memory:");
			connection.Open();
			_jobDB = new JobDB(connection);
      _jobDB.CreateTables();
		}

		public void Dispose()
		{
			_jobDB.Close();
		}

		public static void SetJob1Data(JobPlan job1)
		{
      var rnd = new Random();
			job1.PartName = "Job1";
			job1.SetPlannedCyclesOnFirstProcess(1, 125);
			job1.SetPlannedCyclesOnFirstProcess(2, 53);
      job1.RouteStartingTimeUTC = DateTime.UtcNow.AddMinutes(-10);
      job1.RouteEndingTimeUTC = DateTime.UtcNow;
      job1.Archived = false;
      job1.JobCopiedToSystem = rnd.Next(0,2) > 0;
	  job1.ScheduleId = "Job1tag" + rnd.Next().ToString();
			job1.HoldEntireJob.UserHold = true;
			job1.HoldEntireJob.ReasonForUserHold = "test string";
			job1.HoldEntireJob.HoldUnholdPatternStartUTC = DateTime.UtcNow;
			job1.HoldEntireJob.HoldUnholdPatternRepeats = true;
			job1.HoldEntireJob.HoldUnholdPattern.Add(TimeSpan.FromMinutes(10));
			job1.HoldEntireJob.HoldUnholdPattern.Add(TimeSpan.FromMinutes(18));
			job1.HoldEntireJob.HoldUnholdPattern.Add(TimeSpan.FromMinutes(125));
			job1.Priority = 164;
			job1.Comment = "Hello there";
			job1.CreateMarkerData = true;
      job1.ScheduledBookingIds.Add("booking1");
      job1.ScheduledBookingIds.Add("booking2");
      job1.ScheduledBookingIds.Add("booking3");

			job1.SetPartsPerPallet(1, 1, 10);
			job1.SetPartsPerPallet(1, 2, 15);
			job1.SetPartsPerPallet(2, 1, 20);
			job1.SetPartsPerPallet(2, 2, 22);
			job1.SetPartsPerPallet(2, 3, 23);

			job1.SetPathGroup(1, 1, 64);
			job1.SetPathGroup(1, 2, 74);
			job1.SetPathGroup(2, 1, 12);
			job1.SetPathGroup(2, 2, 88);
			job1.SetPathGroup(2, 3, 5);
			
			job1.SetSimulatedStartingTimeUTC(1, 1, DateTime.Parse("1/6/2011 5:34 AM GMT"));
			job1.SetSimulatedStartingTimeUTC(1, 2, DateTime.Parse("2/10/2011 6:45 AM GMT"));
			job1.SetSimulatedStartingTimeUTC(2, 1, DateTime.Parse("3/14/2011 7:03 AM GMT"));
			job1.SetSimulatedStartingTimeUTC(2, 2, DateTime.Parse("4/20/2011 8:22 PM GMT"));
			job1.SetSimulatedStartingTimeUTC(2, 3, DateTime.Parse("5/22/2011 9:18 AM GMT"));

      job1.SetSimulatedProduction(1, 1, RandSimProduction());
      job1.SetSimulatedProduction(1, 2, RandSimProduction());
      job1.SetSimulatedProduction(2, 1, RandSimProduction());
      job1.SetSimulatedProduction(2, 2, RandSimProduction());
      job1.SetSimulatedProduction(2, 3, RandSimProduction());

      job1.SetSimulatedAverageFlowTime(1, 1, TimeSpan.FromMinutes(0.5));
      job1.SetSimulatedAverageFlowTime(1, 2, TimeSpan.FromMinutes(1.5));
      job1.SetSimulatedAverageFlowTime(2, 1, TimeSpan.FromMinutes(2.5));
      job1.SetSimulatedAverageFlowTime(2, 2, TimeSpan.FromMinutes(3.5));
      job1.SetSimulatedAverageFlowTime(2, 3, TimeSpan.FromMinutes(4.5));

			job1.AddProcessOnPallet(1, 1, "Pal2");
			job1.AddProcessOnPallet(1, 1, "Pal5");
			job1.AddProcessOnPallet(1, 2, "Pal4");
			job1.AddProcessOnPallet(1, 2, "Pal35");
			job1.AddProcessOnPallet(2, 1, "Pal12");
			job1.AddProcessOnPallet(2, 1, "Pal64");
			job1.AddProcessOnPallet(2, 2, "Hi");
			job1.AddProcessOnPallet(2, 2, "Pal2");
			job1.AddProcessOnPallet(2, 3, "Pal5");
			job1.AddProcessOnPallet(2, 3, "OMG");
			
			job1.AddProcessOnFixture(1, 1, "Fix1", "Face1");
			job1.AddProcessOnFixture(1, 1, "Fix2", "Face2");
			job1.AddProcessOnFixture(1, 2, "Fixxx", "Face3");
			job1.AddProcessOnFixture(1, 2, "ABC", "Face4");
			job1.AddProcessOnFixture(2, 1, "Fix5", "Face5");
			job1.AddProcessOnFixture(2, 1, "Fix123", "Face6");
			job1.AddProcessOnFixture(2, 2, "Bye", "erhh");
			job1.AddProcessOnFixture(2, 2, "Fix22", "Face122");
			job1.AddProcessOnFixture(2, 3, "Fix17", "Fac7");
			job1.AddProcessOnFixture(2, 3, "Yeeeee", "Fa7753");

			job1.AddLoadStation(1, 1, 35);
			job1.AddLoadStation(1, 1, 64);
			job1.AddLoadStation(1, 2, 785);
			job1.AddLoadStation(1, 2, 15);
			job1.AddLoadStation(2, 1, 647);
			job1.AddLoadStation(2, 1, 474);
			job1.AddLoadStation(2, 2, 785);
			job1.AddLoadStation(2, 2, 53);
			job1.AddLoadStation(2, 3, 15);
			job1.AddUnloadStation(1, 1, 75);
			job1.AddUnloadStation(1, 1, 234);
			job1.AddUnloadStation(1, 2, 53);
			job1.AddUnloadStation(2, 1, 563);
			job1.AddUnloadStation(2, 2, 2);
			job1.AddUnloadStation(2, 2, 12);
			job1.AddUnloadStation(2, 3, 32);
			
			var route = new JobMachiningStop("Machine");
			route.AddProgram(23, "Hello");
			route.AddProgram(12, "Emily");
      route.ExpectedCycleTime = TimeSpan.FromHours(1.2);
      route.Tools["tool1"] = TimeSpan.FromMinutes(30);
      route.Tools["tool2"] = TimeSpan.FromMinutes(35);
			job1.AddMachiningStop(1, 1, route);
			
			route = new JobMachiningStop("Other Machine");
			route.AddProgram(23, "agw");
			route.AddProgram(12, "awef");
      route.ExpectedCycleTime = TimeSpan.FromHours(2.8);
      route.Tools["tool1"] = TimeSpan.FromMinutes(9);
      route.Tools["tool33"] = TimeSpan.FromMinutes(42);
			job1.AddMachiningStop(1, 2, route);
			
			route = new JobMachiningStop("Test");
			route.AddProgram(64, "Goodbye");
			route.AddProgram(323, "Whee");			
      route.ExpectedCycleTime = TimeSpan.FromHours(6.3);
      route.Tools["tool2"] = TimeSpan.FromMinutes(12);
      route.Tools["tool44"] = TimeSpan.FromMinutes(99);
			job1.AddMachiningStop(2, 1, route);
			
			route = new JobMachiningStop("Test");
			route.AddProgram(64, "agwe");
			route.AddProgram(32, "wefq");			
			job1.AddMachiningStop(2, 2, route);

			route = new JobMachiningStop("Test");
			route.AddProgram(245, "oh my");
			route.AddProgram(36, "dduuude");
			job1.AddMachiningStop(2, 1, route);
			
			route = new JobMachiningStop("Test");
			route.AddProgram(23, "so cool");
			route.AddProgram(53, "so cool");
			job1.AddMachiningStop(2, 2, route);
			
			job1.AddInspection(new JobInspectionData("Insp1", "counter1", 53, TimeSpan.FromMinutes(100), 12));
			job1.AddInspection(new JobInspectionData("Insp2", "counter1", 12, TimeSpan.FromMinutes(64)));
			job1.AddInspection(new JobInspectionData("Insp3", "abcdef", 175, TimeSpan.FromMinutes(121), 2));
			job1.AddInspection(new JobInspectionData("Insp4", "counter2", 16.12, TimeSpan.FromMinutes(33)));
			job1.AddInspection(new JobInspectionData("Insp5", "counter3", 0.544, TimeSpan.FromMinutes(44)));
			
			job1.HoldMachining(1, 1).UserHold = false;
			job1.HoldMachining(1, 1).ReasonForUserHold = "reason for user hold";
			job1.HoldMachining(1, 1).HoldUnholdPatternRepeats  = false;
			job1.HoldMachining(1, 1).HoldUnholdPatternStartUTC = DateTime.Parse("2010/5/3 7:32 PM").ToUniversalTime();
			job1.HoldMachining(1, 1).HoldUnholdPattern.Add(TimeSpan.FromMinutes(5));
			job1.HoldMachining(1, 1).HoldUnholdPattern.Add(TimeSpan.FromMinutes(53));
			
			job1.HoldMachining(1, 2).UserHold = true;
			job1.HoldMachining(1, 2).ReasonForUserHold = "another reason for user hold";
			job1.HoldMachining(1, 2).HoldUnholdPatternRepeats  = true;
			job1.HoldMachining(1, 2).HoldUnholdPatternStartUTC = DateTime.Parse("2010/5/12 6:12 PM").ToUniversalTime();
			job1.HoldMachining(1, 2).HoldUnholdPattern.Add(TimeSpan.FromMinutes(84));
			job1.HoldMachining(1, 2).HoldUnholdPattern.Add(TimeSpan.FromMinutes(1));
			
			job1.HoldMachining(2, 1).UserHold = false;
			job1.HoldMachining(2, 1).ReasonForUserHold = "oh my reason for user hold";
			job1.HoldMachining(2, 1).HoldUnholdPatternRepeats  = true;
			job1.HoldMachining(2, 1).HoldUnholdPatternStartUTC = DateTime.Parse("2010/9/1 1:30 PM").ToUniversalTime();
			job1.HoldMachining(2, 1).HoldUnholdPattern.Add(TimeSpan.FromMinutes(532));
			job1.HoldMachining(2, 1).HoldUnholdPattern.Add(TimeSpan.FromMinutes(64));

			job1.HoldLoadUnload(1, 1).UserHold = true;
			job1.HoldLoadUnload(1, 1).ReasonForUserHold = "abcdef";
			job1.HoldLoadUnload(1, 1).HoldUnholdPatternRepeats  = true;
			job1.HoldLoadUnload(1, 1).HoldUnholdPatternStartUTC = DateTime.Parse("2010/12/2 3:32 PM").ToUniversalTime();
			job1.HoldLoadUnload(1, 1).HoldUnholdPattern.Add(TimeSpan.FromMinutes(63));
			job1.HoldLoadUnload(1, 1).HoldUnholdPattern.Add(TimeSpan.FromMinutes(7));
			
			job1.HoldLoadUnload(1, 2).UserHold = false;
			job1.HoldLoadUnload(1, 2).ReasonForUserHold = "agr";
			job1.HoldLoadUnload(1, 2).HoldUnholdPatternRepeats  = false;
			job1.HoldLoadUnload(1, 2).HoldUnholdPatternStartUTC = DateTime.Parse("2010/6/1 3:12 PM").ToUniversalTime();
			job1.HoldLoadUnload(1, 2).HoldUnholdPattern.Add(TimeSpan.FromMinutes(174));
			job1.HoldLoadUnload(1, 2).HoldUnholdPattern.Add(TimeSpan.FromMinutes(83));
			
			job1.HoldLoadUnload(2, 3).UserHold = true;
			job1.HoldLoadUnload(2, 3).ReasonForUserHold = "erhagsad";
			job1.HoldLoadUnload(2, 3).HoldUnholdPatternRepeats  = false;
			job1.HoldLoadUnload(2, 3).HoldUnholdPatternStartUTC = DateTime.Parse("2010/11/5 9:30 AM").ToUniversalTime();
			job1.HoldLoadUnload(2, 3).HoldUnholdPattern.Add(TimeSpan.FromMinutes(32));
			job1.HoldLoadUnload(2, 3).HoldUnholdPattern.Add(TimeSpan.FromMinutes(64));
		}
		
		public static void SetJob2Data(JobPlan job2)
		{
			job2.PartName = "Job1";
      job2.Archived = false;
      job2.RouteStartingTimeUTC = DateTime.UtcNow.AddHours(-10);
      job2.RouteEndingTimeUTC = DateTime.UtcNow.AddHours(-2);
			job2.SetPlannedCyclesOnFirstProcess(1, 4);
			job2.HoldEntireJob.UserHold = false;
			job2.HoldEntireJob.ReasonForUserHold = "another reason";
			job2.HoldEntireJob.HoldUnholdPatternRepeats = false;
			job2.HoldEntireJob.HoldUnholdPatternStartUTC = DateTime.Parse("2010/1/1 5:43 PM").ToUniversalTime();
			job2.HoldEntireJob.HoldUnholdPattern.Add(TimeSpan.FromMinutes(5));
			job2.HoldEntireJob.HoldUnholdPattern.Add(TimeSpan.FromMinutes(533));
			job2.Comment = "this is a test";
	  		job2.ScheduleId = "Job2tag-hello";
			job2.Priority = 7654;
			job2.CreateMarkerData = false;
			job2.JobCopiedToSystem = false;
			
			job2.SetPartsPerPallet(1, 1, 3);
			job2.SetPartsPerPallet(2, 1, 5);
			job2.SetPartsPerPallet(3, 1, 6);

			job2.SetPathGroup(1, 1, 17);
			job2.SetPathGroup(2, 1, 75);
			job2.SetPathGroup(3, 1, 33);
			
			job2.SetSimulatedStartingTimeUTC(1, 1, DateTime.Parse("8/22/2011 3:14 AM GMT"));
			job2.SetSimulatedStartingTimeUTC(2, 1, DateTime.Parse("9/15/2011 4:25 PM GMT"));
			job2.SetSimulatedStartingTimeUTC(3, 1, DateTime.Parse("10/4/2011 5:33 AM GMT"));


			job2.AddProcessOnPallet(1, 1, "Pal2");
			job2.AddProcessOnPallet(1, 1, "asg");
			job2.AddProcessOnPallet(2, 1, "awerg");
			job2.AddProcessOnPallet(3, 1, "ehe");
			job2.AddProcessOnPallet(3, 1, "awger");
			
			job2.AddProcessOnFixture(1, 1, "Fix6", "asg43");
			job2.AddProcessOnFixture(1, 1, "h343", "werg");
			job2.AddProcessOnFixture(2, 1, "a235sg", "awef");
			job2.AddProcessOnFixture(3, 1, "erte34", "htr");
			job2.AddProcessOnFixture(3, 1, "35aert", "aweh33");

			job2.AddLoadStation(1, 1, 375);
			job2.AddLoadStation(2, 1, 86);
			job2.AddLoadStation(2, 1, 36);
			job2.AddLoadStation(3, 1, 86);
			job2.AddLoadStation(3, 1, 375);
			job2.AddUnloadStation(1, 1, 234);
			job2.AddUnloadStation(2, 1, 75);
			job2.AddUnloadStation(3, 1, 786);

			var route = new JobMachiningStop("Abc");
			route.AddProgram(12, "fgaserg");
			job2.AddMachiningStop(1, 1, route);
			
			route = new JobMachiningStop("gwerwer");
			route.AddProgram(23, "awga");
			job2.AddMachiningStop(2, 1, route);
			
			route = new JobMachiningStop("agreer");
			route.AddProgram(75, "ahtt");
			route.AddProgram(365, "awreer");			
			job2.AddMachiningStop(3, 1, route);

			route = new JobMachiningStop("duude");
			route.AddProgram(643, "awgouihag");
			route.AddProgram(7458, "agoouerherg");
			job2.AddMachiningStop(3, 1, route);

		}

    private static IEnumerable<SimulatedStationUtilization> RandSimStationUse()
    {
        var rnd = new Random();
        var ret = new List<SimulatedStationUtilization>();
        for (int i = 0; i < 3; i++) {
            ret.Add(new SimulatedStationUtilization("id" + rnd.Next(0, 10000).ToString(),
                                            "group" + rnd.Next(0, 100000).ToString(),
                                            rnd.Next(0, 10000),
                                            DateTime.UtcNow.AddMinutes(-rnd.Next(200, 300)),
                                            DateTime.UtcNow.AddMinutes(-rnd.Next(0, 100)),
                                            TimeSpan.FromMinutes(rnd.Next(10,1000)),
											TimeSpan.FromMinutes(rnd.Next(10,1000))));
        }
        return ret;
    }

	private static Dictionary<string, int> RandExtraParts()
    {
        var rnd = new Random();
        var ret = new Dictionary<string, int>();
        for (int i = 0; i < 3; i++) {
            ret.Add("part" + rnd.Next(0, 10000).ToString(),
			        rnd.Next(10000));
        }
        return ret;
    }

    private static IEnumerable<JobPlan.SimulatedProduction> RandSimProduction()
    {
        var rnd = new Random();
        var ret = new List<JobPlan.SimulatedProduction>();
        for (int i = 0; i < 3; i++) {
            var prod = default(JobPlan.SimulatedProduction);
            prod.TimeUTC = DateTime.UtcNow.AddMinutes(-100+i);
            prod.Quantity = rnd.Next(0, 100);
            ret.Add(prod);
        }

        return ret;
    }

    private void PrintJob(JobPlan j)
    {
        var str = Newtonsoft.Json.JsonConvert.SerializeObject(j, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
        Console.WriteLine("XXXXX " + str);
    }

		[Fact]
		public void Jobs()
		{
			var job1 = new JobPlan("Unique1", 2, new int[] {2, 3});
			SetJob1Data(job1);
			
			_jobDB.AddJob(job1);

			CheckJobs(job1, null, null, job1.ScheduleId, null);
			Assert.Empty(_jobDB.LoadExtraPartsForScheduleId(job1.ScheduleId));
			var recent = _jobDB.LoadMostRecentSchedule();
			Assert.Empty(recent.ExtraParts);
			Assert.Equal(job1.ScheduleId, recent.LatestScheduleId);
			CheckPlanEqual(job1, recent.Jobs[0], true);

			var job2 = new JobPlan(new JobPlan("Unique2", 3));
			SetJob2Data(job2);

      var simStationUse = RandSimStationUse();
	  var theExtraParts = RandExtraParts();
			
			_jobDB.AddJobs(new NewJobs(job2.ScheduleId, new JobPlan[] { job2 }, simStationUse, theExtraParts, true));

			Assert.Equal(theExtraParts, _jobDB.LoadExtraPartsForScheduleId(job2.ScheduleId));
			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);
      CheckJobsDate(job1, job2, null);
      CheckSimStationUse(simStationUse);
            Assert.True(_jobDB.DoesJobExist(job1.UniqueStr));
            Assert.False(_jobDB.DoesJobExist("aoughwoeufeg"));
            var newAfter = _jobDB.LoadJobsAfterScheduleId(job1.ScheduleId);
            Assert.Equal(1, newAfter.Jobs.Count);
			Assert.Equal(job2.ScheduleId, newAfter.LatestScheduleId);
			Assert.Equal(theExtraParts, newAfter.ExtraParts);
            CheckPlanEqual(job2, newAfter.Jobs[0], true);
            Assert.Equal(0, _jobDB.LoadJobsAfterScheduleId(job2.ScheduleId).Jobs.Count);

			recent = _jobDB.LoadMostRecentSchedule();
			Assert.Equal(theExtraParts, recent.ExtraParts);
			Assert.Equal(job2.ScheduleId, recent.LatestScheduleId);
			CheckPlanEqual(job2, recent.Jobs[0], true);

			job2.SetPlannedCyclesOnFirstProcess(1, 563);
			job2.Priority = 675;

			_jobDB.UpdateJob(job2);

			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);

			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);
			
			_jobDB.UpdateJob("Unique1", 32, "newcomm");
			job1.Comment = "newcomm";
			job1.Priority = 32;
			
			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);
			
			_jobDB.UpdateJob("Unique1", 64, "hello");
			job1.Comment = "hello";
			job1.Priority = 64;
			
			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);
			
			job1.HoldEntireJob.UserHold = false;
			job1.HoldEntireJob.ReasonForUserHold = "this is the reason";
			job1.HoldEntireJob.HoldUnholdPatternStartUTC = DateTime.Parse("2010/6/29 6:12 AM").ToUniversalTime();
			job1.HoldEntireJob.HoldUnholdPatternRepeats = false;
			job1.HoldEntireJob.HoldUnholdPattern.Clear();
			job1.HoldEntireJob.HoldUnholdPattern.Add(TimeSpan.FromSeconds(1255));
			_jobDB.UpdateJobHold("Unique1", job1.HoldEntireJob);
			
			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);
			
			job1.HoldMachining(1, 1).UserHold = true;
			job1.HoldMachining(1, 1).ReasonForUserHold = "abnceasd";
			job1.HoldMachining(1, 1).HoldUnholdPatternStartUTC = DateTime.Parse("2010/8/3 7:55 AM").ToUniversalTime();
			job1.HoldMachining(1, 1).HoldUnholdPatternRepeats = false;
			job1.HoldMachining(1, 1).HoldUnholdPattern.Clear();
			_jobDB.UpdateJobMachiningHold("Unique1", 1, 1, job1.HoldMachining(1, 1));
			
			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);
			
			job1.HoldLoadUnload(2, 3).UserHold = false;
			job1.HoldLoadUnload(2, 3).ReasonForUserHold = "agrwerg";
			job1.HoldLoadUnload(2, 3).HoldUnholdPatternStartUTC = DateTime.Parse("2010/7/2 9:55 AM").ToUniversalTime();
			job1.HoldLoadUnload(2, 3).HoldUnholdPatternRepeats = true;
			job1.HoldLoadUnload(2, 3).HoldUnholdPattern.Clear();
			job1.HoldLoadUnload(2, 3).HoldUnholdPattern.Add(TimeSpan.FromSeconds(64));
			job1.HoldLoadUnload(2, 3).HoldUnholdPattern.Add(TimeSpan.FromSeconds(12));
			job1.HoldLoadUnload(2, 3).HoldUnholdPattern.Add(TimeSpan.FromSeconds(6743));
			_jobDB.UpdateJobLoadUnloadHold("Unique1", 2, 3, job1.HoldLoadUnload(2, 3));
			
			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);

			Assert.Null(_jobDB.LoadJob("aguheriheg"));

			//check job2 is not copied
			var notCopied = _jobDB.LoadJobsNotCopiedToSystem(DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddHours(-3));
			Assert.Equal(1, notCopied.Jobs.Count);
			CheckJobEqual(job2, notCopied.Jobs.FirstOrDefault(), true);

			//mark job2 copied
			_jobDB.MarkJobCopiedToSystem("Unique2");
			job2.JobCopiedToSystem = true;
			CheckJobs(job1, job2, null, job2.ScheduleId, theExtraParts);
			notCopied = _jobDB.LoadJobsNotCopiedToSystem(DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddHours(-3));
			Assert.Equal(0, notCopied.Jobs.Count);

      //Archive job2
      _jobDB.ArchiveJob(job2.UniqueStr);
      job2.Archived = true;
      CheckJobs(job1, null, null, job2.ScheduleId, theExtraParts);
      CheckJobsDate(job1, job2, null);

      //delete job1
			_jobDB.DeleteJob("Unique1");
			Assert.Equal(0, _jobDB.LoadJobs().Jobs.Count);
      CheckJobsDate(null, job2, null);
            Assert.False(_jobDB.DoesJobExist("Unique1"));

            //delete job2
            _jobDB.DeleteJob("Unique2");
			Assert.Equal(0, _jobDB.LoadJobs().Jobs.Count);
			Assert.Equal(0, _jobDB.LoadJobs(DateTime.UtcNow.AddHours(-100), DateTime.UtcNow.AddMinutes(100)).Count);

			job1 = new JobPlan(new JobPlan(job1, "Test1"));
			job1.SetPlannedCyclesOnFirstProcess(1, 153);
			job2.SetPlannedCyclesOnFirstProcess(1, 4);
      job2.Archived = false;
			
			var job3 = new JobPlan(new JobPlan(job1, "Test2"));
      job3.Archived = false;
			job3.SetPlannedCyclesOnFirstProcess(1, 4);
			job3.SetPlannedCyclesOnFirstProcess(2, 5);

			_jobDB.AddJobs(new NewJobs(new JobPlan[] { job1, job2, job3 }, new SimulatedStationUtilization[] {}, false, "tag2", false));
			
			CheckJobs(job1, job2, job3, job2.ScheduleId, theExtraParts);
			
			for (int i = 0; i < 4; i++) {
				_jobDB.AddCompletedFirstProcess(job2.UniqueStr, 1, i*15);
				_jobDB.AddCompletedMaterial(job2.UniqueStr, i*15);
				_jobDB.AddCompletedFirstProcess(job3.UniqueStr, 1, i*10 + 100);
				_jobDB.AddCompletedMaterial(job3.UniqueStr, i*10+100);
			}
			
			_jobDB.ArchiveCompletedJobs();
      job2.Archived = true;
			
			CheckJobs(job1, job3, null, job2.ScheduleId, theExtraParts);
      CheckJobsDate(job1, job2, job3);
			
			for (int i = 0; i < 4; i++) {
				_jobDB.AddCompletedFirstProcess(job3.UniqueStr, 2, i*10 + 200);
				_jobDB.AddCompletedMaterial(job3.UniqueStr, i*10+200);
			}
			_jobDB.AddCompletedFirstProcess(job3.UniqueStr, 2, 240);
			
			_jobDB.ArchiveCompletedJobs();
			
			CheckJobs(job1, job3, null, job2.ScheduleId, theExtraParts);
      CheckJobsDate(job1, job2, job3);
			
			_jobDB.AddCompletedMaterial(job3.UniqueStr, 240);
			
			_jobDB.ArchiveCompletedJobs();
      job3.Archived = true;
			
			CheckJobs(job1, null, null, job2.ScheduleId, theExtraParts);
      CheckJobsDate(job1, job2, job3);
		}

		[Fact]
		public void TMaterial()
		{
			Assert.Empty(_jobDB.GetMaterialInProcess("whee", 1));
			Assert.Equal(0, _jobDB.GetCompletedOnAnyPath("whee"));

			_jobDB.AddCompletedMaterial("whee", 1);
			_jobDB.AddCompletedMaterial("whee", 2);

			Assert.Equal(2, _jobDB.GetCompletedOnAnyPath("whee"));

			_jobDB.AddCompletedMaterial("whee", 1);

			Assert.Equal(2, _jobDB.GetCompletedOnAnyPath("whee"));

			_jobDB.AddCompletedMaterial("whee", 3);
			_jobDB.AddCompletedMaterial("whee2", 53);
			_jobDB.AddCompletedMaterial("whee2", 42);
			_jobDB.AddCompletedMaterial("whee2", 53);

			Assert.Equal(3, _jobDB.GetCompletedOnAnyPath("whee"));
			Assert.Equal(2, _jobDB.GetCompletedOnAnyPath("whee2"));

			_jobDB.AddMaterialInProcess("whee", 2, 6);
			_jobDB.AddMaterialInProcess("whee", 3, 7);
			_jobDB.AddMaterialInProcess("whee", 2, 12);
			_jobDB.AddMaterialInProcess("whee2", 3, 13);
			_jobDB.AddMaterialInProcess("whee2", 3, 54);

			Assert.Equal(_jobDB.GetMaterialInProcess("whee", 2), (IEnumerable<long>)new long[] { 6, 12 });
			Assert.Equal(_jobDB.GetMaterialInProcess("whee", 3), (IEnumerable<long>)new long[] { 7 });
			Assert.Equal(_jobDB.GetMaterialInProcess("whee2", 3), (IEnumerable<long>)new long[] { 13, 54 });

			_jobDB.RemoveMaterialInProcess("whee", 2, 6);
			Assert.Equal(_jobDB.GetMaterialInProcess("whee", 2), (IEnumerable<long>)new long[] { 12 });

			_jobDB.AddMaterialInProcess("whee", 2, 123);
			_jobDB.AddMaterialInProcess("whee", 2, 644);

			Assert.Equal(_jobDB.GetMaterialInProcess("whee", 2), (IEnumerable<long>)new long[] { 12, 123, 644 });

			_jobDB.RemoveMaterialInProcess("whee", 2, 6);

			Assert.Equal(_jobDB.GetMaterialInProcess("whee", 2), (IEnumerable<long>)new long[] { 12, 123, 644 });

			_jobDB.RemoveMaterialInProcess("whee", 2, 123);
			_jobDB.RemoveMaterialInProcess("whee", 2, 123);

			Assert.Equal(_jobDB.GetMaterialInProcess("whee", 2), (IEnumerable<long>)new long[] { 12, 644 });
			
			_jobDB.AddCompletedFirstProcess("whee", 1, 1);
			_jobDB.AddCompletedFirstProcess("whee", 2, 2);
			
			Assert.Equal(_jobDB.GetMaterialCompletedFirstProcess("whee", 1), (IEnumerable<long>)new long[] { 1 });			
			Assert.Equal(_jobDB.GetMaterialCompletedFirstProcess("whee", 2), (IEnumerable<long>)new long[] { 2 });
			
			Assert.Equal(1, _jobDB.GetCompletedCount("whee", 1));
			Assert.Equal(1, _jobDB.GetCompletedCount("whee", 2));
			
			_jobDB.AddCompletedFirstProcess("whee", 1, 3);
			_jobDB.AddCompletedFirstProcess("whee", 2, 4);
			
			Assert.Equal(_jobDB.GetMaterialCompletedFirstProcess("whee", 1), (IEnumerable<long>)new long[] { 1, 3 });			
			Assert.Equal(_jobDB.GetMaterialCompletedFirstProcess("whee", 2), (IEnumerable<long>)new long[] { 2, 4 });

			Assert.Equal(2, _jobDB.GetCompletedCount("whee", 1));
			Assert.Equal(1, _jobDB.GetCompletedCount("whee", 2));

			_jobDB.AddCompletedMaterial("whee", 4);
			
			Assert.Equal(2, _jobDB.GetCompletedCount("whee", 1));
			Assert.Equal(2, _jobDB.GetCompletedCount("whee", 2));
		}

		[Fact]
		public void TInspectionStorage()
		{
			var job1 = new JobPlan("abc", 1);
			var job2 = new JobPlan("def", 1);

			job1.AddInspection(new JobInspectionData("type1", "ctr1", 1, TimeSpan.FromMinutes(20), 55));
			job1.AddInspection(new JobInspectionData("type2", "ctr1", 3, TimeSpan.FromMinutes(56)));
			job1.AddInspection(new JobInspectionData("type3", "ctr3", 5, TimeSpan.FromMinutes(32), 22));
			job1.AddInspection(new JobInspectionData("freq1", "ctr5", 0.1234, TimeSpan.FromMinutes(55)));

			job2.AddInspection(new JobInspectionData("type4", "ctr4", 6, TimeSpan.FromMinutes(16)));
			job2.AddInspection(new JobInspectionData("type1", "ctr2", 7, TimeSpan.FromMinutes(64)));
			job2.AddInspection(new JobInspectionData("type2", "ctr3", 8, TimeSpan.FromMinutes(166), 66));
			job2.AddInspection(new JobInspectionData("freq2", "ctr5", 0.5, TimeSpan.FromMinutes(66)));
			job2.AddInspection(new JobInspectionData("freq3", "ctr2", 0.22, TimeSpan.FromMinutes(77)));

			_jobDB.AddJobInspections(new JobPlan[] { job1, job2});
			//Try overwriting
			_jobDB.AddJobInspections(new JobPlan[] { job1 });

			CheckInspEqual(job1.GetInspections(), _jobDB.LoadInspections("abc"));
			CheckInspEqual(job2.GetInspections(), _jobDB.LoadInspections("def"));
		}

		[Fact]
		public void DecrementQuantities()
		{
			var decs = new List<JobAndDecrementQuantity>();

			var d = default(JobAndDecrementQuantity);
			d.DecrementId = "bbb";
			d.JobUnique = "uniq1";
			d.Part = "part1";
			d.TimeUTC = new DateTime(2017, 1, 1);
			d.Quantity = 53;
			decs.Add(d);

			d = default(JobAndDecrementQuantity);
			d.DecrementId = "bbb";
			d.JobUnique = "uniq2";
			d.Part = "part2";
			d.TimeUTC = new DateTime(2017, 1, 1);
			d.Quantity = 821;
			decs.Add(d);

			d = default(JobAndDecrementQuantity);
			d.DecrementId = "ddd";
			d.JobUnique = "uniq1";
			d.Part = "part1";
			d.TimeUTC = new DateTime(2017, 2, 1);
			d.Quantity = 102;
			decs.Add(d);

			_jobDB.AddNewDecrement(decs);

			Assert.Equal(decs, _jobDB.LoadDecrementQuantitiesAfter("aaa"));
			Assert.Equal(new [] {decs[2]}, _jobDB.LoadDecrementQuantitiesAfter("bbb"));
			Assert.Equal(new [] {decs[2]}, _jobDB.LoadDecrementQuantitiesAfter("ccc"));
			Assert.Empty(_jobDB.LoadDecrementQuantitiesAfter("ddd"));

			Assert.Equal(decs, _jobDB.LoadDecrementQuantitiesAfter(new DateTime(2016, 1, 1)));
			Assert.Equal(new [] {decs[2]}, _jobDB.LoadDecrementQuantitiesAfter(new DateTime(2017, 1, 15)));
			Assert.Empty(_jobDB.LoadDecrementQuantitiesAfter(new DateTime(2017, 2, 1)));
		}

		private void CheckJobs(JobPlan job1, JobPlan job2, JobPlan job3, string schId, Dictionary<string, int> extraParts)
		{
			CheckJobEqual(job1, _jobDB.LoadJob(job1.UniqueStr), true);
			if ((job2 != null)) CheckJobEqual(job2, _jobDB.LoadJob(job2.UniqueStr), true); 
			if ((job3 != null)) CheckJobEqual(job3, _jobDB.LoadJob(job3.UniqueStr), true);

			var jobsAndExtra = _jobDB.LoadJobs();
			var jobs = jobsAndExtra.Jobs.ToDictionary(x => x.UniqueStr, x => x);
			Assert.Equal(schId == null ? "" : schId, jobsAndExtra.LatestScheduleId);
			Assert.Equal(extraParts == null ? new Dictionary<string, int>() : extraParts,
			 jobsAndExtra.ExtraParts);

			if (job2 == null) {
				Assert.Equal(1, jobs.Count);
			} else if (job3 == null) {
				Assert.Equal(2, jobs.Count);
			} else {
				Assert.Equal(3, jobs.Count);
			}
			Assert.True(jobs.ContainsKey(job1.UniqueStr));
			if ((job2 != null)) Assert.True(jobs.ContainsKey(job2.UniqueStr)); 
			if ((job3 != null)) Assert.True(jobs.ContainsKey(job3.UniqueStr));

			CheckJobEqual(job1, jobs[job1.UniqueStr], true);
			if ((job2 != null)) CheckJobEqual(job2, jobs[job2.UniqueStr], true);			
			if ((job3 != null)) CheckJobEqual(job3, jobs[job3.UniqueStr], true);
		}

    private void CheckJobsDate(JobPlan job1, JobPlan job2, JobPlan job3)
    {
        var now = DateTime.UtcNow;

        //job1 is from -10 minutes to now
        //job2 is from -10 hours to -2 hours
        //job3 is the same as job1

        //overlap the start of job1: note if job1 != null then job3 also != null
        var justJob1_3 = _jobDB.LoadJobs(now.AddMinutes(-20), now.AddMinutes(-5));
        if (job1 == null) {
            Assert.Equal(justJob1_3.Count, 0);
        } else if (job3 == null) {
          Assert.Equal(justJob1_3.Count, 1);
          CheckJobEqual(job1, justJob1_3[job1.UniqueStr], true);
        } else {
            Assert.Equal(justJob1_3.Count, 2);
            CheckJobEqual(job1, justJob1_3[job1.UniqueStr], true);
            CheckJobEqual(job3, justJob1_3[job3.UniqueStr], true);
        }

        //overlap the end of job1
        var justJob1a = _jobDB.LoadJobs(now.AddMinutes(-5), now.AddMinutes(10));
        if (job1 == null) {
          Assert.Equal(justJob1a.Count, 0);
        } else if (job3 == null) {
          Assert.Equal(justJob1a.Count, 1);
          CheckJobEqual(job1, justJob1a[job1.UniqueStr], true);
        } else {
            Assert.Equal(justJob1_3.Count, 2);
            CheckJobEqual(job1, justJob1a[job1.UniqueStr], true);
            CheckJobEqual(job3, justJob1a[job3.UniqueStr], true);
        }

        //not overlapping
        Assert.Equal(_jobDB.LoadJobs(now.AddMinutes(-70), now.AddMinutes(-40)).Count, 0);

        //everything
        var all = _jobDB.LoadJobs(now.AddHours(-6), now.AddMinutes(10));
        int cnt = 0;
        if (job1 != null) {
          CheckJobEqual(job1, all[job1.UniqueStr], true);
          cnt += 1;
        }
        if (job2 != null) {
          CheckJobEqual(job2, all[job2.UniqueStr], true);
          cnt += 1;
        }
        if (job3 != null) {
          CheckJobEqual(job3, all[job3.UniqueStr], true);
          cnt += 1;
        }
        Assert.Equal(cnt, all.Count);
    }

    private void CheckSimStationUse(IEnumerable<SimulatedStationUtilization> simStations)
    {
        var now = DateTime.UtcNow;
        var fromDb = ToList(_jobDB.LoadSimulatedStationUse(now.AddMinutes(-500), now.AddMinutes(10)));
        var actualSims = ToList(simStations);

        Assert.Equal(fromDb.Count, actualSims.Count);

        fromDb.Sort((x, y) => x.SimulationId.CompareTo(y.SimulationId));
        actualSims.Sort((x, y) => x.SimulationId.CompareTo(y.SimulationId));

        for (int i = 0; i < fromDb.Count; i++) {
            var db = fromDb[i];
            var sim = actualSims[i];
            Assert.Equal(db.SimulationId, sim.SimulationId);
            Assert.Equal(db.StationGroup, sim.StationGroup);
            Assert.Equal(db.StationNum, sim.StationNum);
            Assert.Equal(db.StartUTC, sim.StartUTC);
            Assert.Equal(db.EndUTC, sim.EndUTC);
            Assert.Equal(db.UtilizationTime, sim.UtilizationTime);
			Assert.Equal(db.PlannedDownTime, sim.PlannedDownTime);
        }
    }

		private static List<T> ToList<T>(IEnumerable<T> l)
		{
			var ret = new List<T>();
			foreach (T k in l)
				ret.Add(k);
			return ret;
		}
		
		public static void CheckHoldEqual(JobHoldPattern h1, JobHoldPattern h2)
		{
			
			DateTime d1 = h1.HoldUnholdPatternStartUTC;
			DateTime d2 = h1.HoldUnholdPatternStartUTC;
			d1 = new DateTime(d1.Year, d1.Month, d1.Day, d1.Hour, d1.Minute, d1.Second);
			d2 = new DateTime(d2.Year, d2.Month, d2.Day, d2.Hour, d2.Minute, d2.Second);
			
			Assert.Equal(h1.UserHold, h2.UserHold);
			Assert.Equal(h1.ReasonForUserHold, h2.ReasonForUserHold);
			Assert.Equal(d1, d2);
			Assert.Equal(h1.HoldUnholdPatternRepeats, h2.HoldUnholdPatternRepeats);
			Assert.Equal(h1.HoldUnholdPattern.Count, h2.HoldUnholdPattern.Count);
			for (int i = 0; i < h1.HoldUnholdPattern.Count; i++) {
				Assert.Equal(h1.HoldUnholdPattern[i], h2.HoldUnholdPattern[i]);
			}
		}

		public static void CheckPlanEqual(JobPlan job1, JobPlan job2, bool checkHolds)
		{
			Assert.NotNull(job1);
			Assert.NotNull(job2);
			
			Assert.Equal(job1.PartName, job2.PartName);
			Assert.Equal(job1.UniqueStr, job2.UniqueStr);
			Assert.Equal(job1.Priority, job2.Priority);
			Assert.Equal(job1.NumProcesses, job2.NumProcesses);
			Assert.Equal(job1.CreateMarkerData, job2.CreateMarkerData);
      Assert.Equal(job1.RouteStartingTimeUTC, job2.RouteStartingTimeUTC);
      Assert.Equal(job1.RouteEndingTimeUTC, job2.RouteEndingTimeUTC);
      Assert.Equal(job1.Archived, job2.Archived);
      Assert.Equal(job1.JobCopiedToSystem, job2.JobCopiedToSystem);
      EqualSort(job1.ScheduledBookingIds, job2.ScheduledBookingIds);
      Assert.Equal(job1.ScheduleId, job2.ScheduleId);

			CheckInspEqual(job1.GetInspections (), job2.GetInspections ());
			
			if (checkHolds)
				CheckHoldEqual(job1.HoldEntireJob, job2.HoldEntireJob);
			
			for (int proc = 1; proc <= job1.NumProcesses; proc++) {
				Assert.Equal(job1.GetNumPaths(proc), job2.GetNumPaths(proc));
			}
			
			for (int path = 1; path <= job1.GetNumPaths(1); path++) {
          Assert.Equal(job1.GetPlannedCyclesOnFirstProcess(path), job2.GetPlannedCyclesOnFirstProcess(path));
			}

			for (int proc = 1; proc <= job1.NumProcesses; proc++) {
				for (int path = 1; path <= job1.GetNumPaths(proc); path++) {
					Assert.Equal(job1.GetSimulatedStartingTimeUTC(proc, path),
                       job2.GetSimulatedStartingTimeUTC(proc, path));
					
					Assert.Equal(job1.PartsPerPallet(proc, path),
                       job2.PartsPerPallet(proc, path));
					
					Assert.Equal(job1.GetPathGroup(proc, path),
                       job2.GetPathGroup(proc, path));

          Assert.Equal(job1.GetSimulatedProduction(proc, path), job2.GetSimulatedProduction(proc, path));
          Assert.Equal(job1.GetSimulatedAverageFlowTime(proc, path), job2.GetSimulatedAverageFlowTime(proc, path));
					
					EqualSort(job1.LoadStations(proc, path), job2.LoadStations(proc, path));
					EqualSort(job1.UnloadStations(proc, path), job2.UnloadStations(proc, path));
          EqualSort(job1.PlannedPallets(proc, path), job2.PlannedPallets(proc, path));
					
					EqualSort(job1.PlannedFixtures(proc, path), job2.PlannedFixtures(proc, path));
					
					if (checkHolds) {
						CheckHoldEqual(job1.HoldMachining(proc, path), job2.HoldMachining(proc, path));
						CheckHoldEqual(job1.HoldLoadUnload(proc, path), job2.HoldLoadUnload(proc, path));
					}	


					var e1 = job1.GetMachiningStop(proc, path).GetEnumerator();
					var e2 = job2.GetMachiningStop(proc, path).GetEnumerator();

					while (e1.MoveNext()) {
						if (!e2.MoveNext()) {
                Assert.True(false, "Unequal number of routes");
						}

						JobMachiningStop route1 = e1.Current;
						JobMachiningStop route2 = e2.Current;

						if (route1 == null) {
							Assert.Null(route2);
						} else {
							Assert.NotNull(route2);
              Assert.Equal(route1.ExpectedCycleTime, route2.ExpectedCycleTime);
							EqualSort(route1.AllPrograms(), route2.AllPrograms());
              Assert.Equal(route1.Tools, route2.Tools);
						}
					}
				}
			}
		}

		public static void CheckJobEqual(JobPlan job1, JobPlan job2, bool checkHolds)
		{
			CheckPlanEqual(job1, job2, checkHolds);
		}

		public static void CheckInspEqual(IEnumerable<JobInspectionData> i1, IEnumerable<JobInspectionData> i2)
		{
			var i2Copy = new List<JobInspectionData>(i2);
			foreach (var j1 in i1) {
				foreach (var j2 in i2Copy) {
					if (j1.InspectionType == j2.InspectionType
					    && j1.Counter == j2.Counter
					    && j1.MaxVal == j2.MaxVal
					    && j1.TimeInterval == j2.TimeInterval
					    && j1.RandomFreq == j2.RandomFreq
						&& j1.InspectSingleProcess == j2.InspectSingleProcess)
					{
						i2Copy.Remove(j2);
						goto found;
					}
				}

				Assert.True(false, "Unable to find " + j1.InspectionType + " " + j1.Counter);

			found:;
			}

			if (i2Copy.Count > 0) {
          Assert.True(false, "i2 has extra stuff");
			}
		}

    public static void EqualSort<T>(IEnumerable<T> e1, IEnumerable<T> e2) {
        var lst1 = new List<T>(e1);
        lst1.Sort();
        var lst2 = new List<T>(e2);
        lst2.Sort();
        Assert.Equal(lst1, lst2);
    }
	}
}