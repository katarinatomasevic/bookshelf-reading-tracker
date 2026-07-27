import { Component, inject } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-session-expired-modal',
  imports: [DialogModule, ButtonModule],
  templateUrl: './session-expired-modal.html',
})
export class SessionExpiredModal {
  protected readonly authService = inject(AuthService);

  onAcknowledge(): void {
    this.authService.acknowledgeSessionExpired();
  }
}
