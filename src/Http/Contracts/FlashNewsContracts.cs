using System.ComponentModel.DataAnnotations;

namespace Mgo2Server.Http.Contracts;

/// <summary>Fields of a server message broadcast.</summary>
public sealed class FlashNewsBroadcastRequest
{
    /// <summary>Text shown in the client's ticker.</summary>
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public required string Message { get; set; }

    /// <summary>First byte of the leading field, whose purpose is unknown.</summary>
    [Range(0, 0xff)]
    public byte Unknown1 { get; set; }

    /// <summary>Second byte of the leading field, whose purpose is unknown.</summary>
    [Range(0, 0xff)]
    public byte Unknown2 { get; set; }

    /// <summary>Fifth byte of the payload, whose purpose is unknown.</summary>
    [Range(0, 0xff)]
    public byte Unknown5 { get; set; } = 0x01;

    /// <summary>Sixth byte of the payload, used as the maintenance time.</summary>
    [Range(0, 0xff)]
    public byte Unknown6 { get; set; }
}

/// <summary>Fields of an emergency maintenance broadcast.</summary>
public sealed class FlashNewsEmergencyRequest
{
    /// <summary>Maintenance start time, whose encoding is not yet known.</summary>
    [Range(0, 0xff)]
    public byte MaintenanceTime { get; set; }

    /// <summary>First byte of the leading field, whose purpose is unknown.</summary>
    [Range(0, 0xff)]
    public byte Unknown1 { get; set; }

    /// <summary>Second byte of the leading field, whose purpose is unknown.</summary>
    [Range(0, 0xff)]
    public byte Unknown2 { get; set; }

    /// <summary>Fifth byte of the payload, whose purpose is unknown.</summary>
    [Range(0, 0xff)]
    public byte Unknown5 { get; set; } = 0x01;
}
