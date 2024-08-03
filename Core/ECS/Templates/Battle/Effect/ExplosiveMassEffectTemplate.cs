using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Effect;
using Vint.Core.ECS.Components.Battle.Weapon;
using Vint.Core.ECS.Components.Battle.Weapon.Splash;
using Vint.Core.ECS.Components.Group;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Effect;

[ProtocolId(1543482696222L)]
public class ExplosiveMassEffectTemplate : EffectBaseTemplate {
    public IEntity Create(
        BattlePlayer battlePlayer,
        TimeSpan duration,
        bool canTargetTeammates,
        float radiusOfMaxSplashDamage,
        float radiusOfMinSplashDamage) {
        IEntity entity = Create("battle/effect/explosivemass", battlePlayer, duration, true);

        entity.AddComponent<DiscreteWeaponComponent>();

        entity.AddComponent(new SplashEffectComponent(canTargetTeammates));
        entity.AddComponent(new SplashWeaponComponent(0, radiusOfMaxSplashDamage, radiusOfMinSplashDamage));
        entity.AddComponent(new DamageWeakeningByDistanceComponent(0, radiusOfMaxSplashDamage, radiusOfMinSplashDamage));

        entity.AddGroupComponent<BattleGroupComponent>(battlePlayer.Battle.Entity);
        return entity;
    }
}
