import { LogSessionDialogComponent } from './log-session-dialog.component';

describe('LogSessionDialogComponent', () => {
  let cmp: LogSessionDialogComponent;
  let saved: any[];

  beforeEach(() => {
    cmp = new LogSessionDialogComponent();
    cmp.gameName = 'Nine Sols';
    cmp.device = 'PC';
    saved = [];
    cmp.saved.subscribe(v => saved.push(v));
  });

  it('emits saved with hours 1.5 for "1,5"', () => {
    cmp.hoursText = '1,5';
    cmp.note = 'Boss 2';
    cmp.save();
    expect(saved.length).toBe(1);
    expect(saved[0].hours).toBe(1.5);
    expect(saved[0].note).toBe('Boss 2');
  });

  it('shows the error and does not emit for "0"', () => {
    cmp.hoursText = '0';
    cmp.save();
    expect(saved.length).toBe(0);
    expect(cmp.hoursError).toBe('Enter hours between 0.1 and 24.');
    expect(cmp.canSave).toBeFalse();
  });

  it('disables saving for a future date', () => {
    cmp.hoursText = '1';
    const d = new Date();
    d.setDate(d.getDate() + 2);
    cmp.playedOn = LogSessionDialogComponent.toIsoDate(d);
    expect(cmp.canSave).toBeFalse();
    cmp.save();
    expect(saved.length).toBe(0);
  });
});
