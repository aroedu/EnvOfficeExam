import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import {
  LucideArrowDownWideNarrow,
  LucideChartNoAxesColumn,
  LucideChevronLeft,
  LucideChevronRight,
  LucideCircleAlert,
  LucideClock3,
  LucideInbox,
  LucideRefreshCw,
  LucideSearch,
  LucideSlidersHorizontal,
  LucideTicketCheck,
  LucideX
} from '@lucide/angular';
import { EMPTY, Subject, catchError, debounceTime, distinctUntilChanged, finalize, of, startWith, switchMap, takeUntil, tap } from 'rxjs';
import { TicketApiService } from '../../services/ticket-api.service';
import {
  TicketAuditLogDto,
  TicketDto,
  TicketPriority,
  TicketQuery,
  TicketSortField,
  TicketStatisticsDto,
  TicketStatus
} from '../../models/ticket.models';

@Component({
  selector: 'app-ticket-workspace',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LucideArrowDownWideNarrow,
    LucideChartNoAxesColumn,
    LucideChevronLeft,
    LucideChevronRight,
    LucideCircleAlert,
    LucideClock3,
    LucideInbox,
    LucideRefreshCw,
    LucideSearch,
    LucideSlidersHorizontal,
    LucideTicketCheck,
    LucideX
  ],
  templateUrl: './ticket-workspace.component.html',
  styleUrl: './ticket-workspace.component.scss'
})
export class TicketWorkspaceComponent {
  private readonly api = inject(TicketApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshList$ = new Subject<void>();
  private readonly cancelListRequest$ = new Subject<void>();
  private readonly refreshStatistics$ = new Subject<void>();
  private readonly historySelection$ = new Subject<TicketDto | null>();

  readonly search = new FormControl('', { nonNullable: true });
  readonly filters = new FormGroup({
    status: new FormControl<TicketStatus | ''>('', { nonNullable: true }),
    priority: new FormControl<TicketPriority | ''>('', { nonNullable: true }),
    assignedTo: new FormControl('', { nonNullable: true }),
    sortBy: new FormControl<TicketSortField>('CreatedAt', { nonNullable: true }),
    sortDescending: new FormControl(true, { nonNullable: true }),
    pageSize: new FormControl(10, { nonNullable: true })
  });

  readonly tickets = signal<TicketDto[]>([]);
  readonly statistics = signal<TicketStatisticsDto | null>(null);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly page = signal(1);
  readonly loading = signal(true);
  readonly statsLoading = signal(true);
  readonly historyLoading = signal(false);
  readonly listError = signal('');
  readonly statsError = signal('');
  readonly historyError = signal('');
  readonly actionMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);
  readonly selectedTicket = signal<TicketDto | null>(null);
  readonly historyEntries = signal<TicketAuditLogDto[]>([]);
  readonly updatingTicketId = signal<string | null>(null);

