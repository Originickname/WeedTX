using Vint.Core.ECS.Components.Battle.Weapon.Types.Railgun;
using Vint.Core.ECS.Components.Battle.Weapon.Types.Vulcan;

namespace Vint.Core.ECS.Components.Server;

public class DamagePerPelletPropertyComponent : RangedComponent;

public class DamagePerSecondPropertyComponent : RangedComponent;

public class HealingPropertyComponent : RangedComponent;

public class SelfHealingPropertyComponent : RangedComponent;

public class AimingMaxDamagePropertyComponent : RangedComponent;

public class AimingMinDamagePropertyComponent : RangedComponent;

public class HeatDamagePropertyComponent : RangedComponent;

public class MinDamagePropertyComponent : RangedComponent;

public class BehaviorPropertyComponent : RangedComponent;

public class MaxDamagePropertyComponent : RangedComponent;

public class DecreaseFriendTemperaturePropertyComponent : RangedComponent;

public class IncreaseFriendTemperaturePropertyComponent : RangedComponent;

public class DeltaTemperaturePerSecondPropertyComponent : RangedComponent;

public class DamageWeakeningByTargetPropertyComponent : RangedComponent, IConvertible<DamageWeakeningByTargetComponent> {
    public void Convert(DamageWeakeningByTargetComponent component) =>
        component.DamagePercent = FinalValue;
}

public class TemperatureLimitPropertyComponent : RangedComponent, IConvertible<VulcanWeaponComponent> {
    public void Convert(VulcanWeaponComponent component) =>
        component.TemperatureLimit = FinalValue;
}
