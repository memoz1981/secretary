using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Abstractions;
using Secretary.Domain.Entities;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests;

/// <summary>A caller who cannot be found now goes to registration rather than being told goodbye,
/// which is the right trade and has one cost: every misheard digit is a second record for
/// somebody who already has one. 050 for 055 makes a second Elvin at the same address, and
/// nobody finds out until the driver does.</summary>
public sealed class CustomerRegistrationTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly CustomerIdentityService _sut;

    public CustomerRegistrationTests()
    {
        _uow.SetupGet(u => u.Customers).Returns(_customers.Object);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(Now);

        var tenant = new Mock<ICurrentTenantProvider>();
        tenant.SetupGet(t => t.TenantId).Returns(1);

        _sut = new CustomerIdentityService(_uow.Object, clock.Object, tenant.Object);
    }

    [Fact]
    public async Task Registering_a_number_we_already_hold_returns_that_customer_instead_of_a_second_one()
    {
        var existing = WithId(Customer.Create(1, "Elvin Məmmədov", Now), 11);
        GivenPhoneMatches(existing);
        GivenAddresses(11, Address(11, "Xətai", "Sarayevo", "12"));

        var result = await _sut.RegisterAsync(Details("Elvin", "0551234567"), default);

        result.CustomerId.ShouldBe(11);
        result.AlreadyExisted.ShouldBeTrue();
        _customers.Verify(c => c.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The other half of the same case: a customer we hold but have no address for is
    /// invisible to the lookups, so registration is how the address arrives. It belongs on the
    /// record they already have.</summary>
    [Fact]
    public async Task A_new_address_for_a_number_we_already_hold_is_added_to_that_customer()
    {
        GivenPhoneMatches(WithId(Customer.Create(1, "Elvin Məmmədov", Now), 11));
        GivenAddresses(11);

        await _sut.RegisterAsync(Details("Elvin", "0551234567"), default);

        _customers.Verify(
            c => c.AddAddressAsync(
                It.Is<CustomerAddress>(a => a.CustomerId == 11 && a.IsDefault),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Registering the same address twice for one customer would leave the agent
    /// choosing between two identical lines on the next call.</summary>
    [Fact]
    public async Task An_address_the_customer_already_has_is_not_added_again()
    {
        GivenPhoneMatches(WithId(Customer.Create(1, "Elvin Məmmədov", Now), 11));

        // Said differently, folding to the same street: "Sarayevo küçəsi" is "Sarayevo".
        GivenAddresses(11, Address(11, "Xətai", "Sarayevo küçəsi", "12"));

        await _sut.RegisterAsync(Details("Elvin", "0551234567", street: "Sarayevo"), default);

        _customers.Verify(
            c => c.AddAddressAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_genuinely_new_number_creates_a_customer_with_a_phone_and_an_address()
    {
        _customers
            .Setup(c => c.FindByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        GivenAddresses(0);

        var result = await _sut.RegisterAsync(Details("Elvin", "0551234567"), default);

        result.AlreadyExisted.ShouldBeFalse();
        _customers.Verify(c => c.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
        _customers.Verify(
            c => c.AddPhoneNumberAsync(It.IsAny<CustomerPhoneNumber>(), It.IsAny<CancellationToken>()), Times.Once);
        _customers.Verify(
            c => c.AddAddressAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Not one of the twelve rayons. Almost always a mishearing, and the agent has to be
    /// able to ask again rather than escalate.</summary>
    [Fact]
    public async Task An_address_outside_Baku_is_refused_by_name()
    {
        _customers
            .Setup(c => c.FindByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        GivenAddresses(0);

        var failure = await Should.ThrowAsync<ArgumentException>(
            () => _sut.RegisterAsync(
                new NewCustomerDetails("Elvin", "0551234567", "Gəncə", "Sarayevo", "12", null), default));

        failure.Message.ShouldContain("Gəncə");
    }

    private void GivenPhoneMatches(params Customer[] customers)
        => _customers
            .Setup(c => c.FindByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customers);

    private void GivenAddresses(int customerId, params CustomerAddress[] addresses)
        => _customers
            .Setup(c => c.GetAddressesAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(addresses);

    private static NewCustomerDetails Details(string name, string phone, string street = "Sarayevo")
        => new(name, phone, "Xətai", street, "12", null);

    private static CustomerAddress Address(int customerId, string district, string street, string building)
        => CustomerAddress.Create(
            customerId, district, null, street, null, building, null, null, null, null, isDefault: true, Now);

    private static T WithId<T>(T entity, int id)
        where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity, id);
        return entity;
    }
}
