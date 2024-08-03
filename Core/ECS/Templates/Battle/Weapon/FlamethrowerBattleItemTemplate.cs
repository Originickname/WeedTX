using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Weapon.Types;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Weapon;

[ProtocolId(4652768934679402653)]
public class FlamethrowerBattleItemTemplate : StreamWeaponTemplate {
    public IEntity Create(IEntity tank, BattlePlayer battlePlayer, IEntity shell) {
        IEntity entity = Create($"garage/shell/flamethrower/{shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}", tank, battlePlayer);

        entity.AddComponent<FlamethrowerComponent>();
        return entity;
    }
}
