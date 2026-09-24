export type TicketStatus = 'New' | 'InProgress' | 'Waiting' | 'Completed';
export type TicketPriority = 'Low' | 'Medium' | 'High';
export type TicketSortField = 'CreatedAt' | 'UpdatedAt' | 'Priority' | 'Title' | 'Status';

export interface TicketDto {
  id: string;
  title: string;
  organizationName: string;
  status: TicketStatus;
  priority: TicketPriority;
  assignedTo: string | null;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface TicketQuery {
  page: number;
  pageSize: number;
  status?: TicketStatus;
  priority?: TicketPriority;
  assignedTo?: string;
  search?: string;
  sortBy: TicketSortField;
  sortDescending: boolean;
}

export interface TicketStatisticsDto {
  byStatus: { status: TicketStatus; count: number }[];
  byPriority: { priority: TicketPriority; count: number }[];
}

export interface TicketAuditLogDto {
  oldStatus: TicketStatus;
  newStatus: TicketStatus;
  changedBy: string | null;
  changedAt: string;
}
