import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  slaStatus: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });
  const previousFiltersRef = useRef(filters);

  const load = useCallback(async (nextFilters: TicketFilters) => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(nextFilters);
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  }, []);

  useEffect(() => {
    const previousFilters = previousFiltersRef.current;
    const changedSearch = previousFilters.search !== filters.search;
    const changedOtherFilters =
      previousFilters.status !== filters.status ||
      previousFilters.priority !== filters.priority ||
      previousFilters.slaStatus !== filters.slaStatus ||
      previousFilters.categoryId !== filters.categoryId ||
      previousFilters.customerId !== filters.customerId ||
      previousFilters.assignedAgentId !== filters.assignedAgentId ||
      previousFilters.unassignedOnly !== filters.unassignedOnly ||
      previousFilters.page !== filters.page ||
      previousFilters.pageSize !== filters.pageSize ||
      previousFilters.sortBy !== filters.sortBy ||
      previousFilters.sortDirection !== filters.sortDirection;

    previousFiltersRef.current = filters;

    const ms = changedSearch && !changedOtherFilters ? DEBOUNCE_MS : 0;
    const handle = window.setTimeout(() => {
      void load(filters);
    }, ms);

    return () => window.clearTimeout(handle);
  }, [filters, load]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  return { filters, updateFilters, resetFilters, reload: () => void load(filters), ...state };
}
