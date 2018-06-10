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
import Divider from '@material-ui/core/Divider';
import withStyles from '@material-ui/core/styles/withStyles';
import * as im from 'immutable';
import { createSelector } from 'reselect';
import DocumentTitle from 'react-document-title';
import Button from '@material-ui/core/Button';

import { LoadStationAndQueueData, selectLoadStationAndQueueProps } from '../../data/load-station';
import { MaterialDialog, InProcMaterial, WhiteboardRegion, MaterialDialogProps } from './Material';
import * as api from '../../data/api';
import * as routes from '../../data/routes';
import * as guiState from '../../data/gui-state';
import { Store, connect, mkAC, AppActionBeforeMiddleware } from '../../store/store';
import * as matDetails from '../../data/material-details';
import { MaterialSummary } from '../../data/events';
import SelectWorkorderDialog from './SelectWorkorder';
import SetSerialDialog from './EnterSerial';
import SelectInspTypeDialog from './SelectInspType';

const palletStyles = withStyles(() => ({
  palletContainerFill: {
    width: '100%',
    position: 'relative' as 'relative',
    flexGrow: 1,
  },
  palletContainerScroll: {
    width: '100%',
    position: 'relative' as 'relative',
    minHeight: '12em',
  },
  labelContainer: {
    position: 'absolute' as 'absolute',
    top: '4px',
    left: '4px',
  },
  label: {
    color: 'rgba(0,0,0,0.5)',
    fontSize: 'small',
  },
  faceContainer: {
    marginLeft: '4em',
    marginRight: '4em',
  },
}));

export const PalletColumn = palletStyles<LoadStationProps>(props => {
  let palletClass: string;
  if (props.fillViewPort) {
    palletClass = props.classes.palletContainerFill;
  } else {
    palletClass = props.classes.palletContainerScroll;
  }

  const maxFace = props.data.face.map((m, face) => face).max();
  const palLabel = "Pallet " + (props.data.pallet ? props.data.pallet.pallet : "");

  let palDetails: JSX.Element;
  if (props.data.face.size === 1) {
    const mat = props.data.face.first();
    palDetails = (
      <WhiteboardRegion label={palLabel} spaceAround>
        { (mat || []).map((m, idx) =>
          <InProcMaterial key={idx} mat={m} onOpen={props.openMat}/>)
        }
      </WhiteboardRegion>
    );
  } else {
    palDetails = (
      <>
        <div className={props.classes.labelContainer}>
          <span className={props.classes.label}>{palLabel}</span>
        </div>
        <div className={props.classes.faceContainer}>
          {
            props.data.face.toSeq().sortBy((data, face) => face).map((data, face) =>
              <div key={face}>
                <WhiteboardRegion label={"Face " + face.toString()} spaceAround>
                  { data.map((m, idx) =>
                    <InProcMaterial key={idx} mat={m} onOpen={props.openMat}/>)
                  }
                </WhiteboardRegion>
                {face === maxFace ? undefined : <Divider key={1}/>}
              </div>
            ).valueSeq()
          }
        </div>
      </>
    );
  }

  return (
    <>
      <WhiteboardRegion label="Raw Material" spaceAround>
        { props.data.castings.map((m, idx) =>
          <InProcMaterial key={idx} mat={m} onOpen={props.openMat}/>)
        }
      </WhiteboardRegion>
      <Divider/>
      <div className={palletClass}>
        {palDetails}
      </div>
      <Divider/>
      <WhiteboardRegion label="Completed Material"/>
    </>
  );
});

export interface LoadMatDialogProps extends MaterialDialogProps {
  readonly openSelectWorkorder: (mat: matDetails.MaterialDetail) => void;
  readonly openSetSerial: () => void;
  readonly openForceInspection: () => void;
}

export function LoadMatDialog(props: LoadMatDialogProps) {
  function openAssignWorkorder() {
    if (!props.display_material) {
      return;
    }
    props.openSelectWorkorder(props.display_material);
  }
  return (
    <MaterialDialog
      display_material={props.display_material}
      onClose={props.onClose}
      buttons={
        <>
          <Button color="primary" onClick={props.openSetSerial}>
            {
              props.display_material && props.display_material.serial ?
                "Change Serial"
                : "Assign Serial"
            }
          </Button>
          <Button color="primary" onClick={props.openForceInspection}>
            Signal Inspection
          </Button>
          <Button color="primary" onClick={openAssignWorkorder}>
            {
              props.display_material && props.display_material.workorderId ?
                "Change Workorder"
                : "Assign Workorder"
            }
          </Button>
        </>
      }
    />
  );
}

