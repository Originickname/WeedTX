using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Weapon.Types.Hammer;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Weapon;

[ProtocolId(4939169559170921259)]
public class HammerBattleItemTemplate : DiscreteWeaponTemplate {
    public IEntity Create(IEntity tank, BattlePlayer battlePlayer, IEntity shell) {
        string configPath = $"garage/shell/hammer/{shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}";
        IEntity entity = Create(configPath, tank, battlePlayer);

        entity.AddComponent<HammerComponent>();
        entity.AddComponent<MagazineReadyStateComponent>();
        entity.AddComponent<MagazineWeaponComponent>(configPath);
        entity.AddComponent<HammerPelletConeComponent>(configPath.Replace("garage", "battle"));
        return entity;
    }
}
