using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Abilities.Urs;

public sealed partial class UrsDashAction : EntityWorldTargetActionEvent
{
    /// <summary>
    /// The ID of the entity that is spawned.
    /// </summary>
    [DataField]
    public EntProtoId EntityId = "EffectGoliathTentacleSpawn";

    /// <summary>
    /// Directions determining where the entities will spawn.
    /// </summary>
    [DataField]
    public List<Direction> OffsetDirections = new()
    {
        Direction.North,
        Direction.South,
        Direction.East,
        Direction.West,
    };
};
