using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class EscalationTests
{
    private const int TenantId = 1;
    private const int ClientId = 10;
    private const int AcceptedBy = 100;
    private static readonly Instant RaisedAt = Instant.FromUtc(2026, 7, 11, 9, 0);

    private static Escalation Raise() => Escalation.Raise(TenantId, ClientId, "agent could not resolve request", RaisedAt);

    [Fact]
    public void Raise_starts_ringing()
    {
        var escalation = Raise();

        escalation.EscalationStatus.ShouldBe(EscalationStatus.Ringing);
        escalation.ClientId.ShouldBe(ClientId);
        escalation.RaisedAt.ShouldBe(RaisedAt);
        escalation.AcceptedByAccountId.ShouldBeNull();
    }

    [Fact]
    public void Accept_moves_to_connected_and_records_who_and_when()
    {
        var escalation = Raise();
        var acceptedAt = RaisedAt + Duration.FromSeconds(8);

        escalation.Accept(AcceptedBy, acceptedAt);

        escalation.EscalationStatus.ShouldBe(EscalationStatus.Connected);
        escalation.AcceptedByAccountId.ShouldBe(AcceptedBy);
        escalation.AcceptedAt.ShouldBe(acceptedAt);
    }

    [Fact]
    public void Accept_twice_throws()
    {
        var escalation = Raise();
        escalation.Accept(AcceptedBy, RaisedAt + Duration.FromSeconds(8));

        Should.Throw<InvalidStateTransitionException>(() => escalation.Accept(AcceptedBy + 1, RaisedAt + Duration.FromSeconds(10)));
    }

    [Fact]
    public void Abandon_moves_ringing_to_abandoned()
    {
        var escalation = Raise();
        var abandonedAt = RaisedAt + Duration.FromSeconds(30);

        escalation.Abandon(abandonedAt);

        escalation.EscalationStatus.ShouldBe(EscalationStatus.Abandoned);
        escalation.EndedAt.ShouldBe(abandonedAt);
    }

    [Fact]
    public void Abandon_after_accept_throws()
    {
        var escalation = Raise();
        escalation.Accept(AcceptedBy, RaisedAt + Duration.FromSeconds(8));

        Should.Throw<InvalidStateTransitionException>(() => escalation.Abandon(RaisedAt + Duration.FromSeconds(38)));
    }

    [Fact]
    public void EndConnectedCall_requires_connected_state()
    {
        var escalation = Raise();

        Should.Throw<InvalidStateTransitionException>(() => escalation.EndConnectedCall(RaisedAt + Duration.FromSeconds(30)));
    }

    [Fact]
    public void EndConnectedCall_sets_ended_at_when_connected()
    {
        var escalation = Raise();
        escalation.Accept(AcceptedBy, RaisedAt + Duration.FromSeconds(8));
        var endedAt = RaisedAt + Duration.FromSeconds(90);

        escalation.EndConnectedCall(endedAt);

        escalation.EndedAt.ShouldBe(endedAt);
    }

    [Fact]
    public void WaitTime_is_time_from_raised_to_accepted()
    {
        var escalation = Raise();
        escalation.Accept(AcceptedBy, RaisedAt + Duration.FromSeconds(8));

        escalation.WaitTime().ShouldBe(Duration.FromSeconds(8));
    }

    [Fact]
    public void WaitTime_is_time_from_raised_to_abandoned_when_never_accepted()
    {
        var escalation = Raise();
        escalation.Abandon(RaisedAt + Duration.FromSeconds(30));

        escalation.WaitTime().ShouldBe(Duration.FromSeconds(30));
    }
}
