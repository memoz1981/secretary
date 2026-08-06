using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Application.Tests.TestSupport;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests.Services;

public sealed class EscalationServiceTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private const int TenantId = 7;
    private const int ClientId = 10;

    private readonly FakeUnitOfWork _uow = new();
    private readonly EscalationService _sut;

    public EscalationServiceTests()
    {
        var tenantProvider = new FakeCurrentTenantProvider(TenantId);
        var clientService = new ClientService(_uow.Object, new FakeClock(Now), tenantProvider);
        _sut = new EscalationService(_uow.Object, new FakeClock(Now), tenantProvider, clientService);
    }

    [Fact]
    public async Task RaiseAsync_creates_ringing_escalation_linked_to_an_existing_client()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync(client);

        var result = await _sut.RaiseAsync(new RaiseEscalationRequest("+994000000", "no availability"), default);

        result.Status.ShouldBe(EscalationStatus.Ringing);
        result.CallerPhoneNumber.ShouldBe("+994000000");
        result.ClientName.ShouldBe("Tofiq Aliyev");
        _uow.Clients.Verify(c => c.AddAsync(It.IsAny<Client>(), default), Times.Never);
        _uow.Escalations.Verify(e => e.AddAsync(It.IsAny<Escalation>(), default), Times.Once);
    }

    [Fact]
    public async Task RaiseAsync_creates_the_client_when_the_caller_is_unknown()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync((Client?)null);

        var result = await _sut.RaiseAsync(new RaiseEscalationRequest("+994000000", "caller asked for a human"), default);

        result.Status.ShouldBe(EscalationStatus.Ringing);
        result.CallerPhoneNumber.ShouldBe("+994000000");
        _uow.Clients.Verify(c => c.AddAsync(It.IsAny<Client>(), default), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_transitions_to_connected()
    {
        var escalation = Escalation.Raise(TenantId, ClientId, "reason", Now);
        _uow.Escalations.Setup(e => e.GetByIdAsync(escalation.Id, default)).ReturnsAsync(escalation);
        _uow.Clients.Setup(c => c.GetByIdAsync(ClientId, default)).ReturnsAsync((Client?)null);

        const int acceptedBy = 100;
        var result = await _sut.AcceptAsync(escalation.Id, acceptedBy, default);

        result.Status.ShouldBe(EscalationStatus.Connected);
        result.AcceptedByAccountId.ShouldBe(acceptedBy);
    }

    [Fact]
    public async Task AbandonAsync_transitions_to_abandoned()
    {
        var escalation = Escalation.Raise(TenantId, ClientId, "reason", Now);
        _uow.Escalations.Setup(e => e.GetByIdAsync(escalation.Id, default)).ReturnsAsync(escalation);
        _uow.Clients.Setup(c => c.GetByIdAsync(ClientId, default)).ReturnsAsync((Client?)null);

        var result = await _sut.AbandonAsync(escalation.Id, default);

        result.Status.ShouldBe(EscalationStatus.Abandoned);
    }

    [Fact]
    public async Task AcceptAsync_throws_not_found_when_missing()
    {
        _uow.Escalations.Setup(e => e.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Escalation?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.AcceptAsync(42, 100, default));
    }

    [Fact]
    public async Task GetRingingAsync_delegates_to_repository_and_denormalizes_client_details()
    {
        var escalation = Escalation.Raise(TenantId, ClientId, "reason", Now);
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        _uow.Escalations.Setup(e => e.GetRingingForCurrentTenantAsync(default)).ReturnsAsync([escalation]);
        _uow.Clients.Setup(c => c.GetByIdAsync(ClientId, default)).ReturnsAsync(client);

        var result = await _sut.GetRingingAsync(default);

        result.Count.ShouldBe(1);
        result[0].CallerPhoneNumber.ShouldBe("+994000000");
    }
}
