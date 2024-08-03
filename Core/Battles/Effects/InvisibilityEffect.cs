using System;
using Vint.Core.Battles.Player;
using Vint.Core.ECS.Templates.Battle.Effect;
using Vint.Core.Config;
using DurationComponent = Vint.Core.ECS.Components.Battle.Effect.DurationComponent;
using EffectDurationComponent = Vint.Core.ECS.Components.Server.DurationComponent;

namespace Vint.Core.Battles.Effects;

public sealed class InvisibilityEffect : DurationEffect, ISupplyEffect {
    const string MarketConfigPath = "garage/module/upgrade/properties/invisibility";
    public InvisibilityEffect(TimeSpan duration,BattleTank tank, int level = -1) : base(tank, level, MarketConfigPath) {
        Duration = duration;
    }
    public event Action? Deactivated;

    public override async Task Activate() {
        if (IsActive) return;

        Tank.Effects.Add(this);

        Entities.Add(new InvisibilityEffectTemplate().Create(Tank.BattlePlayer, Duration));
        await ShareAll();

        Schedule(Duration, Deactivate);
    }

    public float SupplyMultiplier => 0;
    public float SupplyDurationMs { get; }
    public override async Task Deactivate() {
        if (!IsActive) return;

        Tank.Effects.TryRemove(this);

        await UnshareAll();
        Entities.Clear();
        Deactivated?.Invoke();
    }
}
