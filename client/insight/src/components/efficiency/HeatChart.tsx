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
import * as im from 'immutable';
import { format, startOfDay } from 'date-fns';
import { HeatmapSeries,
         XAxis,
         YAxis,
         Hint,
         FlexibleWidthXYPlot,
       } from 'react-vis';
import Card, { CardHeader, CardContent } from 'material-ui/Card';
import * as numerable from 'numeral';
import Select from 'material-ui/Select';
import { MenuItem } from 'material-ui/Menu';

import * as gui from '../../data/gui-state';
import * as events from '../../data/events';

export interface HeatChartPoint {
  readonly x: Date;
  readonly y: string;
  readonly color: number;
}

export interface HeatChartProps {
  readonly points: ReadonlyArray<HeatChartPoint>;
  readonly color_label: string;
}

interface HeatChartState {
  readonly selected_point?: HeatChartPoint;
}

const formatHint = (label: string) => (p: HeatChartPoint) => {
  return [
    { title: "Station", value: p.y },
    { title: "Day", value: p.x.toDateString() },
    { title: label, value: numerable(p.color).format('0.0%')}
  ];
};

function tick_format(d: Date): string {
  return format(d, "ddd MMM D");
}

export class HeatChart extends React.PureComponent<HeatChartProps, HeatChartState> {
  state: HeatChartState = {};

  render() {
    return (
      <FlexibleWidthXYPlot
        height={400}
        xType="ordinal"
        yType="ordinal"
        margin={{bottom: 60, left: 100}}
      >
        <XAxis tickFormat={tick_format} tickLabelAngle={-45}/>
        <YAxis/>
        <HeatmapSeries
          data={this.props.points}
          onValueMouseOver={(pt: HeatChartPoint) => this.setState({selected_point: pt})}
          onValueMouseOut={() => this.setState({selected_point: undefined})}
        />
        {
          this.state.selected_point === undefined ? undefined :
            <Hint value={this.state.selected_point} format={formatHint(this.props.color_label)}/>
        }
      </FlexibleWidthXYPlot>
    );
  }
}

export function binPointsByDay(
    byPartThenStat: im.Map<string, im.Map<string, ReadonlyArray<events.CycleData>>>,
    extractActual: (c: events.CycleData) => number
  ): ReadonlyArray<HeatChartPoint> {

  return byPartThenStat.valueSeq()
    .flatMap(byStation => (
      byStation.toSeq()
      .map((points, station) =>
        im.Seq(points).map(point => ({
          x: startOfDay(point.x),
          y: station,
          color: extractActual(point)
        }))
      )
      .valueSeq()
      .flatMap(x => x)
    ))
    .groupBy(p => ({x: p.x, y: p.y}))
    .map((points, {x, y}) => ({
      x,
      y,
      color: points.reduce((sum, p) => sum + p.color, 0)
    }))
    .valueSeq()
    .toArray();
}

export interface SelectableHeatChartProps {
  readonly icon: JSX.Element;
  readonly card_label: string;
  readonly color_label: string;
  readonly planned_or_actual: gui.PlannedOrActual;
  readonly setType: (p: gui.PlannedOrActual) => void;

  readonly points: ReadonlyArray<HeatChartPoint>;
}

export function SelectableHeatChart(props: SelectableHeatChartProps) {
  return (
    <Card raised>
      <CardHeader
        title={
          <div style={{display: 'flex', flexWrap: 'wrap', alignItems: 'center'}}>
            {props.icon}
            <div style={{marginLeft: '10px', marginRight: '3em'}}>
              {props.card_label}
            </div>
            <div style={{flexGrow: 1}}/>
            <Select
              autoWidth
              displayEmpty
              value={props.planned_or_actual}
              onChange={e => props.setType(e.target.value as gui.PlannedOrActual)}
            >
              <MenuItem
                key={gui.PlannedOrActual.Actual}
                value={gui.PlannedOrActual.Actual}
              >
                Actual
              </MenuItem>
              <MenuItem
                key={gui.PlannedOrActual.Planned}
                value={gui.PlannedOrActual.Planned}
              >
                Planned
              </MenuItem>
              <MenuItem
                key={gui.PlannedOrActual.PlannedMinusActual}
                value={gui.PlannedOrActual.PlannedMinusActual}
              >
                Planned minus Actual
              </MenuItem>
            </Select>
          </div>}
      />
      <CardContent>
        <HeatChart
          points={props.points}
          color_label={props.color_label}
        />
      </CardContent>
    </Card>
  );
}