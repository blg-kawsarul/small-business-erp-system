import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, map } from 'rxjs';
import { ConfirmDialog, ConfirmDialogData } from '../shared/confirm-dialog';
import { errorMessage } from './api.service';

@Injectable({ providedIn: 'root' })
export class NotifyService {
  private readonly snack = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  success(message: string): void {
    this.snack.open(message, 'OK', { duration: 3500, panelClass: 'snack-success' });
  }

  error(error: unknown): void {
    this.snack.open(typeof error === 'string' ? error : errorMessage(error), 'Close', { duration: 8000, panelClass: 'snack-error' });
  }

  confirm(data: ConfirmDialogData): Observable<boolean> {
    return this.dialog
      .open(ConfirmDialog, { data, width: '440px', autoFocus: 'dialog' })
      .afterClosed()
      .pipe(map((r) => r === true));
  }
}
