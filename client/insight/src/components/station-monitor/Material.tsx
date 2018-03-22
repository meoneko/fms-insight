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
import { connect } from 'react-redux';
import * as jdenticon from 'jdenticon';
import Typography from 'material-ui/Typography';
import ButtonBase from 'material-ui/ButtonBase';
import Button from 'material-ui/Button';
import Tooltip from 'material-ui/Tooltip';
import WarningIcon from 'material-ui-icons/Warning';
import CheckmarkIcon from 'material-ui-icons/Check';
import Avatar from 'material-ui/Avatar';
import Paper from 'material-ui/Paper';
import { CircularProgress } from 'material-ui/Progress';
import Dialog, {
  DialogActions,
  DialogContent,
  DialogTitle,
} from 'material-ui/Dialog';
import { distanceInWordsToNow } from 'date-fns';

import * as im from 'immutable';

import * as api from '../../data/api';
import * as matDetails from '../../data/material-details';
import LogEntry from '../LogEntry';
import { Store } from '../../data/store';
import { MaterialSummary } from '../../data/events';
import { withStyles } from 'material-ui';

/*
function getPosition(el: Element) {
  const box = el.getBoundingClientRect();
  const doc = document.documentElement;
  const body = document.body;
  var clientTop  = doc.clientTop  || body.clientTop  || 0;
  var clientLeft = doc.clientLeft || body.clientLeft || 0;
  var scrollTop  = window.pageYOffset || doc.scrollTop;
  var scrollLeft = window.pageXOffset || doc.scrollLeft;
  return {
    top: box.top  + scrollTop  - clientTop,
    left: box.left + scrollLeft - clientLeft
  };
}*/

export function PartIdenticon({part}: {part: string}) {
  const iconSize = 50;
  // tslint:disable-next-line:no-any
  const icon = (jdenticon as any).toSvg(part, iconSize);

  return (
    <div
      style={{width: iconSize, height: iconSize}}
      dangerouslySetInnerHTML={{__html: icon}}
    />
  );
}

function materialAction(mat: Readonly<api.IInProcessMaterial>): string | undefined {
  switch (mat.action.type) {
    case api.ActionType.Loading:
      switch (mat.location.type) {
        case api.LocType.OnPallet:
          return "Transfer to face " + mat.action.loadOntoFace.toString();
        default:
          return "Load onto face " + mat.action.loadOntoFace.toString();
      }
    case api.ActionType.UnloadToInProcess:
    case api.ActionType.UnloadToCompletedMaterial:
      if (mat.action.unloadIntoQueue) {
        return "Unload into queue " + mat.action.unloadIntoQueue;
      } else {
        return "Unload from pallet";
      }
  }
  return undefined;
}

const matStyles = withStyles(theme => ({
  paper: {
    minWidth: '10em',
    padding: '8px'
  },
  container: {
    display: 'flex',
    textAlign: 'left',
  },
  mainContent: {
    marginLeft: '8px',
    flexGrow: 1,
  },
  rightContent: {
    marginLeft: '4px'
  },
  avatar: {
    width: '30px',
    height: '30px'
  }
}));

export interface InProcMaterialProps {
  readonly mat: Readonly<api.IInProcessMaterial>; // TODO: deep readonly
  // tslint:disable-next-line:no-any
  onOpen: (m: Readonly<api.IInProcessMaterial>) => any;
}

export const InProcMaterial = matStyles<InProcMaterialProps>(props => {
  const action = materialAction(props.mat);
  const inspections = props.mat.signaledInspections.join(", ");

  return (
    <Paper elevation={4} className={props.classes.paper}>
      <ButtonBase focusRipple onClick={() => props.onOpen(props.mat)}>
        <div className={props.classes.container}>
          <PartIdenticon part={props.mat.partName}/>
          <div className={props.classes.mainContent}>
            <Typography variant="title">
              {props.mat.partName}
            </Typography>
            <div>
              <small>Serial: {props.mat.serial ? props.mat.serial : "none"}</small>
            </div>
            {
              props.mat.workorderId === undefined ? undefined :
                <div>
                  <small>Workorder: {props.mat.workorderId}</small>
                </div>
            }
            {
              action === undefined ? undefined :
                <div>
                  <small>{action}</small>
                </div>
            }
          </div>
          <div className={props.classes.rightContent}>
            {props.mat.serial && props.mat.serial.length >= 1 ?
              <div>
                <Avatar className={props.classes.avatar}>
                  {props.mat.serial.substr(props.mat.serial.length - 1, 1)}
                </Avatar>
              </div>
              : undefined
            }
            {
              props.mat.signaledInspections.length === 0 ? undefined :
                <div>
                  <Tooltip title={inspections}>
                    <WarningIcon/>
                  </Tooltip>
                </div>
            }
          </div>
        </div>
      </ButtonBase>
    </Paper>
  );
});

export interface MaterialSummaryProps {
  readonly mat: Readonly<MaterialSummary>; // TODO: deep readonly
  readonly focusInspectionType?: string;
  // tslint:disable-next-line:no-any
  onOpen: (m: Readonly<MaterialSummary>) => any;
}

