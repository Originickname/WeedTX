using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Weapon.Types;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Weapon;

[ProtocolId(-2434344547754767853)]
public class SmokyBattleItemTemplate : DiscreteWeaponTemplate {
    public IEntity Create(IEntity tank, BattlePlayer battlePlayer, IEntity shell) {
        IEntity entity = base.Create($"garage/shell/smoky/{shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}", tank, battlePlayer);
        entity.AddComponent<SmokyComponent>();
        return entity;
    }
}
