using System;

namespace PetPet.Domain.Entities;

public class SeedRecord
{
    public int Id { get; set; }
    public int GeneratorVersion { get; set; }
    public long Seed { get; set; }
    public DateTime BaseTimeUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public string CountsJson { get; set; } = null!;
    public string BusinessFingerprint { get; set; } = null!;
}
