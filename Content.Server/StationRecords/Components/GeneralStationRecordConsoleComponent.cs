using Content.Server.StationRecords.Systems;
using Content.Shared.StationRecords;

namespace Content.Server.StationRecords.Components;

[RegisterComponent, Access(typeof(GeneralStationRecordConsoleSystem))]
public sealed partial class GeneralStationRecordConsoleComponent : Component
{
    /// <summary>
    /// Selected crewmember record id.
    /// Station always uses the station that owns the console.
    /// </summary>
    [DataField]
    public uint? ActiveKey;

    /// <summary>
    /// Qualities to filter a search by.
    /// </summary>
    [DataField]
    public StationRecordsFilter? Filter;

    /// <summary>
    /// Whether this Records Console is able to delete entries.
    /// </summary>
    [DataField]
    public bool CanDeleteEntries;

    /// <summary>
    /// Whether the station is currently hiring or not.
    /// </summary>
    [DataField]
    public bool AllJobsAvalible; //scav'

    /// <summary>
    /// Whether the console is ship or station one.
    /// </summary>
    [DataField]
    public bool UseAllJobsToggle; //scav
}
