using Vint.Core.Battles.Player;
using Vint.Core.Battles.Weapons;
using Vint.Core.ECS.Entities;
using Vint.Core.ECS.Templates.Battle.Effect;

namespace Vint.Core.Battles.Effects;

public class ExternalImpactEffect(
    TimeSpan cooldown,
    IEntity marketEntity,
    float radius,
    float minPercent,
    float maxDamage,
    float minDamage,
    float impact,
    BattleTank tank,
    int level
) : Effect(tank, level), IModuleWeaponEffect {
    public ModuleWeaponHandler WeaponHandler { get; private set; } = null!;

    public override async Task Activate() {
        if (IsActive) return;

        Tank.Effects.Add(this);

        IEntity entity = new ExternalImpactEffectTemplate().Create(Tank.BattlePlayer,
            Duration,
            Battle.Properties.FriendlyFire,
            impact * Tank.Weight.Weight / 1000, // this thing converts impact force to multiplier, cuz tanks weight thousands of tons, hope its not critical :pray: - J
            minPercent,
            0,
            radius);

        WeaponHandler = new ExternalImpactWeaponHandler(Tank,
            cooldown,
            marketEntity,
            entity,
            true,
            0,
            radius,
            minPercent,
            maxDamage,
            minDamage,
            int.MaxValue);

        Entities.Add(entity);

        await ShareAll();
        Schedule(Duration, Deactivate);
    }

    public override async Task Deactivate() {
        if (!IsActive) return;

        Tank.Effects.TryRemove(this);

        await UnshareAll();
        Entities.Clear();
    }
}
