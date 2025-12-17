using System;

namespace PetPet.Domain.Events;

public class MatchSuccessEvent
{
    public string SourceEmail { get; set; } = string.Empty;
    public string TargetEmail { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
