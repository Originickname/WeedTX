using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Weapon.Types.Railgun;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Weapon;

[ProtocolId(-6419489500262573655)]
public class RailgunBattleItemTemplate : DiscreteWeaponTemplate {
    public IEntity Create(IEntity tank, BattlePlayer battlePlayer, IEntity shell) {
        string configPath = $"garage/shell/railgun/{shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}";
        IEntity entity = Create(configPath, tank, battlePlayer);

        entity.AddComponent(new RailgunChargingWeaponComponent(1f));
        entity.AddComponent<DamageWeakeningByTargetComponent>(configPath);
        entity.AddComponent<RailgunComponent>();

        return entity;
    }
}
