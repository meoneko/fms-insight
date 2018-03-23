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
import { withStyles } from 'material-ui';
import { addHours } from 'date-fns';
import Grid from 'material-ui/Grid';
import Card, { CardContent, CardHeader, CardActions } from 'material-ui/Card';
import Button from 'material-ui/Button';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';

import { MaterialSummary } from '../../data/events';
import { Store } from '../../data/store';
import { MatSummary, MaterialDetailTitle, MaterialDetailContent } from './Material';
import * as matDetails from '../../data/material-details';

const matListStyles = withStyles(theme => ({
  summaryItem: {
    paddingTop: 6,
    paddingBottom: 6,
  },
}));

export interface InspectionListProps {
  readonly recent_inspections: ReadonlyArray<MaterialSummary>;
  readonly focusInspectionType: string;
  // tslint:disable-next-line:no-any
  readonly openMat: (mat: MaterialSummary) => any;
}

export const InspectionList = matListStyles<InspectionListProps>(props => {
  return (
    <ul style={{listStyle: 'none'}}>
      {
        props.recent_inspections.map((mat, i) =>
          <li key={i} className={props.classes.summaryItem}>
            <div style={{display: 'inline-block'}}>
              <MatSummary
                mat={mat}
                focusInspectionType={props.focusInspectionType}
                onOpen={props.openMat}
              />
            </div>
          </li>
        )
      }
    </ul>
  );
});

export function SelectedMaterial({mat}: {mat: matDetails.MaterialDetail}) {
  return (
    <>
      <MaterialDetailTitle partName={mat.partName}/>
      <MaterialDetailContent mat={mat}/>
    </>
  );
}

export interface InspectionProps extends InspectionListProps {
  readonly fillViewPort: boolean;
  readonly display_material: matDetails.MaterialDetail | null;

  // tslint:disable-next-line:no-any
  readonly completeInspection: (mat: matDetails.MaterialDetail, inspType: string, success: boolean) => any;
}

const inspStyles = withStyles(() => ({
  mainFillViewport: {
    'height': 'calc(100vh - 64px - 2.5em)',
    'padding': '8px',
    'width': '100%',
    'display': 'flex',
    'flex-direction': 'column' as 'column',
  },
  stretchCard: {
    'height': '100%',
    'display': 'flex',
    'flex-direction': 'column' as 'column',
  },
  stretchCardContent: {
    'overflow-y': 'auto',
    'flex-grow': 1,
  },
  mainScrollable: {
    'padding': '8px',
    'width': '100%',
  },
}));

export const Inspection = inspStyles<InspectionProps>(props => {

  function markInspComplete() {
    if (!props.display_material) {
      return;
    }

    props.completeInspection(props.display_material, "MyInspection", true);
  }

  return (
    <main className={props.fillViewPort ? props.classes.mainFillViewport : props.classes.mainScrollable}>
      <Grid container style={{flexGrow: 1}}>
        <Grid item xs={12} md={6}>
          <Card className={props.fillViewPort ? props.classes.stretchCard : undefined}>
            <CardHeader title="Recent Inspections"/>
            <CardContent className={props.fillViewPort ? props.classes.stretchCardContent : undefined}>
              <InspectionList
                recent_inspections={props.recent_inspections}
                focusInspectionType={props.focusInspectionType}
                openMat={props.openMat}
              />
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={6}>
          <Card className={props.fillViewPort ? props.classes.stretchCard : undefined}>
            <CardHeader title="Selected Material"/>
            <CardContent className={props.fillViewPort ? props.classes.stretchCardContent : undefined}>
              {props.display_material ? <SelectedMaterial mat={props.display_material}/> : undefined}
            </CardContent>
            {
              props.display_material ?
                <CardActions>
                  <Button onClick={markInspComplete}>
                    Mark Inspection Success
                  </Button>
                </CardActions>
                : undefined
            }
          </Card>
        </Grid>
      </Grid>
    </main>
  );
});

export const extractRecentInspections = createSelector(
  (st: Store) => st.Events.last30.mat_summary.matsById,
  (st: Store) => st.Route.selected_insp_type,
  (mats: im.Map<number, MaterialSummary>, inspType: string | undefined): ReadonlyArray<MaterialSummary> => {
    const cutoff = addHours(new Date(), -36);
    const allDetails = mats
      .valueSeq()
      .filter(e => e.completed_time !== undefined && e.completed_time >= cutoff);

    const filtered =
      inspType === undefined
        ? allDetails.filter(m => m.signaledInspections.length > 0)
        : allDetails.filter(m => m.signaledInspections.indexOf(inspType) >= 0);

    return filtered
      .sortBy(e => e.completed_time)
      .reverse()
      .toArray();
  }
);

export default connect(
  (st: Store) => ({
    recent_inspections: extractRecentInspections(st),
    focusInspectionType: st.Route.selected_insp_type || "",
    display_material: st.MaterialDetails.inspection_display_material || null,
  }),
  {
    openMat: matDetails.openInspectionMaterial,
    completeInspection: matDetails.completeInspection,
  }
)(Inspection);