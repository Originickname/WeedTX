using Vint.Core.Battles.Player;
using Vint.Core.Battles.Weapons;
using Vint.Core.ECS.Entities;
using Vint.Core.ECS.Templates.Battle.Effect;
using Vint.Core.Utils;

namespace Vint.Core.Battles.Effects;

public class ExplosiveMassEffect(
    TimeSpan cooldown,
    IEntity marketEntity,
    float radius,
    float maxDamage,
    float minDamage,
    BattleTank tank,
    int level
) : Effect(tank, level), IModuleWeaponEffect {
    public ModuleWeaponHandler WeaponHandler { get; private set; } = null!;

    public override async Task Activate() {
        if (IsActive) return;

        CanBeDeactivated = false;
        Tank.Effects.Add(this);

        IEntity entity = new ExplosiveMassEffectTemplate().Create(Tank.BattlePlayer,
            Duration,
            Battle.Properties.FriendlyFire,
            0,
            radius);

        WeaponHandler = new ExplosiveMassWeaponHandler(Tank,
            cooldown,
            marketEntity,
            entity,
            true,
            0,
            0,
            0,
            maxDamage,
            minDamage,
            int.MaxValue);

        Entities.Add(entity);

        await Share(Tank.BattlePlayer);
        Schedule(Duration, DeactivateInternal);
    }

    public override async Task Deactivate() {
        if (!IsActive || !CanBeDeactivated) return;

        Tank.Effects.TryRemove(this);
        await Unshare(Tank.BattlePlayer);

        Entities.Clear();
    }

    async Task DeactivateInternal() {
        CanBeDeactivated = true;
        await Deactivate();
    }

    public override async Task Share(BattlePlayer battlePlayer) {
        if (battlePlayer.Tank != Tank) return;

        await battlePlayer.PlayerConnection.Share(Entities);
    }

    public override async Task Unshare(BattlePlayer battlePlayer) {
        if (battlePlayer.Tank != Tank) return;

        await battlePlayer.PlayerConnection.Unshare(Entities);
    }
}
