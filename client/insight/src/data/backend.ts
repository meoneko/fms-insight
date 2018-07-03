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

import * as api from './api';
import * as im from 'immutable';

export interface JobAPI {
  history(startUTC: Date, endUTC: Date): Promise<Readonly<api.IHistoricData>>;
  currentStatus(): Promise<Readonly<api.ICurrentStatus>>;
  mostRecentUnfilledWorkordersForPart(part: string): Promise<ReadonlyArray<Readonly<api.IPartWorkorder>>>;

  removeMaterialFromAllQueues(materialId: number): Promise<void>;
  setMaterialInQueue(materialId: number, queue: api.QueuePosition): Promise<void>;
  addUnprocessedMaterialToQueue(
    jobUnique: string, lastCompletedProcess: number, queue: string, pos: number, serial: string
  ): Promise<void>;
}

export interface ServerAPI {
  fMSInformation(): Promise<Readonly<api.IFMSInfo>>;
}

export interface LogAPI {
  get(startUTC: Date, endUTC: Date): Promise<ReadonlyArray<Readonly<api.ILogEntry>>>;
  recent(lastSeenCounter: number): Promise<ReadonlyArray<Readonly<api.ILogEntry>>>;
  logForMaterial(materialID: number): Promise<ReadonlyArray<Readonly<api.ILogEntry>>>;
  logForSerial(serial: string): Promise<ReadonlyArray<Readonly<api.ILogEntry>>>;
  getWorkorders(ids: string[]): Promise<ReadonlyArray<Readonly<api.IWorkorderSummary>>>;

  setInspectionDecision(inspType: string, mat: api.LogMaterial, inspect: boolean): Promise<Readonly<api.ILogEntry>>;
  recordInspectionCompleted(insp: api.NewInspectionCompleted): Promise<Readonly<api.ILogEntry>>;
  recordWashCompleted(insp: api.NewWash): Promise<Readonly<api.ILogEntry>>;
  setWorkorder(workorder: string, mat: api.LogMaterial): Promise<Readonly<api.ILogEntry>>;
  setSerial(serial: string, mat: api.LogMaterial): Promise<Readonly<api.ILogEntry>>;
}

export let ServerBackend: ServerAPI = new api.ServerClient();
export let JobsBackend: JobAPI = new api.JobsClient();
export let LogBackend: LogAPI = new api.LogClient();

