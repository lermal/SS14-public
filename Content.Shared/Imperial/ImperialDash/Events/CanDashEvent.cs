namespace Content.Shared.Imperial.ImperialDash.Events;

[ByRefEvent]
public record struct CanDashEvent(bool Cancelled = false);
