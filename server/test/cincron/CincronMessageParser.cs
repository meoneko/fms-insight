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
using System.Text;
using Xunit;
using Cincron;
using FluentAssertions;

namespace MachineWatchTest.Cincron
{
  public class MessageParseTest
  {
    private static Newtonsoft.Json.JsonSerializerSettings jsonSettings = new Newtonsoft.Json.JsonSerializerSettings()
    {
      TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
      Formatting = Newtonsoft.Json.Formatting.Indented
    };

    private TimeZoneInfo centralZone;

    public MessageParseTest()
    {
      try
      {
        centralZone = TimeZoneInfo.FindSystemTimeZoneById("US/Central");
      }
      catch (TimeZoneNotFoundException)
      {
        centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
      }
    }

    private DateTime SetCurYear(DateTime d)
    {
      return new DateTime(DateTime.Now.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, DateTimeKind.Utc);
    }

    private List<CincronMessage> LoadExpected()
    {
      return
        Newtonsoft.Json.JsonConvert.DeserializeObject<List<CincronMessage>>(System.IO.File.ReadAllText("../../../cincron/sample-messages.json"), jsonSettings)
        .Select(m =>
        {
          m.TimeUTC = SetCurYear(m.TimeUTC);
          m.TimeOfFirstEntryInLogFileUTC = SetCurYear(m.TimeOfFirstEntryInLogFileUTC);
          return m;
        })
        .ToList();
    }

    [Fact]
    public void ParseAllMessages()
    {
      var msg = MessageParser.ExtractMessages("../../../cincron/sample-messages", 0, "", centralZone);
      msg.Should().BeEquivalentTo(LoadExpected());
    }

    [Fact]
    public void ParseMiddle()
    {
      var msg = MessageParser.ExtractMessages("../../../cincron/sample-messages", 363143, "Jan 25 01:13:12 ABCDEF CINCRON[5889]: stn002--I10402:Control Data for Work Unit 22 Updated   [STEP_NO = 2]", centralZone);
      var expected = LoadExpected();
      msg.Should().BeEquivalentTo(expected.GetRange(2533, 2233));
    }

    [Fact]
    public void Rollover()
    {
      var msg = MessageParser.ExtractMessages("../../../cincron/sample-messages", 363143, "bad match", centralZone);
      var expected = LoadExpected();
      // since offset doesn't match, should load everything from beginning
      msg.Should().BeEquivalentTo(expected);
    }

    /*
    [Fact]
    public void ProcessEvents()
    {
      var logConn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
      logConn.Open();
      var log = new BlackMaple.MachineFramework.JobLogDB(logConn);
      log.CreateTables(firstSerialOnEmpty: null);

      System.IO.File.Copy("../../../cincron/june-sample-messages-partial", "../../../cincron/june-input", overwrite: true);

      var w = new MessageWatcher("../../../cincron/june-input", log, new BlackMaple.MachineFramework.FMSSettings());
      w.CheckMessages(null, null);

      var evts = log.GetLog(0).Where(e => e.Pallet == "18").OrderBy(e => e.EndTimeUTC).ToList();
      System.IO.File.WriteAllText("../../../cincron/june-sample-events-partial.json", Newtonsoft.Json.JsonConvert.SerializeObject(evts, jsonSettings));

      System.IO.File.Copy("../../../cincron/june-sample-messages", "../../../cincron/june-input", overwrite: true);
      w.CheckMessages(null, null);

      evts = log.GetLog(0).Where(e => e.Pallet == "18").OrderBy(e => e.EndTimeUTC).ToList();
      System.IO.File.WriteAllText("../../../cincron/june-sample-events-full.json", Newtonsoft.Json.JsonConvert.SerializeObject(evts, jsonSettings));
    }
    */
  }
}