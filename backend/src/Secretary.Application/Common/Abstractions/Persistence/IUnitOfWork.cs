namespace Secretary.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    ITenantRepository Tenants { get; }
    IAccountRepository Accounts { get; }
    IProviderRepository Providers { get; }
    IServiceOfferingRepository ServiceOfferings { get; }
    IProviderServiceOfferingRepository ProviderServiceOfferings { get; }
    IClientRepository Clients { get; }
    IAppointmentRepository Appointments { get; }
    ICallRepository Calls { get; }
    IEscalationRepository Escalations { get; }
    ITenantModuleRepository TenantModules { get; }
    IBusinessHoursRepository BusinessHours { get; }

    // Orders module.
    IMeasurementUnitRepository Units { get; }
    IProductRepository Products { get; }
    ICustomerRepository Customers { get; }
    IOrderRepository Orders { get; }
    IOrderSettingsRepository OrderSettings { get; }
    IOrderCallRepository OrderCalls { get; }

    // Feedback module.
    ISurveyRepository Surveys { get; }
    ISurveyQuestionRepository SurveyQuestions { get; }
    ISurveyRequestRepository SurveyRequests { get; }
    IFeedbackCallRepository FeedbackCalls { get; }
    IFeedbackAnswerRepository FeedbackAnswers { get; }
    IFeedbackSettingsRepository FeedbackSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Throws away everything pending, so a failed save cannot be retried by the next
    /// one.
    ///
    /// A rejected insert stays tracked, and every later SaveChanges on the same request tries it
    /// again. One bad order took the escalation and the call-log write down with it — the caller
    /// was neither transferred nor recorded, because of a row that was never going to save.
    /// Whoever handles the failure calls this before doing anything else that writes.</summary>
    void DiscardPendingChanges();
}