  readonly firstResult = computed(() => this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1);
  readonly lastResult = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));
  readonly pageSize = computed(() => this.filters.controls.pageSize.value);
  readonly canGoBack = computed(() => this.page() > 1);
  readonly canGoForward = computed(() => this.page() < this.totalPages());
  readonly totalTickets = computed(() => this.statistics()?.byStatus.reduce((sum, entry) => sum + entry.count, 0) ?? 0);
  readonly highPriorityCount = computed(() => this.statistics()?.byPriority.find(entry => entry.priority === 'High')?.count ?? 0);

  readonly statuses: TicketStatus[] = ['New', 'InProgress', 'Waiting', 'Completed'];
  readonly priorities: TicketPriority[] = ['High', 'Medium', 'Low'];
  readonly sortFields: { value: TicketSortField; label: string }[] = [
    { value: 'CreatedAt', label: 'תאריך יצירה' },
    { value: 'UpdatedAt', label: 'עדכון אחרון' },
    { value: 'Priority', label: 'עדיפות' },
    { value: 'Title', label: 'כותרת' },
    { value: 'Status', label: 'סטטוס' }
  ];
  readonly nextStatuses: Record<TicketStatus, TicketStatus[]> = {
    New: ['InProgress'],
    InProgress: ['Waiting', 'Completed'],
    Waiting: ['InProgress', 'Completed'],
    Completed: []
  };

  private readonly statusLabels: Record<TicketStatus, string> = {
    New: 'חדש',
    InProgress: 'בטיפול',
    Waiting: 'ממתין',
    Completed: 'הושלם'
  };
  private readonly priorityLabels: Record<TicketPriority, string> = {
    High: 'גבוהה',
    Medium: 'בינונית',
    Low: 'נמוכה'
  };

  constructor() {
    this.refreshList$
      .pipe(
        startWith(undefined),
        switchMap(() => {
          this.loading.set(true);
          this.listError.set('');
          return this.api.getTickets(this.buildQuery()).pipe(
            takeUntil(this.cancelListRequest$),
            tap(result => {
              this.tickets.set(result.items);
              this.totalCount.set(result.totalCount);
              this.totalPages.set(result.totalPages);
            }),
            catchError(() => {
              this.listError.set('לא ניתן לטעון את הפניות. בדוק שה־API פועל ונסה שוב.');
              return EMPTY;
            }),
            finalize(() => this.loading.set(false))
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();

    this.filters.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.page.set(1);
        this.refreshList$.next();
      });

    this.search.valueChanges
      .pipe(
        distinctUntilChanged(),
        tap(() => {
          this.page.set(1);
          this.cancelListRequest$.next();
          this.loading.set(true);
          this.listError.set('');
        }),
        debounceTime(350),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => {
        this.page.set(1);
        this.refreshList$.next();
      });

    this.refreshStatistics$
      .pipe(
        startWith(undefined),
        switchMap(() => {
          this.statsLoading.set(true);
          return this.api.getStatistics().pipe(
            tap(result => {
              this.statistics.set(result);
              this.statsError.set('');
            }),
            catchError(() => {
              this.statsError.set('לא ניתן לטעון נתונים מסכמים.');
              return EMPTY;
            }),
            finalize(() => this.statsLoading.set(false))
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();

    this.historySelection$
      .pipe(
        switchMap(ticket => {
          this.selectedTicket.set(ticket);
          this.historyEntries.set([]);
          this.historyError.set('');
          if (!ticket) {
            this.historyLoading.set(false);
            return of([] as TicketAuditLogDto[]);
          }

          this.historyLoading.set(true);
          return this.api.getHistory(ticket.id).pipe(
            tap(entries => this.historyEntries.set(entries)),
            catchError(() => {
              this.historyError.set('לא ניתן לטעון את היסטוריית השינויים.');
              return of([] as TicketAuditLogDto[]);
            }),
            finalize(() => this.historyLoading.set(false))
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  statusLabel(status: TicketStatus): string {
    return this.statusLabels[status];
  }

  priorityLabel(priority: TicketPriority): string {
    return this.priorityLabels[priority];
  }

  statusCount(status: TicketStatus): number {
    return this.statistics()?.byStatus.find(entry => entry.status === status)?.count ?? 0;
  }

  trackTicket(_index: number, ticket: TicketDto): string {
    return ticket.id;
  }

  changePage(delta: number): void {
    const nextPage = this.page() + delta;
    if (nextPage < 1 || nextPage > this.totalPages()) return;
    this.page.set(nextPage);
    this.refreshList$.next();
  }

  toggleSortDirection(): void {
    this.filters.controls.sortDescending.setValue(!this.filters.controls.sortDescending.value);
  }

  clearFilters(): void {
    this.search.setValue('', { emitEvent: false });
    this.filters.setValue({
      status: '',
      priority: '',
      assignedTo: '',
      sortBy: 'CreatedAt',
      sortDescending: true,
      pageSize: 10
    });
    this.page.set(1);
  }

  refresh(): void {
    this.refreshList$.next();
    this.refreshStatistics$.next();
  }

  openHistory(ticket: TicketDto): void {
    this.historySelection$.next(ticket);
  }

  closeHistory(): void {
    this.historySelection$.next(null);
  }

  updateStatus(ticket: TicketDto, event: Event): void {
    const newStatus = (event.target as HTMLSelectElement).value as TicketStatus;
    if (newStatus === ticket.status || !this.nextStatuses[ticket.status].includes(newStatus)) return;

    this.updatingTicketId.set(ticket.id);
    this.actionMessage.set(null);
    this.api.updateStatus(ticket.id, newStatus, ticket.version)
      .pipe(
        tap(updated => {
          this.tickets.update(items => items.map(item => item.id === updated.id ? updated : item));
          this.actionMessage.set({ text: `הסטטוס של „${updated.title}” עודכן.`, type: 'success' });
          this.refreshStatistics$.next();
          if (this.selectedTicket()?.id === updated.id) this.historySelection$.next(updated);
        }),
        catchError((error: HttpErrorResponse) => {
          const text = error.status === 409
            ? 'העדכון התנגש בשינוי אחר או במעבר סטטוס שאינו מותר (409). הרשימה רועננה; בדוק את הנתונים ונסה שוב.'
            : 'עדכון הסטטוס נכשל. נסה שוב.';
          this.actionMessage.set({ text, type: 'error' });
          if (error.status === 409) this.refreshList$.next();
          return EMPTY;
        }),
        finalize(() => this.updatingTicketId.set(null)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  private buildQuery(): TicketQuery {
    const filters = this.filters.getRawValue();
    const search = this.search.value.trim();
    return {
      page: this.page(),
      pageSize: filters.pageSize,
      status: filters.status || undefined,
      priority: filters.priority || undefined,
      assignedTo: filters.assignedTo.trim() || undefined,
      search: search || undefined,
      sortBy: filters.sortBy,
      sortDescending: filters.sortDescending
    };
  }
}