export function initMockBackend(
  curSt: Readonly<api.ICurrentStatus>,
  jobs: Readonly<api.IHistoricData>,
  workorders: Map<string, ReadonlyArray<Readonly<api.IPartWorkorder>>>,
  events: Readonly<api.ILogEntry>[],
) {
  ServerBackend = {
    fMSInformation() {
      return Promise.resolve({
        name: "Sample",
        version: "1.0.0"
      });
    }
  };

  JobsBackend = {
    history(startUTC: Date, endUTC: Date): Promise<Readonly<api.IHistoricData>> {
      return Promise.resolve(jobs);
    },
    currentStatus(): Promise<Readonly<api.ICurrentStatus>> {
      return Promise.resolve(curSt);
    },
    mostRecentUnfilledWorkordersForPart(part: string): Promise<ReadonlyArray<Readonly<api.IPartWorkorder>>> {
      return Promise.resolve(workorders.get(part) || []);
    },

    removeMaterialFromAllQueues(materialId: number): Promise<void> {
      // do nothing
      return Promise.resolve();
    },
    setMaterialInQueue(materialId: number, queue: api.QueuePosition): Promise<void> {
      // do nothing
      return Promise.resolve();
    },
    addUnprocessedMaterialToQueue(
      jobUnique: string, lastCompletedProcess: number, queue: string, pos: number, serial: string
    ): Promise<void> {
      // do nothing
      return Promise.resolve();
    }
  };

  const serialsToMatId =
    im.Map(
      im.Seq(events)
      .filter(e => e.type === api.LogType.PartMark)
      .flatMap(e => e.material.map(m => [e.result, m.id] as [string, number]))
    );

  LogBackend = {
    get(startUTC: Date, endUTC: Date): Promise<ReadonlyArray<Readonly<api.ILogEntry>>> {
      return Promise.resolve(
        im.Seq(events)
        .filter(e => e.endUTC >= startUTC && e.endUTC <= endUTC)
        .toArray()
      );
    },
    recent(lastSeenCounter: number): Promise<ReadonlyArray<Readonly<api.ILogEntry>>> {
      // no recent events, everything is static
      return Promise.resolve([]);
    },
    logForMaterial(materialID: number): Promise<ReadonlyArray<Readonly<api.ILogEntry>>> {
      return Promise.resolve(
        im.Seq(events)
        .filter(e => im.Seq(e.material).some(m => m.id === materialID))
        .toArray()
      );
    },
    logForSerial(serial: string): Promise<ReadonlyArray<Readonly<api.ILogEntry>>> {
      var mId = serialsToMatId.get(serial);
      if (mId) {
        return this.logForMaterial(mId);
      } else {
        return Promise.resolve([]);
      }
    },
    getWorkorders(ids: string[]): Promise<ReadonlyArray<Readonly<api.IWorkorderSummary>>> {
      // no workorder summaries
      return Promise.resolve([]);
    },

    setInspectionDecision(inspType: string, mat: api.LogMaterial, inspect: boolean): Promise<Readonly<api.ILogEntry>> {
      const evt = {
        counter: 0,
        material: [mat],
        pal: "",
        type: api.LogType.Inspection,
        startofcycle: false,
        endUTC: new Date(),
        loc: 'Inspection',
        locnum: 1,
        result: inspect.toString(),
        program: '',
        elapsed: '00:00:00',
        active: '00:00:00',
        details: {
          "InspectionType": inspType,
        }
      };
      events.push(evt);
      return Promise.resolve(evt);
    },
    recordInspectionCompleted(insp: api.NewInspectionCompleted): Promise<Readonly<api.ILogEntry>> {
      const evt: api.ILogEntry = {
        counter: 0,
        material: [insp.material],
        pal: "",
        type: api.LogType.InspectionResult,
        startofcycle: false,
        endUTC: new Date(),
        loc: 'InspectionComplete',
        locnum: insp.inspectionLocationNum,
        result: insp.success.toString(),
        program: insp.inspectionType,
        elapsed: insp.elapsed,
        active: insp.active,
        details: insp.extraData
      };
      events.push(evt);
      return Promise.resolve(evt);
    },
    recordWashCompleted(wash: api.NewWash): Promise<Readonly<api.ILogEntry>> {
      const evt: api.ILogEntry = {
        counter: 0,
        material: [wash.material],
        pal: "",
        type: api.LogType.Wash,
        startofcycle: false,
        endUTC: new Date(),
        loc: 'Wash',
        locnum: wash.washLocationNum,
        result: '',
        program: '',
        elapsed: wash.elapsed,
        active: wash.active,
        details: wash.extraData
      };
      events.push(evt);
      return Promise.resolve(evt);
    },
    setWorkorder(workorder: string, mat: api.LogMaterial): Promise<Readonly<api.ILogEntry>> {
      const evt: api.ILogEntry =  {
        counter: 0,
        material: [mat],
        pal: "",
        type: api.LogType.OrderAssignment,
        startofcycle: false,
        endUTC: new Date(),
        loc: 'OrderAssignment',
        locnum: 1,
        result: workorder,
        program: '',
        elapsed: '00:00:00',
        active: '00:00:00'
      };
      events.push(evt);
      return Promise.resolve(evt);
    },
    setSerial(serial: string, mat: api.LogMaterial): Promise<Readonly<api.ILogEntry>> {
      const evt: api.ILogEntry = {
        counter: 0,
        material: [mat],
        pal: "",
        type: api.LogType.PartMark,
        startofcycle: false,
        endUTC: new Date(),
        loc: 'Mark',
        locnum: 1,
        result: serial,
        program: '',
        elapsed: '00:00:00',
        active: '00:00:00'
      };
      events.push(evt);
      return Promise.resolve(evt);
    },
  };
}