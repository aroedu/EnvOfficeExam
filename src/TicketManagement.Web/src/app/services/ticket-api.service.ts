import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PagedResult,
  TicketAuditLogDto,
  TicketDto,
  TicketPriority,
  TicketQuery,
  TicketStatisticsDto,
  TicketStatus
} from '../models/ticket.models';

@Injectable({ providedIn: 'root' })
export class TicketApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/tickets';

  getTickets(query: TicketQuery): Observable<PagedResult<TicketDto>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDescending', query.sortDescending);

    if (query.status) params = params.set('status', query.status);
    if (query.priority) params = params.set('priority', query.priority);
    if (query.assignedTo) params = params.set('assignedTo', query.assignedTo);
    if (query.search) params = params.set('search', query.search);

    return this.http.get<PagedResult<TicketDto>>(this.endpoint, { params });
  }

  getStatistics(): Observable<TicketStatisticsDto> {
    return this.http.get<TicketStatisticsDto>(`${this.endpoint}/statistics`);
  }

  updateStatus(id: string, newStatus: TicketStatus, version: number): Observable<TicketDto> {
    return this.http.patch<TicketDto>(`${this.endpoint}/${id}/status`, { newStatus, version });
  }

  getHistory(id: string): Observable<TicketAuditLogDto[]> {
    return this.http.get<TicketAuditLogDto[]>(`${this.endpoint}/${id}/history`);
  }
}
