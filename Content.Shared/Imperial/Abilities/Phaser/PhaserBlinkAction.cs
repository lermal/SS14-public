using Content.Shared.Actions;
using Content.Shared.Ninja.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Abilities.Phaser;

[RegisterComponent, NetworkedComponent, Access(typeof(DashAbilitySystem)), AutoGenerateComponentState]
public sealed partial class PhaserBlinkActionComponent : Component
{
    /// <summary>
    /// The action id for dashing.
    /// </summary>
    [DataField]
    public EntProtoId<WorldTargetActionComponent> DashAction = "ActionPhaserBlink";

    [DataField, AutoNetworkedField]
    public EntityUid? DashActionEntity;
}

public sealed partial class PhaserDashEvent : EntityWorldTargetActionEvent;
