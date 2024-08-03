using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Weapon.Types;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Weapon;

[ProtocolId(525358843506658817)]
public class FreezeBattleItemTemplate : StreamWeaponTemplate {
    public IEntity Create(IEntity tank, BattlePlayer battlePlayer, IEntity shell) {
        IEntity entity = Create($"garage/shell/freeze/{shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}", tank, battlePlayer);

        entity.AddComponent<FreezeComponent>();
        return entity;
    }
}
