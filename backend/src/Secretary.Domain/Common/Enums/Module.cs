namespace Secretary.Domain.Enums;

/// <summary>What a tenant can be granted access to.
///
/// Hardcoded on purpose. A module is not data — each one is a body of code, a set of agent tools
/// and its own instruction file, so there is nothing an admin could usefully create at runtime.
/// What an admin does is decide which tenants get which, and that is TenantModule.
///
/// The six here are the ones the public landing page advertises. Only Appointment is built; the
/// rest exist so the grant model, the admin screen and the module picker are not written twice,
/// and so the landing page and this enum cannot drift apart.
///
/// Numbering is stable — the value is persisted on every TenantModule row.</summary>
public enum Module
{
    /// <summary>Randevu — booking, rescheduling, cancellation. The only one built.</summary>
    Appointment = 1,

    /// <summary>Məlumat xətti — answering questions about the business.</summary>
    Information = 2,

    /// <summary>Xatırlatma — outbound appointment reminders.</summary>
    Reminder = 3,

    /// <summary>Rəy və məmnuniyyət — outbound satisfaction follow-up.</summary>
    Feedback = 4,

    /// <summary>Sifariş qəbulu — taking orders.</summary>
    Order = 5,

    /// <summary>Sorğu — outbound surveys.</summary>
    Survey = 6,
}
