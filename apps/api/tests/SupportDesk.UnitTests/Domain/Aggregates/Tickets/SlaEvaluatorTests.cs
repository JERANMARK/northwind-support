using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
    public void Evaluate_OpenWithinAtRiskThreshold_IsAtRisk()
    {
        var createdAtUtc = Now.AddHours(-8);
        var dueAtUtc = Now.AddHours(2);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(createdAtUtc, dueAtUtc, null, Now));
    }

    [Fact]
    public void Evaluate_AtExactAtRiskThreshold_IsAtRisk()
    {
        // 24-hour window, 25% threshold = 6 hours remaining triggers AtRisk
        var createdAtUtc = Now.AddHours(-18);
        var dueAtUtc = Now.AddHours(6);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(createdAtUtc, dueAtUtc, null, Now));
    }

    [Fact]
    public void Evaluate_JustAboveAtRiskThreshold_IsWithinSla()
    {
        // 24-hour window, 25% threshold = 6 hours. Just above means >6 hours remaining
        var createdAtUtc = Now.AddHours(-17);
        var dueAtUtc = Now.AddHours(7);

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(createdAtUtc, dueAtUtc, null, Now));
    }

    [Fact]
    public void Evaluate_JustBelowAtRiskThreshold_IsAtRisk()
    {
        // 24-hour window, 25% threshold = 6 hours. Just below means <6 hours remaining
        var createdAtUtc = Now.AddHours(-19);
        var dueAtUtc = Now.AddHours(5);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(createdAtUtc, dueAtUtc, null, Now));
    }

    [Fact]
    public void Evaluate_ResolvedExactlyOnDueDate_IsMet()
    {
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now, Now));
    }

    [Fact]
    public void Evaluate_ResolvedOneMinuteBeforeDue_IsMet()
    {
        // Created at Now, resolved at Now+1min, due at Now+2min, now is Now+2min
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now.AddMinutes(2), Now.AddMinutes(1), Now.AddMinutes(2)));
    }

    [Fact]
    public void Evaluate_ResolvedOneMinuteAfterDue_IsBreached()
    {
        // Created at Now, resolved at Now+2min, due at Now+1min, now is Now+2min
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now.AddMinutes(2), Now.AddMinutes(2)));
    }

    [Fact]
    public void Evaluate_OpenExactlyAtDueDate_IsWithinSla()
    {
        // Due date is exactly now - not > now, so not breached
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(-1), Now.AddHours(-1), null, Now.AddHours(-1)));
    }

    [Fact]
    public void Evaluate_OpenOneMinuteBeforeDue_IsWithinSla()
    {
        // Due date is 1 minute in the future - with a 2-minute window, 25% is 30 seconds, so 1 min is WithinSla
        var createdAt = Now.AddMinutes(-1);
        var dueAt = Now.AddMinutes(1);
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(createdAt, dueAt, null, Now));
    }

    [Fact]
    public void Evaluate_OpenOneMinuteAfterDue_IsBreached()
    {
        // Due date is 1 minute in the past
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddHours(-1), Now.AddMinutes(-1), null, Now));
    }

    [Fact]
    public void Evaluate_OpenOneMillisecondAfterDue_IsBreached()
    {
        // Due date is 1 millisecond in the past
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddHours(-1), Now.AddMilliseconds(-1), null, Now));
    }

    [Fact]
    public void Evaluate_WithCustomAtRiskThreshold_UsesCustomValue()
    {
        // 24-hour window with 50% threshold = 12 hours remaining triggers AtRisk
        var createdAtUtc = Now.AddHours(-12);
        var dueAtUtc = Now.AddHours(12);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(createdAtUtc, dueAtUtc, null, Now, atRiskThresholdPercent: 50));
    }
}
