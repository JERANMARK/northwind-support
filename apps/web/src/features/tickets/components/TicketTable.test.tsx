import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { ticketListItem } from '../../../test/fixtures';
import { TicketTable } from './TicketTable';

describe('TicketTable', () => {
  it('shows the reference, owner and SLA state of each ticket', () => {
    render(
      <MemoryRouter>
        <TicketTable
          tickets={[
            ticketListItem(),
            ticketListItem({
              id: 21,
              reference: 'TCK-0021',
              title: 'Possible data exposure in shared links',
              priority: 'Critical',
              assignedAgent: null,
              slaStatus: 'WithinSla',
            }),
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'TCK-0012' })).toHaveAttribute('href', '/tickets/12');
    expect(screen.getByText('Sara Lindqvist')).toBeInTheDocument();
    expect(screen.getByText('Breached')).toBeInTheDocument();

    // An unassigned ticket says so, rather than showing an empty cell.
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('Within SLA')).toBeInTheDocument();
  });
});

describe('SlaCell', () => {
  it('renders all SLA states with distinguishable labels', () => {
    render(
      <MemoryRouter>
        <TicketTable
          tickets={[
            ticketListItem({ id: 1, slaStatus: 'Met' as const }),
            ticketListItem({ id: 2, slaStatus: 'Breached' as const }),
            ticketListItem({ id: 3, slaStatus: 'AtRisk' as const }),
            ticketListItem({ id: 4, slaStatus: 'WithinSla' as const }),
            ticketListItem({ id: 5, slaStatus: 'NotApplicable' as const }),
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByText('Met')).toBeInTheDocument();
    expect(screen.getByText('Breached')).toBeInTheDocument();
    expect(screen.getByText('At risk')).toBeInTheDocument();
    expect(screen.getByText('Within SLA')).toBeInTheDocument();
    expect(screen.getByText('No SLA')).toBeInTheDocument();
  });
});
