using System;
using Vint.Core.Battles.Player;
using Vint.Core.Config;
using Vint.Core.ECS.Components.Battle.Weapon;
using Vint.Core.ECS.Components.Battle.Weapon.Types.Shaft;
using Vint.Core.ECS.Components.Server;
using Vint.Core.Server;
using Vint.Core.Utils;

namespace Vint.Core.Battles.Weapons;

public class ShaftWeaponHandler : DiscreteTankWeaponHandler, IHeatWeaponHandler {
    public ShaftWeaponHandler(BattleTank battleTank) : base(battleTank) {
        EnergyDrainPerMs = ConfigManager.GetComponent<EnergyChargeSpeedPropertyComponent>(MarketConfigPath).FinalValue / 1000;
        AimingSpeedComponent = BattleEntity.GetComponent<ShaftAimingSpeedComponent>();
        HeatDamage = ConfigManager.GetComponent<HeatDamagePropertyComponent>(MarketConfigPath).FinalValue;
        TemperatureLimit = ConfigManager.GetComponent<TemperatureLimitPropertyComponent>(MarketConfigPath).FinalValue;

        OverheatingTime = TimeSpan.FromSeconds(ConfigManager.GetComponent<TemperatureHittingTimePropertyComponent>(MarketConfigPath)
            .FinalValue);
        TemperatureDelta =
            ConfigManager.GetComponent<DeltaTemperaturePerSecondPropertyComponent>(MarketConfigPath).FinalValue * (float)Cooldown.TotalSeconds;
    }


    public float TemperatureLimit { get; }
    public float HeatDamage { get; }
    public float TemperatureDelta { get; }
    TimeSpan OverheatingTime { get; }
    DateTimeOffset? AimingBeginTime { get; set; }
    public ShaftAimingSpeedComponent AimingSpeedComponent { get; }
    public bool Aiming { get; private set; }
    public TimeSpan AimingDuration { get; private set; }
    public float EnergyDrainPerMs { get; private set; }

    bool IsOverheating => AimingBeginTime.HasValue &&
                          DateTimeOffset.UtcNow - AimingBeginTime >= OverheatingTime;
    public DateTimeOffset? LastOverheatingUpdate { get; set; }
    public override int MaxHitTargets => 1;

    public void Aim() {
        Aiming = true;
        AimingBeginTime = DateTimeOffset.UtcNow;
        BattleEntity.ChangeComponent<WeaponRotationComponent>(component => { // vertical speed controlled by client, but horizontal is not
            component.Speed = AimingSpeedComponent.MaxHorizontalSpeed;
            component.Acceleration = AimingSpeedComponent.HorizontalAcceleration;
        });
    }

    public override async Task Tick() {
        await base.Tick();
        await UpdateOverheating();
    }

    async Task UpdateOverheating() {
        if (!IsOverheating || LastOverheatingUpdate.HasValue &&
            DateTimeOffset.UtcNow - LastOverheatingUpdate < Cooldown) return;

        if (BattleTank.StateManager.CurrentState is Dead) {
            AimingBeginTime = null;
            LastOverheatingUpdate = null;
            return;
        }

        await BattleTank.UpdateTemperatureAssists(BattleTank, this, false);
        LastOverheatingUpdate = DateTimeOffset.UtcNow;
    }
    public void Idle() {
        double durationMs =
            Math.Clamp((DateTimeOffset.UtcNow - (AimingBeginTime ?? DateTimeOffset.UtcNow)).TotalMilliseconds, 0, 1 / EnergyDrainPerMs);
        AimingDuration = TimeSpan.FromMilliseconds(durationMs);
        BattleEntity.ChangeComponent(OriginalWeaponRotationComponent.Clone());
    }

    public void Reset() {
        Aiming = false;
        AimingBeginTime = null;
        AimingDuration = TimeSpan.Zero;
    }
}
