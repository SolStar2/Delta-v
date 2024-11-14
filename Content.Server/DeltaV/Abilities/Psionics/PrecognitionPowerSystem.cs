using Content.Server.Chat.Managers;
using Content.Server.DoAfter;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Actions.Events;
using Content.Shared.Actions;
using Content.Shared.DeltaV.StationEvents;
using Content.Shared.DoAfter;
using Content.Shared.Psionics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Abilities.Psionics
{
    public sealed class PrecognitionPowerSystem : EntitySystem
    {
        [Dependency] public readonly GameTicker GameTicker = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly MindSystem _mind = default!;
        [Dependency] private readonly IComponentFactory _factory = default!;
        [Dependency] private readonly IChatManager _chat = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PrecognitionPowerComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<PrecognitionPowerComponent, ComponentShutdown>(OnShutdown);
            SubscribeLocalEvent<PrecognitionPowerComponent, PrecognitionPowerActionEvent>(OnPowerUsed);

            SubscribeLocalEvent<PrecognitionPowerComponent, PrecognitionDoAfterEvent>(OnDoAfter);
        }

        private void OnInit(EntityUid uid, PrecognitionPowerComponent component, ComponentInit args)
        {
            _actions.AddAction(uid, ref component.PrecognitionActionEntity, component.PrecognitionActionId);
            _actions.TryGetActionData(component.PrecognitionActionEntity, out var actionData);
            if (actionData is { UseDelay: not null })
                _actions.StartUseDelay(component.PrecognitionActionEntity);
            if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            {
                psionic.PsionicAbility = component.PrecognitionActionEntity;
                psionic.ActivePowers.Add(component);
            }
        }

        private void OnShutdown(EntityUid uid, PrecognitionPowerComponent component, ComponentShutdown args)
        {
            _actions.RemoveAction(uid, component.PrecognitionActionEntity);
            if (TryComp<PsionicComponent>(uid, out var psionic))
            {
                psionic.ActivePowers.Remove(component);
            }
        }

        private void OnPowerUsed(EntityUid uid, PrecognitionPowerComponent component, PrecognitionPowerActionEvent args)
        {
            var ev = new PrecognitionDoAfterEvent(_gameTiming.CurTime);
            var doAfterArgs = new DoAfterArgs(EntityManager, uid, component.UseDelay, ev, uid);

            _doAfterSystem.TryStartDoAfter(doAfterArgs, out var doAfterId);
            component.DoAfter = doAfterId;

            _psionics.LogPowerUsed(uid, "Precognition");
            args.Handled = true;
        }

        private void OnDoAfter(EntityUid uid, PrecognitionPowerComponent component, PrecognitionDoAfterEvent args)
        {
            component.DoAfter = null;
        }
    private NextEventComponent? FindNextEvent(TimeSpan minDetectWindow, TimeSpan maxDetectWindow)
        {
            NextEventComponent? earliestNextEvent = null;
            TimeSpan? earliestNextEventTime = null;
            var query = EntityQueryEnumerator<NextEventComponent>();
            while (query.MoveNext(out var uid, out var nextEventComponent))
            {
                // Update if the event is the most recent event that isnt too close or too far from happening to be of use
                if (nextEventComponent.NextEventTime > GameTicker.RoundDuration() + minDetectWindow
                    && nextEventComponent.NextEventTime < GameTicker.RoundDuration() + maxDetectWindow
                    && nextEventComponent.NextEventTime < earliestNextEventTime)
                    earliestNextEvent ??= nextEventComponent;
            }
            return earliestNextEvent;
        }
        public Dictionary<EntityPrototype, PrecognitionResultComponent> AllPrecognitionResults()
        {
            var allEvents = new Dictionary<EntityPrototype, PrecognitionResultComponent>();
            foreach (var prototype in _prototype.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract)
                    continue;

                if (!prototype.TryGetComponent<PrecognitionResultComponent>(out var precognitionResult, _factory))
                    continue;

                allEvents.Add(prototype, precognitionResult);
            }

            return allEvents;
        }
    }
}
