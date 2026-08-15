using Secretary.Application.Abstractions.Persistence;

namespace Secretary.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(
        AppDbContext db,
        ITenantRepository tenants,
        IAccountRepository accounts,
        IProviderRepository providers,
        IServiceOfferingRepository serviceOfferings,
        IProviderServiceOfferingRepository providerServiceOfferings,
        IClientRepository clients,
        IAppointmentRepository appointments,
        ICallRepository calls,
        IEscalationRepository escalations,
        ITenantModuleRepository tenantModules,
        IBusinessHoursRepository businessHours,
        IMeasurementUnitRepository units,
        IProductRepository products,
        ICustomerRepository customers,
        IOrderRepository orders,
        IOrderSettingsRepository orderSettings,
        IOrderCallRepository orderCalls,
        ISurveyRepository surveys,
        ISurveyQuestionRepository surveyQuestions,
        IFeedbackCallRepository feedbackCalls,
        IFeedbackAnswerRepository feedbackAnswers,
        IFeedbackSettingsRepository feedbackSettings)
    {
        _db = db;
        Units = units;
        Products = products;
        Customers = customers;
        Orders = orders;
        OrderSettings = orderSettings;
        OrderCalls = orderCalls;
        Surveys = surveys;
        SurveyQuestions = surveyQuestions;
        FeedbackCalls = feedbackCalls;
        FeedbackAnswers = feedbackAnswers;
        FeedbackSettings = feedbackSettings;
        Tenants = tenants;
        Accounts = accounts;
        Providers = providers;
        ServiceOfferings = serviceOfferings;
        ProviderServiceOfferings = providerServiceOfferings;
        Clients = clients;
        Appointments = appointments;
        Calls = calls;
        Escalations = escalations;
        TenantModules = tenantModules;
        BusinessHours = businessHours;
    }

    public ITenantRepository Tenants { get; }
    public IAccountRepository Accounts { get; }
    public IProviderRepository Providers { get; }
    public IServiceOfferingRepository ServiceOfferings { get; }
    public IProviderServiceOfferingRepository ProviderServiceOfferings { get; }
    public IClientRepository Clients { get; }
    public IAppointmentRepository Appointments { get; }
    public ICallRepository Calls { get; }
    public IEscalationRepository Escalations { get; }
    public ITenantModuleRepository TenantModules { get; }
    public IBusinessHoursRepository BusinessHours { get; }
    public IMeasurementUnitRepository Units { get; }
    public IProductRepository Products { get; }
    public ICustomerRepository Customers { get; }
    public IOrderRepository Orders { get; }
    public IOrderSettingsRepository OrderSettings { get; }
    public IOrderCallRepository OrderCalls { get; }
    public ISurveyRepository Surveys { get; }
    public ISurveyQuestionRepository SurveyQuestions { get; }
    public IFeedbackCallRepository FeedbackCalls { get; }
    public IFeedbackAnswerRepository FeedbackAnswers { get; }
    public IFeedbackSettingsRepository FeedbackSettings { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);

    /// <summary>Detaches everything the change tracker is holding. Blunt on purpose: after a
    /// failed save the only safe assumption is that nothing pending is trustworthy, and the
    /// alternative — unpicking which entity caused it — is guesswork at the worst moment.</summary>
    public void DiscardPendingChanges() => _db.ChangeTracker.Clear();
}
