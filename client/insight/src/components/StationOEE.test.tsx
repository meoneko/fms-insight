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

import * as React from 'react';
import { StationOEE, stationHoursInLastWeek } from './StationOEE';
import * as im from 'immutable';
import { shallow } from 'enzyme';

it('calculates station hours', () => {
  const date = new Date();
  const hours = [
    {date, station: 'zzz', hours: 2},
    {date, station: 'abc', hours: 4},
    {date, station: 'abc', hours: 10},
    {date, station: 'zzz', hours: 5},
    {date, station: 'zzz', hours: 4},
    {date, station: 'abc', hours: 11},
  ];

  expect(stationHoursInLastWeek(im.List(hours)).toArray()).toEqual(
    [
      {station: 'abc', hours: 4 + 10 + 11},
      {station: 'zzz', hours: 2 + 5 + 4},
    ]
  );
});

it('displays station hours', () => {
  const hours = im.Seq([
    {station: 'abc', hours: 40},
    {station: 'zzz', hours: 3}
  ]);
  const val = shallow(<StationOEE system_active_hours_per_week={100} station_active_hours_past_week={hours}/>);
  expect(val).toMatchSnapshot('station oee table');
});
