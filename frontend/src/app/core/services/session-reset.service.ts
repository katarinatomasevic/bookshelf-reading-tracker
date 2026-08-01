import { Injectable } from '@angular/core';

/**
 * Lets feature services drop user-visible state when a session ends, without `core`
 * having to import them — that would invert the core → features dependency direction.
 * AuthService decides *when* a session ends; each feature decides *what* to discard.
 */
@Injectable({ providedIn: 'root' })
export class SessionResetService {
  private readonly handlers = new Set<() => void>();

  /** True once a session has ended and until a new one begins. Feature services are
   *  created lazily, so one may not exist yet when the session ends — e.g. a reload
   *  on /profile with an expired token clears auth before the search page is opened.
   *  Remembering the reset means such a service still discards its restored state. */
  private resetPending = false;

  register(handler: () => void): void {
    this.handlers.add(handler);
    if (this.resetPending) {
      handler();
    }
  }

  resetAll(): void {
    this.resetPending = true;
    this.handlers.forEach((handler) => handler());
  }

  /** Called when a new session starts, so a later-created service isn't wiped by an
   *  already-superseded logout. */
  clearPending(): void {
    this.resetPending = false;
  }
}