const ConnectedMaterialDialog = connect(
  st => ({
    display_material: st.MaterialDetails.material,
  }),
  {
    onClose: mkAC(matDetails.ActionType.CloseMaterialDialog),
    openSelectWorkorder: (mat: matDetails.MaterialDetail) => [
      {
        type: guiState.ActionType.SetWorkorderDialogOpen,
        open: true
      },
      matDetails.loadWorkorders(mat),
    ] as AppActionBeforeMiddleware,
    openSetSerial: () =>
      ({
        type: guiState.ActionType.SetSerialDialogOpen,
        open: true
      }),
    openForceInspection: () =>
      ({
        type: guiState.ActionType.SetInspTypeDialogOpen,
        open: true
      })
  }
)(LoadMatDialog);

const loadStyles = withStyles(() => ({
  mainFillViewport: {
    'height': 'calc(100vh - 64px - 2.5em)',
    'display': 'flex',
    'padding': '8px',
    'width': '100%',
  },
  mainScrollable: {
    'display': 'flex',
    'padding': '8px',
    'width': '100%',
  },
  palCol: {
    'flexGrow': 1,
    'display': 'flex',
    'flexDirection': 'column' as 'column',
  },
  queueCol: {
    'width': '16em',
    'padding': '8px',
    'display': 'flex',
    'flexDirection': 'column' as 'column',
    'borderLeft': '1px solid rgba(0, 0, 0, 0.12)',
  },
}));

export interface LoadStationProps {
  readonly fillViewPort: boolean;
  readonly data: LoadStationAndQueueData;
  openMat: (m: Readonly<MaterialSummary>) => void;
}

export const LoadStation = loadStyles<LoadStationProps>(props => {
  const palProps = {...props, classes: undefined};

  let queues = props.data.queues
    .toSeq()
    .sortBy((mats, q) => q)
    .map((mats, q) => ({
      label: q,
      material: mats,
    }))
    .valueSeq();

  let cells = queues;
  if (props.data.free) {
    cells = im.Seq([{
      label: "In Process Material",
      material: props.data.free,
    }]).concat(queues);
  }

  const col1 = cells.take(2);
  const col2 = cells.skip(2).take(2);

  return (
    <DocumentTitle title={"Load " + props.data.loadNum.toString() + " - FMS Insight"}>
      <main className={props.fillViewPort ? props.classes.mainFillViewport : props.classes.mainScrollable}>
        <div className={props.classes.palCol}>
          <PalletColumn {...palProps}/>
        </div>
        {
          col1.size === 0 ? undefined :
          <div className={props.classes.queueCol}>
            {
              col1.map((mat, idx) => (
                <WhiteboardRegion key={idx} label={mat.label}>
                  { mat.material.map((m, matIdx) =>
                    <InProcMaterial key={matIdx} mat={m} onOpen={props.openMat}/>)
                  }
                </WhiteboardRegion>
              ))
            }
          </div>
        }
        {
          col2.size === 0 ? undefined :
          <div className={props.classes.queueCol}>
            {
              col2.map((mat, idx) => (
                <WhiteboardRegion key={idx} label={mat.label}>
                  { mat.material.map((m, matIdx) =>
                    <InProcMaterial key={matIdx} mat={m} onOpen={props.openMat}/>)
                  }
                </WhiteboardRegion>
              ))
            }
          </div>
        }
        <SelectWorkorderDialog/>
        <SetSerialDialog/>
        <SelectInspTypeDialog/>
        <ConnectedMaterialDialog/>
      </main>
    </DocumentTitle>
  );
});

const buildLoadData = createSelector(
  (st: Store) => st.Current.current_status,
  (st: Store) => st.Route,
  (curStatus: Readonly<api.ICurrentStatus>, route: routes.State): LoadStationAndQueueData => {
    return selectLoadStationAndQueueProps(
        route.selected_load_id,
        route.load_queues,
        route.load_free_material,
        curStatus);
  }
);

export default connect(
  (st: Store) => ({
    data: buildLoadData(st),
  }),
  {
    openMat: matDetails.openMaterialDialog,
  }
)(LoadStation);