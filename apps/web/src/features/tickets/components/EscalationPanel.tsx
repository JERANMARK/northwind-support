import { useEffect, useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import { formatDateTime } from '../../../lib/format';
import type { TicketDetail, TicketEscalation } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

export function EscalationPanel({ ticket, onUpdated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string>();

  const canEscalate =
    ticket.priority !== 'Critical' &&
    ticket.status !== 'Resolved' &&
    ticket.status !== 'Closed';

  useEffect(() => {
    let active = true;

    const loadHistory = async () => {
      setIsLoadingHistory(true);
      try {
        const escalations = await ticketsApi.getTicketEscalations(ticket.id);
        if (active) {
          setHistory(escalations);
        }
      } catch {
        if (active) {
          setHistory([]);
        }
      } finally {
        if (active) {
          setIsLoadingHistory(false);
        }
      }
    };

    void loadHistory();

    return () => {
      active = false;
    };
  }, [ticket.id]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmed = reason.trim();
    if (trimmed.length < 5 || trimmed.length > 500) {
      setError('Reason must be between 5 and 500 characters.');
      return;
    }

    setError(undefined);
    setIsSubmitting(true);

    try {
      const updated = await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmed,
        escalatedBy: 'Support Desk UI',
      });

      onUpdated(updated);
      setReason('');
      const escalations = await ticketsApi.getTicketEscalations(ticket.id);
      setHistory(escalations);
    } catch (caught) {
      const message =
        caught instanceof ApiError
          ? caught.message
          : toErrorMessage(caught, 'Could not escalate this ticket.');
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <section className="card">
      <h2>Escalation</h2>

      <form onSubmit={submit} noValidate>
        <Field
          id={`escalation-reason-${ticket.id}`}
          label="Escalation reason"
          hint="At least 5 and no more than 500 characters."
          error={error}
        >
          {(fieldProps) => (
            <textarea
              {...fieldProps}
              rows={4}
              value={reason}
              maxLength={500}
              disabled={!canEscalate || isSubmitting}
              onChange={(event) => {
                setReason(event.target.value);
                if (error) {
                  setError(undefined);
                }
              }}
            />
          )}
        </Field>

        <div className="button-row">
          <button
            type="submit"
            className="button button--primary"
            disabled={!canEscalate || isSubmitting || reason.trim().length < 5}
          >
            {isSubmitting ? 'Escalating...' : 'Escalate ticket'}
          </button>
        </div>
      </form>

      {!canEscalate && (
        <p className="field__hint">This ticket cannot be escalated because it is already critical or resolved/closed.</p>
      )}

      <div className="escalation-history">
        <h3>History</h3>

        {isLoadingHistory && <p>Loading escalation history...</p>}

        {!isLoadingHistory && history.length === 0 && (
          <p className="field__hint">No escalations have been recorded for this ticket yet.</p>
        )}

        {!isLoadingHistory && history.length > 0 && (
          <ul className="list list--stacked">
            {history.map((entry) => (
              <li key={entry.id} className="list__item">
                <strong>
                  {entry.fromPriority} → {entry.toPriority}
                </strong>
                <div>
                  Agent: {entry.fromAgentId ?? 'Unassigned'} → {entry.toAgentId ?? 'Unassigned'}
                </div>
                <div>
                  Due: {formatDateTime(entry.fromDueAtUtc)} → {formatDateTime(entry.toDueAtUtc)}
                </div>
                <div>Reason: {entry.reason}</div>
                <div>
                  By {entry.escalatedBy} on {formatDateTime(entry.escalatedAtUtc)}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
