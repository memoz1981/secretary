using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions;

/// <summary>Flow E: what should happen for each appointment that needs a 9am reminder call
/// today. Deliberately a swappable interface (mirroring the repository pattern's
/// swappability) rather than the scheduler calling a concrete "place a phone call" method
/// directly — no outbound telephony exists yet (see backend/README.md), so the default
/// implementation just logs; the real implementation, once outbound calling exists (real
/// telephony, or driving RealtimeVoiceSession outbound instead of only inbound), plugs in
/// here without the scheduler itself changing at all.</summary>
public interface IOutboundReminderNotifier
{
    Task NotifyReminderDueAsync(Appointment appointment, CancellationToken cancellationToken);
}
