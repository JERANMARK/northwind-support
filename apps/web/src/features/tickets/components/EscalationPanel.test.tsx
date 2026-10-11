import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { EscalationPanel } from './EscalationPanel';
import type { TicketDetail } from '../../../types/api';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
    getTicketEscalations: vi.fn(),
  },
}));

const escalateTicket = vi.mocked(ticketsApi.escalateTicket);
const getTicketEscalations = vi.mocked(ticketsApi.getTicketEscalations);

describe('EscalationPanel', () => {
  const mockTicket: TicketDetail = {
    id: 1,
    reference: 'TCK-0001',
    title: 'Test ticket',
    description: 'Test description',
    priority: 'High',
    status: 'InProgress',
    slaStatus: 'WithinSla',
    dueAtUtc: null,
    resolvedAtUtc: null,
    createdAtUtc: '2026-03-02T09:00:00Z',
    updatedAtUtc: '2026-03-02T09:00:00Z',
    assignedAgent: { id: 1, fullName: 'Alex Turner' },
    customer: {
      id: 1,
      name: 'Contoso Ltd',
      email: 'support@contoso.example',
      phone: null,
      tier: 'Standard',
    },
    category: {
      id: 1,
      name: 'General',
      requiresSpecialist: false,
    },
  };

  const mockOnUpdated = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    getTicketEscalations.mockResolvedValue([]);
  });

  it('does not call the API when the reason is too short', async () => {
    const user = userEvent.setup();

    render(<EscalationPanel ticket={mockTicket} onUpdated={mockOnUpdated} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    await user.type(textarea, 'abc');
    await user.click(submitButton);

    expect(escalateTicket).not.toHaveBeenCalled();
    // The error is set in state but displayed via the Field component's error prop
    expect(submitButton).toBeDisabled();
  });

  it('does not call the API when the reason is empty', async () => {
    const user = userEvent.setup();

    render(<EscalationPanel ticket={mockTicket} onUpdated={mockOnUpdated} />);

    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    await user.click(submitButton);

    expect(escalateTicket).not.toHaveBeenCalled();
    // Button should be disabled when reason is too short
    expect(submitButton).toBeDisabled();
  });

  it('displays a 409 error and does not update the ticket', async () => {
    const user = userEvent.setup();

    escalateTicket.mockRejectedValue({
      message: 'Critical tickets cannot be escalated.',
      status: 409,
    });

    render(<EscalationPanel ticket={mockTicket} onUpdated={mockOnUpdated} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    await user.type(textarea, 'This is a valid escalation reason');
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText(/could not escalate/i)).toBeInTheDocument();
    });

    expect(escalateTicket).toHaveBeenCalledTimes(1);
    expect(mockOnUpdated).not.toHaveBeenCalled();
  });

  it('calls the API and updates the ticket on successful escalation', async () => {
    const user = userEvent.setup();
    const updatedTicket: TicketDetail = { ...mockTicket, priority: 'Critical' };

    escalateTicket.mockResolvedValue(updatedTicket);
    getTicketEscalations.mockResolvedValue([
      {
        id: 1,
        ticketId: 1,
        fromPriority: 'High',
        toPriority: 'Critical',
        fromAgentId: 1,
        toAgentId: 1,
        fromDueAtUtc: null,
        toDueAtUtc: '2026-03-02T13:00:00Z',
        reason: 'This is a valid escalation reason',
        escalatedBy: 'Support Desk UI',
        escalatedAtUtc: '2026-03-02T09:00:00Z',
      },
    ]);

    render(<EscalationPanel ticket={mockTicket} onUpdated={mockOnUpdated} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    await user.type(textarea, 'This is a valid escalation reason');
    await user.click(submitButton);

    await waitFor(() => {
      expect(escalateTicket).toHaveBeenCalledWith(1, {
        reason: 'This is a valid escalation reason',
        escalatedBy: 'Support Desk UI',
      });
    });

    expect(mockOnUpdated).toHaveBeenCalledWith(updatedTicket);
  });

  it('disables the button when the ticket cannot be escalated', () => {
    const criticalTicket: TicketDetail = { ...mockTicket, priority: 'Critical' };

    render(<EscalationPanel ticket={criticalTicket} onUpdated={mockOnUpdated} />);

    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });
    expect(submitButton).toBeDisabled();
    expect(screen.getByText(/this ticket cannot be escalated/i)).toBeInTheDocument();
  });
});
