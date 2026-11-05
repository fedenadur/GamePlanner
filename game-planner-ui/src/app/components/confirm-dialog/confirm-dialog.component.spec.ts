import { TestBed } from '@angular/core/testing';
import { ConfirmDialogComponent } from './confirm-dialog.component';

describe('ConfirmDialogComponent', () => {
  function create() {
    TestBed.configureTestingModule({ declarations: [ConfirmDialogComponent] });
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.componentInstance.title = 'Delete X?';
    fixture.componentInstance.message = 'Gone.';
    fixture.componentInstance.confirmLabel = 'Delete';
    fixture.detectChanges();
    return fixture;
  }

  it('renders title and message', () => {
    const el: HTMLElement = create().nativeElement;
    expect(el.querySelector('h4')!.textContent).toBe('Delete X?');
    expect(el.querySelector('p')!.textContent).toBe('Gone.');
    expect(el.querySelector('.btn-danger')!.textContent).toBe('Delete');
  });

  it('emits confirmed and cancelled', () => {
    const f = create();
    let confirmed = 0, cancelled = 0;
    f.componentInstance.confirmed.subscribe(() => confirmed++);
    f.componentInstance.cancelled.subscribe(() => cancelled++);
    const el: HTMLElement = f.nativeElement;
    (el.querySelector('.btn-danger') as HTMLElement).click();
    (el.querySelector('.btn-ghost') as HTMLElement).click();
    expect(confirmed).toBe(1);
    expect(cancelled).toBe(1);
  });
});
