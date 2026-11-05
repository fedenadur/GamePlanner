import { TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
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

  it('pre-fills from the initial session on init', () => {
    cmp.initial = { hours: 2.5, note: 'n', playedOn: '2026-01-02', device: 'Switch' };
    cmp.ngOnInit();
    expect(cmp.hoursText).toBe('2.5');
    expect(cmp.note).toBe('n');
    expect(cmp.playedOn).toBe('2026-01-02');
    expect(cmp.selectedDevice).toBe('Switch');
  });

  it('emits the chosen device when devices are offered', () => {
    cmp.devices = ['PC', 'General'];
    cmp.ngOnInit();
    cmp.selectedDevice = 'General';
    cmp.hoursText = '1';
    cmp.save();
    expect(saved[0].device).toBe('General');
  });

  describe('device selector', () => {
    function render(devices: string[] | null) {
      TestBed.configureTestingModule({ declarations: [LogSessionDialogComponent], imports: [FormsModule] });
      const f = TestBed.createComponent(LogSessionDialogComponent);
      f.componentInstance.device = 'PC';
      f.componentInstance.devices = devices;
      f.detectChanges();
      return f.nativeElement as HTMLElement;
    }
    it('is shown only when devices is not null', () => {
      expect(render(['PC', 'General']).querySelector('select.device-select')).not.toBeNull();
    });
    it('is hidden when devices is null', () => {
      expect(render(null).querySelector('select.device-select')).toBeNull();
    });
  });
});
