using System.ComponentModel.DataAnnotations;

namespace CleanArchitecture.Domain.Entities;

public class BloodPressure
{
    public int Id { get; set; }

    [MaxLength(3)]
    public int Systolisch { get; set; }

    [MaxLength(3)]
    public int Diastolisch { get; set; }

    public DateTime DateTime { get; set; } = new DateTime();

    [MaxLength(3)]
    public int Pulse { get; set; }

    [MaxLength(3)]
    public int Oxygen { get; set; }

}