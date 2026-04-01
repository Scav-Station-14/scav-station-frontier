using Robust.Shared.Serialization;

namespace Content.Shared._NF.StationRecords;

[Serializable, NetSerializable]
public sealed class AdjustStationJobMsg : BoundUserInterfaceMessage
{
    public string JobProto { get; }
    public int Amount { get; }

    public AdjustStationJobMsg(string jobProto, int amount)
    {
        JobProto = jobProto;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class SetStationAdvertisementMsg : BoundUserInterfaceMessage
{
    public string Advertisement { get; }

    public SetStationAdvertisementMsg(string advertisement)
    {
        Advertisement = advertisement;
    }
}

[Serializable, NetSerializable]
public sealed class SetStationJobMsg : BoundUserInterfaceMessage //scav message for setting jobs
{
    public bool State { get; }

    public SetStationJobMsg( bool state)
    {

        State = state;
    }
}
