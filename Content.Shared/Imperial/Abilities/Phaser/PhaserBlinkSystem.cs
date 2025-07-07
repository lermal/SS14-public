using System.Diagnostics;
using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Abilities.Phaser;

public sealed class PhaserBlinkSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PhaserBlinkActionComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<PhaserBlinkActionComponent, PhaserDashEvent>(OnDash);
        SubscribeLocalEvent<PhaserBlinkActionComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PhaserBlinkActionComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.DashActionEntity, comp.DashAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<PhaserBlinkActionComponent> ent, ref GetItemActionsEvent args)
    {
        if (CheckDash(ent, args.User))
            args.AddAction(ent.Comp.DashActionEntity);
    }

    /// <summary>
    /// Handle charges and teleport to a visible location.
    /// </summary>
    private void OnDash(Entity<PhaserBlinkActionComponent> ent, ref PhaserDashEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var (uid, _) = ent;
        var user = args.Performer;
        if (!CheckDash(uid, user))
            return;

        if (args.Handled || args.Coords is not { } coords)
            return;

        var origin = _transform.GetMapCoordinates(user);
        var target = _transform.ToMapCoordinates(coords);
        if (!_examine.InRangeUnOccluded(origin, target, SharedInteractionSystem.MaxRaycastRange, null))
        {
            // can only dash if the destination is visible on screen
            _popup.PopupClient(Loc.GetString("dash-ability-cant-see", ("item", uid)), user, user);
            return;
        }

        // Check if the user is BEING pulled, and escape if so
        if (TryComp<PullableComponent>(user, out var pull) && _pullingSystem.IsPulled(user, pull))
            _pullingSystem.TryStopPull(user, pull);

        // Check if the user is pulling anything, and drop it if so
        if (TryComp<PullerComponent>(user, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
            _pullingSystem.TryStopPull(puller.Pulling.Value, pullable);

        var xform = Transform(user);

        var direction = (origin.Position - target.Position).Normalized();
        var distance = 1.0f; // расстояние позади цели
        var behindTarget = target.Position + direction * new Vector2(distance, distance);

        Logger.Debug($"PhaserBlink: {user} dashing to {coords} from {xform.Coordinates}");
        if (args.Entity == null)
        {
            _transform.SetCoordinates(user, xform, coords);
            Logger.Info("Entity is null, teleporting directly to coordinates.");
        }
        else
        {
            _transform.SetCoordinates(user, xform, new EntityCoordinates(args.Entity.Value, behindTarget));
            Logger.Info("Entity is not null, teleporting to entity.");
        }

        _transform.AttachToGridOrMap(user, xform);
        args.Handled = true;
    }

    public bool CheckDash(EntityUid uid, EntityUid user)
    {
        var ev = new CheckDashEvent(user);
        RaiseLocalEvent(uid, ref ev);
        return !ev.Cancelled;
    }
}
