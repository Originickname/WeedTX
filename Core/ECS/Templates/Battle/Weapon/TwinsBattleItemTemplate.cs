using Vint.Core.Battles.Player;
using Vint.Core.ECS.Components.Battle.Weapon.Types;
using Vint.Core.ECS.Entities;
using Vint.Core.Protocol.Attributes;

namespace Vint.Core.ECS.Templates.Battle.Weapon;

[ProtocolId(583528765588657091)]
public class TwinsBattleItemTemplate : BulletWeaponTemplate {
    public IEntity Create(IEntity tank, BattlePlayer battlePlayer, IEntity shell) {
        IEntity entity = base.Create($"garage/shell/twins/{shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}", tank, battlePlayer);

        entity.AddComponent<TwinsComponent>();
        return entity;
    }
}
