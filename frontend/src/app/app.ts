import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { SessionExpiredModal } from './core/components/session-expired-modal/session-expired-modal';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, SessionExpiredModal],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly authService = inject(AuthService);

  onLogout(): void {
    this.authService.logout();
  }
}