export const MatSummary = matStyles<MaterialSummaryProps>(props => {
  function colorForInspType(type: string): string {
    if (!props.focusInspectionType) {
      return "black";
    }
    if (props.focusInspectionType !== "" && props.focusInspectionType !== type) {
      return "black";
    }
    if (props.mat.completedInspections.indexOf(type) >= 0) {
      return "black";
    } else {
      return "red";
    }
  }

  let allInspCompleted: boolean;
  if (props.focusInspectionType === "") {
    allInspCompleted = im.Set(props.mat.signaledInspections).subtract(props.mat.completedInspections)
      .isEmpty();
  } else {
    allInspCompleted = props.mat.completedInspections.indexOf(props.focusInspectionType || "") >= 0;
  }

  return (
    <Paper elevation={4} className={props.classes.paper}>
      <ButtonBase
        focusRipple
        onClick={() => props.onOpen(props.mat)}
      >
        <div className={props.classes.container}>
          <PartIdenticon part={props.mat.partName}/>
          <div className={props.classes.mainContent}>
            <Typography variant="title">
              {props.mat.partName}
            </Typography>
            <div>
              <small>Serial: {props.mat.serial ? props.mat.serial : "none"}</small>
            </div>
            {
              props.mat.workorderId === undefined ? undefined :
                <div>
                  <small>Workorder: {props.mat.workorderId}</small>
                </div>
            }
            {
              props.mat.completed_time === undefined ? undefined :
                <div>
                  <small>Completed {distanceInWordsToNow(props.mat.completed_time)} ago</small>
                </div>
            }
            {
              props.mat.signaledInspections.length === 0 ? undefined :
                <div>
                  <small>Inspections: </small>
                  {
                    props.mat.signaledInspections.map((type, i) => (
                      <small key={i} style={{color: colorForInspType(type)}}>
                        {type}
                      </small>
                    ))
                  }
                </div>
            }
          </div>
          <div className={props.classes.rightContent}>
            {props.mat.serial && props.mat.serial.length >= 1 ?
              <div>
                <Avatar className={props.classes.avatar}>
                  {props.mat.serial.substr(props.mat.serial.length - 1, 1)}
                </Avatar>
              </div>
              : undefined
            }
            {
              props.focusInspectionType !== undefined && allInspCompleted ?
                <div>
                  <Tooltip title="All Inspections Completed">
                    <CheckmarkIcon/>
                  </Tooltip>
                </div>
                : undefined
            }
          </div>
        </div>
      </ButtonBase>
    </Paper>
  );
});

export interface MaterialEventProps {
  events: ReadonlyArray<Readonly<api.ILogEntry>>;
}

export function MaterialEvents(props: MaterialEventProps) {
  return (
    <ul style={{'list-style': 'none'}}>
      {
        props.events.map(e => (
          <li key={e.counter}>
            <LogEntry entry={e}/>
          </li>
        ))
      }
    </ul>
  );
}

export interface MaterialDialogProps extends matDetails.State {
  // tslint:disable-next-line:no-any
  onClose: () => any;
}

export function MaterialDialog(props: MaterialDialogProps) {
  let body: JSX.Element | undefined;
  if (props.display_material === undefined) {
    body = <p>None</p>;
  } else {
    const mat = props.display_material;
    body = (
      <>
        <DialogTitle disableTypography>
          <div style={{display: 'flex', textAlign: 'left'}}>
            <PartIdenticon part={mat.partName}/>
            <div style={{marginLeft: '8px', flexGrow: 1}}>
              <Typography variant="title">
                {mat.partName}
              </Typography>
            </div>
          </div>
        </DialogTitle>
        <DialogContent>
          <div>
            <small>Serial: {mat.serial || "none"}</small>
          </div>
          <div>
            <small>Workorder: {mat.workorderId || "none"}</small>
          </div>
          <div>
            <small>{materialAction(mat)}</small>
          </div>
          <div>
              {
                mat.signaledInspections.length === 0 ?
                  <small>Inspections: none</small> :
                  <small style={{color: "#F44336"}}>
                    Inspections: {mat.signaledInspections.length === 0 ? "none" : mat.signaledInspections.join(", ")}
                  </small>
              }
          </div>
          {props.loading_events ? <CircularProgress color="secondary"/> : <MaterialEvents events={props.events}/>}
        </DialogContent>
        <DialogActions>
          <Button onClick={props.onClose} color="primary">
            Close
          </Button>
        </DialogActions>
      </>
    );
  }
  return (
    <Dialog
      open={props.display_material !== undefined}
      onClose={props.onClose}
      maxWidth="md"
    >
      {body}
    </Dialog>

  );
}

export const ConnectedMaterialDialog = connect(
  (st: Store) => st.MaterialDetails,
  {
    onClose: () => ({
      type: matDetails.ActionType.CloseMaterialDialog
    }),
  }
)(MaterialDialog);