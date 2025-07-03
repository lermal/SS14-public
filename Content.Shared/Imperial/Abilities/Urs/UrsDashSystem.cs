using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Damage;
using Content.Shared.Imperial.Dash;
using Content.Shared.Imperial.ImperialDash.Components;
using Content.Shared.Imperial.ImperialDash.Events;
using Content.Shared.Imperial.PhaseSpace;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Mind.Components;
using Content.Shared.Ghost;
using Robust.Shared.Physics.Events;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Abilities.Urs;

public sealed class UrsDashSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// Словарь для отслеживания столкновений
    /// </summary>
    private readonly Dictionary<EntityUid, bool> _dashCollisions = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<UrsDashAction>(OnDashAction);
        SubscribeLocalEvent<ImperialDashComponent, StartCollideEvent>(OnStartCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<ImperialDashComponent>();

        while (enumerator.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.DashEndTime)
                continue;
            if (!component.IsDashing)
                continue;

            component.IsDashing = false;

            if (_net.IsClient)
                return;

            RemComp<PhaseSpaceShadowComponent>(uid);

            if (_dashCollisions.Count == 0)
            {
                _stun.TryStun(uid, TimeSpan.FromSeconds(3f), false);
                _popup.PopupPredicted(Loc.GetString("urs-dash-no-collision-stun"), uid, uid, type: PopupType.MediumCaution);
            }
        }
    }

    private void OnDashAction(UrsDashAction args)
    {
        if (args.Handled || args.Coords is not { } coords)
            return;

        // TODO: animation

        _popup.PopupPredicted(Loc.GetString("tentacle-ability-use-popup", ("entity", args.Performer)), args.Performer, args.Performer, type: PopupType.SmallCaution);

        if (_transform.GetGrid(coords) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            return;
        }

        if (!_map.TryGetTileRef(grid, gridComp, coords, out var tileRef) ||
            tileRef.IsSpace() ||
            _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
        {
            return;
        }

        if (_net.IsServer)
        {
            if (!CanDash(args.Performer, out var component))
                return;

            if (!TryComp<PhysicsComponent>(args.Performer, out var physicsComponent))
                return;

            if (!component.RequiredBodyStatus.Contains(physicsComponent.BodyStatus))
                return;

            var performerPos = _transform.GetMapCoordinates(args.Performer);
            var targetPos = _transform.ToMapCoordinates(coords);

            if (performerPos.MapId != targetPos.MapId)
                return;

            var dashDirection = (targetPos.Position - performerPos.Position).Normalized();
            var distanceToTarget = (targetPos.Position - performerPos.Position).Length();

            var backDirection = -dashDirection;
            var backForce = new Vector2(component.Force * 0.2f);
            var backImpulse = backDirection * backForce;

            _physicsSystem.ApplyLinearImpulse(args.Performer, backImpulse);

            Timer.Spawn(TimeSpan.FromSeconds(0.5f),
                () =>
            {
                if (!Exists(args.Performer))
                    return;

                var targetDistance = distanceToTarget * 1.2f;
                var requiredForce = targetDistance * physicsComponent.Mass * 20f; // Увеличиваем силу
                var mainForce = new Vector2(requiredForce);
                var mainImpulse = dashDirection * mainForce;
                var dashTime = TimeSpan.FromSeconds(0.5f);

                var staminaEv = new CheckDashStaminaCostModifiersEvent(1f);
                RaiseLocalEvent(args.Performer, ref staminaEv);

                var distEv = new CheckDashDistanceModifiersEvent(1f);
                RaiseLocalEvent(args.Performer, ref distEv);

                // TODO модификатор расстояния

                _physicsSystem.ApplyLinearImpulse(args.Performer, mainImpulse);

                var shadowComponent = EnsureComp<PhaseSpaceShadowComponent>(args.Performer);

                shadowComponent.ShadowUpdateRate = TimeSpan.Zero;
                shadowComponent.PositionUpdateRate = TimeSpan.Zero;

                component.DashEndTime = dashTime + _timing.CurTime;

                var cooldownEv = new CheckDashCooldownModifiersEvent(1f);
                RaiseLocalEvent(args.Performer, ref cooldownEv, true);

                component.NextDash = _timing.CurTime + component.DashReloadTime +
                                     TimeSpan.FromSeconds(staminaEv.Modifier);

                component.DashButtonPressedTick = _timing.CurTick;

                component.IsDashing = true;

                _dashCollisions[args.Performer] = false;
            });
        }

        args.Handled = true;
    }

    private bool CanDash(EntityUid uid, [NotNullWhen(true)] out ImperialDashComponent? component)
    {
        if (!TryComp(uid, out component))
            return false;

        var isSimulationTick = _timing.CurTick == component.DashButtonPressedTick;

        if (component.IsDashing && !isSimulationTick)
            return false;

        if (_timing.CurTime < component.NextDash && !isSimulationTick)
            return false;

        if (!_actionBlockerSystem.CanMove(uid))
            return false;

        var ev = new CanDashEvent();
        RaiseLocalEvent(uid, ref ev);
        return !ev.Cancelled;
    }

    private void OnStartCollide(EntityUid uid, ImperialDashComponent component, StartCollideEvent args)
    {
        if (!component.IsDashing)
            return;

        var otherEntity = args.OtherEntity;

        if (!TryComp<MindContainerComponent>(otherEntity, out _) ||
            HasComp<GhostComponent>(otherEntity))
        {
            return;
        }

        if (_dashCollisions.ContainsKey(uid))
        {
            _dashCollisions[uid] = true;
        }

        var damage = new DamageSpecifier(_prototypeManager.Index<DamageGroupPrototype>("Brute"), 20);

        _damageableSystem.TryChangeDamage(otherEntity, damage);

        _stun.TryStun(otherEntity, TimeSpan.FromSeconds(10f), false);

        _popup.PopupPredicted(Loc.GetString("urs-dash-hit-player"), otherEntity, otherEntity, type: PopupType.LargeCaution);
    }
}
