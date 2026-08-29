import { Directive, ElementRef, EventEmitter, HostListener, Output } from '@angular/core';

@Directive({
    selector: '[clickOutside]',
    standalone: false
})
export class ClickOutsideDirective {
  @Output() clickOutside = new EventEmitter<void>();

  constructor(private el: ElementRef) {}

  @HostListener('document:click', ['$event.target'])
  onClick(target: EventTarget | null) {
    if (target instanceof Node && !this.el.nativeElement.contains(target)) this.clickOutside.emit();
  }
}
