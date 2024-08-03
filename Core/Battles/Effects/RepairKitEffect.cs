using System;
using Vint.Core.Battles.Damage;
using Vint.Core.Battles.Player;
using Vint.Core.Config;
using Vint.Core.ECS.Components.Battle.Effect;
using Vint.Core.ECS.Components.Server;
using Vint.Core.ECS.Components.Server.Effect;
using Vint.Core.ECS.Templates.Battle.Effect;
using Vint.Core.Utils;
using DurationComponent = Vint.Core.ECS.Components.Battle.Effect.DurationComponent;
using EffectDurationComponent = Vint.Core.ECS.Components.Server.DurationComponent;

namespace Vint.Core.Battles.Effects;

public sealed class RepairKitEffect : DurationEffect, ISupplyEffect, IExtendableEffect {
    const string EffectConfigPath = "battle/effect/healing";
    const string MarketConfigPath = "garage/module/upgrade/properties/repairkit";

    public RepairKitEffect(BattleTank tank, int level = -1) : base(tank, level, MarketConfigPath) {
        SupplyHealingComponent = ConfigManager.GetComponent<HealingComponent>(EffectConfigPath);

        InstantHp = IsSupply ? 0 : Leveling.GetStat<ModuleHealingEffectInstantHPPropertyComponent>(MarketConfigPath, Level);
        Percent = IsSupply ? SupplyHealingComponent.Percent : Leveling.GetStat<ModuleHealingEffectPercentPropertyComponent>(MarketConfigPath, Level);
        SupplyDurationMs = ConfigManager.GetComponent<EffectDurationComponent>(EffectConfigPath).Duration * Tank.SupplyDurationMultiplier;
        TickPeriod = TimeSpan.FromMilliseconds(ConfigManager.GetComponent<TickComponent>(EffectConfigPath).Period);

        Heal = HealLeft = Tank.MaxHealth * Percent;
        HealPerMs = (float)(Heal / Duration.TotalMilliseconds);

        if (IsSupply)
            Duration = TimeSpan.FromMilliseconds(SupplyDurationMs);
    }

    HealingComponent SupplyHealingComponent { get; }
    public event Action? Deactivated;

    float InstantHp { get; set; }
    float Percent { get; set; }
    TimeSpan TickPeriod { get; }

    DateTimeOffset LastTick { get; set; }
    TimeSpan TimePassedFromLastTick => DateTimeOffset.UtcNow - LastTick;

    float Heal { get; set; }
    float HealLeft { get; set; }
    float HealPerMs { get; set; }

    public async Task Extend(int newLevel) {
        if (!IsActive) return;

        UnScheduleAll();

        LastTick = DateTimeOffset.UtcNow.AddTicks(-TickPeriod.Ticks);

        bool isSupply = newLevel < 0;

        if (isSupply) {
            Duration = TimeSpan.FromMilliseconds(SupplyDurationMs);
            Percent = SupplyHealingComponent.Percent;
        } else {
            Duration = TimeSpan.FromMilliseconds(Leveling.GetStat<ModuleEffectDurationPropertyComponent>(MarketConfigPath, newLevel));
            InstantHp = Leveling.GetStat<ModuleHealingEffectInstantHPPropertyComponent>(MarketConfigPath, newLevel);
            Percent = Leveling.GetStat<ModuleHealingEffectPercentPropertyComponent>(MarketConfigPath, newLevel);

            CalculatedDamage heal = new(default, InstantHp, false, false);
            await Battle.DamageProcessor.Heal(Tank, heal);
        }

        Level = newLevel;

        Heal = HealLeft = Tank.MaxHealth * Percent;
        HealPerMs = (float)(Heal / Duration.TotalMilliseconds);

        await Entity!.ChangeComponent<DurationConfigComponent>(component => component.Duration = Convert.ToInt64(Duration.TotalMilliseconds));
        await Entity!.RemoveComponent<DurationComponent>();
        await Entity!.AddComponent(new DurationComponent(DateTimeOffset.UtcNow));

        Schedule(Duration, Deactivate);
    }

    public float SupplyMultiplier => 0;
    public float SupplyDurationMs { get; }

    public override async Task Activate() {
        if (IsActive) return;

        Tank.Effects.Add(this);

        CalculatedDamage heal = new(default, InstantHp, false, false);

        LastTick = DateTimeOffset.UtcNow.AddTicks(-TickPeriod.Ticks);
        await Battle.DamageProcessor.Heal(Tank, heal);

        Entities.Add(new HealingEffectTemplate().Create(Tank.BattlePlayer, Duration));
        await ShareAll();

        Schedule(Duration, Deactivate);
    }

    public override async Task Deactivate() {
        if (!IsActive) return;

        Tank.Effects.TryRemove(this);

        await UnshareAll();
        Entities.Clear();

        if (HealLeft <= 0 || Tank.Health >= Tank.MaxHealth) return;

        Deactivated?.Invoke();
    }

    public override async Task Tick() {
        await base.Tick();

        TimeSpan timePassed = TimePassedFromLastTick;

        if (!IsActive || HealLeft <= 0 || timePassed < TickPeriod) return;

        LastTick = DateTimeOffset.UtcNow;

        float healHp = Math.Min(Math.Min(Convert.ToSingle(timePassed.TotalMilliseconds * HealPerMs), Tank.MaxHealth - Tank.Health), HealLeft);
        HealLeft -= healHp;

        if (Tank.Health >= Tank.MaxHealth) return;

        CalculatedDamage heal = new(default, healHp, false, false);
        await Battle.DamageProcessor.Heal(Tank, heal);
    }
}
