using Content.Shared.Damage;

namespace Content.Shared.Climbing.Components;

/// <summary>
///     Glass tables shatter and stun you when climbed on.
///     This is a really entity-specific behavior, so opted to make it
///     not very generalized with regards to naming.
/// </summary>
[RegisterComponent, Access(typeof(Systems.ClimbSystem))]
public sealed partial class PipeMovementComponent : Component
{
    /// <summary>
    ///     Whether the entity is in a pipe.
    /// </summary>
    [DataField("isInPipe")]
    public bool IsInPipe = false;

    /// <summary>
    ///     The speed of the entity when it is in a pipe.
    /// </summary>
    [DataField("speedInPipe")]
    public float SpeedInPipe = 1.0f;

    /// <summary>
    ///     The speed of exit from pipe
    /// </summary>
    [DataField("speedExitPipe")]
    public float SpeedExitPipe = 1.0f;

    /// <summary>
    ///     The speed of enter in pipe
    /// </summary>
    [DataField("speedEnterPipe")]
    public float SpeedEnterPipe = 1.0f;
}
