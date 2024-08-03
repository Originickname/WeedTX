using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Weapon;
using Vint.Core.ECS.Components.Battle.Weapon.Stream;
using Vint.Core.ECS.Components.Battle.Weapon.Types.Vulcan;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Weapon;

[ProtocolId(-3936735916503799349)]
public class VulcanBattleItemTemplate : StreamWeaponTemplate {
    public IEntity Create(IEntity tank, BattlePlayer battlePlayer, IEntity shell) {
        string configPath = $"garage/shell/vulcan/{shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}";
        IEntity entity = Create(configPath, tank, battlePlayer);

        entity.AddComponent<VulcanComponent>();
        entity.AddComponent<StreamHitConfigComponent>("battle/weapon/vulcan");
        entity.AddComponent<VulcanWeaponComponent>(configPath);
        entity.AddComponent<KickbackComponent>(configPath);
        entity.AddComponent<ImpactComponent>(configPath);
        return entity;
    }
}
