using System.Linq;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Components;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Content.Shared.Roles; // Frontier
using Robust.Shared.Prototypes; // Frontier
using Content.Shared.Access.Systems; // Frontier
using Content.Server.Station.Components; // Frontier
using Content.Server._NF.Station.Components; // Frontier
using Content.Server.Administration.Logs; // Frontier
using Content.Shared.Database; // Frontier
using Content.Shared._NF.StationRecords; // Frontier
using Content.Shared._Scav.Persistence; // Scav

namespace Content.Server.StationRecords.Systems;

public sealed class GeneralStationRecordConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly StationJobsSystem _stationJobsSystem = default!; // Frontier
    [Dependency] private readonly AccessReaderSystem _access = default!; // Frontier
    [Dependency] private readonly IPrototypeManager _proto = default!; // Frontier
    [Dependency] private readonly IAdminLogManager _adminLog = default!; // Frontier

    public override void Initialize()
    {
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordModifiedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, AfterGeneralRecordCreatedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordRemovedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, GridReInitEvent>(OnGridReInit);

        Subs.BuiEvents<GeneralStationRecordConsoleComponent>(GeneralStationRecordConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            subs.Event<DeleteStationRecord>(OnRecordDelete);
            subs.Event<AdjustStationJobMsg>(OnAdjustJob); // Frontier
            subs.Event<SetStationAdvertisementMsg>(OnAdvertisementChanged); // Frontier
            subs.Event<SetStationJobMsg>(OnSetJob); // Scav
        });
    }

    private void OnRecordDelete(Entity<GeneralStationRecordConsoleComponent> ent, ref DeleteStationRecord args)
    {
        if (!ent.Comp.CanDeleteEntries)
            return;

        var owning = _station.GetOwningStation(ent.Owner);

        if (owning != null)
            _stationRecords.RemoveRecord(new StationRecordKey(args.Id, owning.Value));
        UpdateUserInterface(ent); // Apparently an event does not get raised for this.
    }

    private void UpdateUserInterface<T>(Entity<GeneralStationRecordConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    // TODO: instead of copy paste shitcode for each record console, have a shared records console comp they all use
    // then have this somehow play nicely with creating ui state
    // if that gets done put it in StationRecordsSystem console helpers section :)
    private void OnKeySelected(Entity<GeneralStationRecordConsoleComponent> ent, ref SelectStationRecord msg)
    {
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(ent);
    }

    // Frontier: job counts, advertisements
    private void OnAdjustJob(Entity<GeneralStationRecordConsoleComponent> ent, ref AdjustStationJobMsg msg)
    {
        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is EntityUid station)
        {
            // Frontier: check access - hack because we don't have an AccessReaderComponent, it's the station
            if (TryComp(stationUid, out StationJobsComponent? stationJobs) &&
                (stationJobs.Groups.Count > 0 || stationJobs.Tags.Count > 0))
            {
                var accessSources = _access.FindPotentialAccessItems(msg.Actor);
                var access = _access.FindAccessTags(msg.Actor, accessSources);

                // Check access groups and tags
                bool hasAccess = stationJobs.Tags.Any(access.Contains);
                if (!hasAccess)
                {
                    foreach (var group in stationJobs.Groups)
                    {
                        if (!_proto.TryIndex(group, out var accessGroup))
                            continue;

                        hasAccess = accessGroup.Tags.Any(access.Contains);
                        if (hasAccess)
                            break;
                    }
                }

                if (!hasAccess)
                {
                    UpdateUserInterface(ent);
                    return;
                }
            }
            // End Frontier
            _stationJobsSystem.TryAdjustJobSlot(station, msg.JobProto, msg.Amount, false, true);
            UpdateUserInterface(ent);
        }
    }
    private void OnFiltersChanged(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null ||
            ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent);
        }
    }

    private void OnAdvertisementChanged(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationAdvertisementMsg msg)
    {
        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is EntityUid station
            && TryComp<ExtraShuttleInformationComponent>(station, out var vesselInfo))
        {
            vesselInfo.Advertisement = msg.Advertisement;
            _adminLog.Add(LogType.ShuttleInfoChanged, $"{ToPrettyString(msg.Actor):actor} set their shuttle {ToPrettyString(station)}'s ad text to {vesselInfo.Advertisement}");
            UpdateUserInterface(ent);
            _stationJobsSystem.UpdateJobsAvailable(); // Nasty - ideally this sends out partial information - one ship changed its advertisement.
        }
    }
    // End Frontier: job counts, advertisements

    private void UpdateUserInterface(Entity<GeneralStationRecordConsoleComponent> ent)
    {
        var (uid, console) = ent;
        var owningStation = _station.GetOwningStation(uid);

        // Frontier: jobs, advertisements
        IReadOnlyDictionary<ProtoId<JobPrototype>, int?>? jobList = null;
        string? advertisement = null;
        if (owningStation != null)
        {
            jobList = _stationJobsSystem.GetJobs(owningStation.Value);
            if (TryComp<ExtraShuttleInformationComponent>(owningStation, out var extraVessel))
            {
                advertisement = extraVessel.Advertisement;

            }
        }

        if (!TryComp<StationRecordsComponent>(owningStation, out var stationRecords))
        {
            _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, new GeneralStationRecordConsoleState(null, null, null, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, ent.Comp.AllJobsAvalible, ent.Comp.UseAllJobsToggle)); // Frontier: add as many args as we can, scav too
            return;
        }

        var listing = _stationRecords.BuildListing((owningStation.Value, stationRecords), console.Filter);

        switch (listing.Count)
        {
            case 0:
                var consoleState = new GeneralStationRecordConsoleState(null, null, null, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, ent.Comp.AllJobsAvalible, ent.Comp.UseAllJobsToggle); // Frontier: add as many args as we can, scav too
                _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, consoleState);
                return;
            default:
                if (console.ActiveKey == null)
                    console.ActiveKey = listing.Keys.First();
                break;
        }

        if (console.ActiveKey is not { } id)
        {
            _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, new GeneralStationRecordConsoleState(null, null, listing, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, ent.Comp.AllJobsAvalible, ent.Comp.UseAllJobsToggle)); // Frontier: add as many args as we can, scav too
            return;
        }

        var key = new StationRecordKey(id, owningStation.Value);
        _stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record, stationRecords);

        GeneralStationRecordConsoleState newState = new(id, record, listing, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, ent.Comp.AllJobsAvalible, ent.Comp.UseAllJobsToggle); //scav added arg
        _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, newState);
    }

    //scav explicit set job message handler, checks copied from frontier adjust job handler
    private void OnSetJob(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationJobMsg msg)
    {
        var stationUid = _station.GetOwningStation(ent);
        IReadOnlyDictionary<ProtoId<JobPrototype>, int?>? jobList = null;
        if (stationUid is EntityUid station)
        {
            if (TryComp(stationUid, out StationJobsComponent? stationJobs) &&
                (stationJobs.Groups.Count > 0 || stationJobs.Tags.Count > 0))
            {
                var accessSources = _access.FindPotentialAccessItems(msg.Actor);
                var access = _access.FindAccessTags(msg.Actor, accessSources);
                bool hasAccess = stationJobs.Tags.Any(access.Contains);
                if (!hasAccess)
                {
                    foreach (var group in stationJobs.Groups)
                    {
                        if (!_proto.TryIndex(group, out var accessGroup))
                            continue;

                        hasAccess = accessGroup.Tags.Any(access.Contains);
                        if (hasAccess)
                            break;
                    }
                }
                if (!hasAccess)
                {
                    UpdateUserInterface(ent);
                    return;
                }
            }
            jobList = _stationJobsSystem.GetJobs(stationUid.Value);
            foreach (var job in jobList.Keys)
            {
                if(msg.State)
                    _stationJobsSystem.TrySetJobSlot(station, job, 0, false, null, true);
                else
                    _stationJobsSystem.TrySetJobSlot(station, job, -1, false, null, true);
            }
            // Toggle hiring status
            ent.Comp.AllJobsAvalible = !msg.State;
            UpdateUserInterface(ent);
        }
    }

    private void OnGridReInit(Entity<GeneralStationRecordConsoleComponent> ent, ref GridReInitEvent args)// Scav reset hiring status on gridreinit event for persitent ships
    {
        ent.Comp.AllJobsAvalible = false;
    }// Scav end
}
