import { Component, computed, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../../../core/services/auth.service';

/**
 * The single place that knows how "add to shelf" behaves, so the search card, the shelf card
 * and the book details page cannot drift apart. A guest is never blocked: the button changes
 * its text and takes them to the login page, which returns them right back here.
 */
@Component({
  selector: 'app-add-to-shelf-button',
  imports: [ButtonModule],
  templateUrl: './add-to-shelf-button.html',
})
export class AddToShelfButton {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly isOnShelf = input(false);
  readonly pending = input(false);
  readonly add = output<void>();

  protected readonly isLoggedIn = this.authService.isLoggedIn;

  protected readonly label = computed(() => {
    if (!this.isLoggedIn()) {
      return 'Log in to add to shelf';
    }
    return this.isOnShelf() ? 'On shelf' : 'Add to shelf';
  });

  protected readonly icon = computed(() =>
    this.isLoggedIn() && this.isOnShelf() ? 'pi pi-check' : 'pi pi-plus',
  );

  onClick(): void {
    if (!this.isLoggedIn()) {
      this.router.navigateByUrl(this.authService.buildLoginRedirect(this.router.url));
      return;
    }

    if (!this.isOnShelf()) {
      this.add.emit();
    }
  }
}
