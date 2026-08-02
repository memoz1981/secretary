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

public sealed class ClientServiceTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private const int TenantId = 7;

    private readonly FakeUnitOfWork _uow = new();
    private readonly ClientService _sut;

    public ClientServiceTests()
    {
        _sut = new ClientService(_uow.Object, new FakeClock(Now), new FakeCurrentTenantProvider(TenantId));
    }

    [Fact]
    public async Task FindOrCreateByPhoneNumberAsync_returns_existing_client_unchanged_when_name_already_known()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync(client);

        var result = await _sut.FindOrCreateByPhoneNumberAsync("+994000000", "Different Name", default);

        result.Name.ShouldBe("Tofiq Aliyev");
        _uow.Clients.Verify(c => c.AddAsync(It.IsAny<Client>(), default), Times.Never);
    }

    [Fact]
    public async Task FindOrCreateByPhoneNumberAsync_fills_in_name_when_previously_unknown()
    {
        var client = Client.Create(TenantId, "+994000000", Now);
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync(client);

        var result = await _sut.FindOrCreateByPhoneNumberAsync("+994000000", "Tofiq Aliyev", default);

        result.Name.ShouldBe("Tofiq Aliyev");
    }

    [Fact]
    public async Task FindOrCreateByPhoneNumberAsync_creates_new_client_when_none_exists()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync((Client?)null);

        var result = await _sut.FindOrCreateByPhoneNumberAsync("+994000000", "Tofiq Aliyev", default);

        result.PhoneNumber.ShouldBe("+994000000");
        result.Name.ShouldBe("Tofiq Aliyev");
        _uow.Clients.Verify(c => c.AddAsync(It.IsAny<Client>(), default), Times.Once);
    }

    [Fact]
    public async Task GetByPhoneNumberAsync_returns_null_when_not_found()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync((Client?)null);

        var result = await _sut.GetByPhoneNumberAsync("+994000000", default);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_maps_blacklist_fields()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        client.BlackList("no-shows", Now);
        _uow.Clients.Setup(c => c.GetAllForCurrentTenantAsync(default)).ReturnsAsync([client]);

        var result = await _sut.ListAsync(default);

        result.Count.ShouldBe(1);
        result[0].BlackListed.ShouldBeTrue();
        result[0].BlackListReason.ShouldBe("no-shows");
    }

    [Fact]
    public async Task CreateAsync_throws_when_phone_number_already_exists()
    {
        var existing = Client.Create(TenantId, "+994000000", Now);
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync(existing);

        await Should.ThrowAsync<DuplicateClientPhoneNumberException>(
            () => _sut.CreateAsync(new CreateClientRequest("+994000000", "Tofiq"), default));
        _uow.Clients.Verify(c => c.AddAsync(It.IsAny<Client>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_adds_and_saves()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync((Client?)null);

        var result = await _sut.CreateAsync(new CreateClientRequest("+994000000", "Tofiq"), default);

        result.PhoneNumber.ShouldBe("+994000000");
        _uow.Clients.Verify(c => c.AddAsync(It.IsAny<Client>(), default), Times.Once);
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_throws_when_new_phone_number_belongs_to_another_client()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq");
        var other = Client.Create(TenantId, "+994111111", Now, "Someone Else");
        SetPrivateId(other, 99);

        _uow.Clients.Setup(c => c.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994111111", default)).ReturnsAsync(other);

        await Should.ThrowAsync<DuplicateClientPhoneNumberException>(
            () => _sut.UpdateAsync(client.Id, new UpdateClientRequest("+994111111", "Tofiq"), default));
    }

    [Fact]
    public async Task RemoveAsync_soft_deactivates()
    {
        var client = Client.Create(TenantId, "+994000000", Now);
        _uow.Clients.Setup(c => c.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        await _sut.RemoveAsync(client.Id, default);

        client.Status.ShouldBe(EntityStatus.Inactive);
        _uow.Clients.Verify(c => c.Remove(It.IsAny<Client>()), Times.Never);
    }

    [Fact]
    public async Task BlackListAsync_then_UndoBlackListAsync_round_trips()
    {
        var client = Client.Create(TenantId, "+994000000", Now);
        _uow.Clients.Setup(c => c.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var blacklisted = await _sut.BlackListAsync(client.Id, new BlackListClientRequest("repeated no-shows"), default);
        blacklisted.BlackListed.ShouldBeTrue();
        blacklisted.BlackListReason.ShouldBe("repeated no-shows");

        var restored = await _sut.UndoBlackListAsync(client.Id, default);
        restored.BlackListed.ShouldBeFalse();
        restored.BlackListReason.ShouldBeNull();
    }

    [Fact]
    public async Task GetUpcomingAppointmentsAsync_maps_appointments_with_client_details()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        var appointment = Appointment.Create(
            TenantId, client.Id, 20, 30,
            Instant.FromUtc(2026, 7, 12, 9, 0), Instant.FromUtc(2026, 7, 12, 9, 30), null, AppointmentCreatedBy.Agent, Now);

        _uow.Appointments.Setup(a => a.GetUpcomingForClientAsync(client.Id, Now, default)).ReturnsAsync([appointment]);
        _uow.Clients.Setup(c => c.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var result = await _sut.GetUpcomingAppointmentsAsync(client.Id, default);

        result.Count.ShouldBe(1);
        result[0].ClientName.ShouldBe("Tofiq Aliyev");
        result[0].ClientPhoneNumber.ShouldBe("+994000000");
    }

    /// <summary>Identity ids are 0 until a real save; force a distinct id where a test needs
    /// two entities to differ (the duplicate-phone check compares ids).</summary>
    private static void SetPrivateId(Client client, int id)
        => typeof(Client).BaseType!
            .GetProperty("Id")!
            .SetValue(client, id);
}
