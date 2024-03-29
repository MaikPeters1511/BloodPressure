using System.ComponentModel.DataAnnotations;

namespace CleanArchitecture.Domain.Entities;

public class BloodPressure
{
    public int Id { get; set; }

    [Range(0,500)]
    public int Systolisch { get; set; }

    [Range(0,500)]
    public int Diastolisch { get; set; }

    public DateTime DateTime { get; set; } = new DateTime();

    [Range(0,500)]
    public int Pulse { get; set; }

    [Range(0,100)]
    public int Oxygen { get; set; }

}