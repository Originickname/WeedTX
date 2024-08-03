using System.Linq;
using Vint.Core.Battles.Effects;
using Vint.Core.Battles.Modules.Interfaces;
using Vint.Core.Battles.Modules.Types.Base;

namespace Vint.Core.Battles.Modules.Types;

public class RepairKitModule : ActiveBattleModule, IHealthModule, IFlagModule, IShotModule {
    public override string ConfigPath => "garage/module/upgrade/properties/repairkit";
    public override RepairKitEffect GetEffect() => new(Tank, Level);
    public override bool ActivationCondition => Tank.Health < Tank.MaxHealth && Effect == null;
    RepairKitEffect? Effect { get; set; }
    void Deactivated() => Effect = null;
    public override async Task Activate() {
        if (!CanBeActivated) return;

        await base.Activate();
        Effect = Tank.Effects.OfType<RepairKitEffect>().SingleOrDefault();

        switch (Effect) {
            case null:
                Effect = GetEffect();
                Effect.Deactivated += Deactivated;
                await Effect.Activate();
                break;

            case IExtendableEffect extendableEffect:
                await extendableEffect.Extend(Level);
                break;
        }
    }



    public override async Task TryUnblock() {
        if (Tank.Health >= Tank.MaxHealth) return;

        await base.TryUnblock();
    }

    public override async Task TryBlock(bool force = false, long blockTimeMs = 0) {
        if (!force && Tank.Health < Tank.MaxHealth) return;

        await base.TryBlock(force, blockTimeMs);
    }
    public async Task OnFlagAction(FlagAction action) {
        if (action == FlagAction.Capture)
            await TryDeactivate();
    }

    public Task OnShot() => TryDeactivate();
    Task TryDeactivate() =>
        Effect == null
            ? Task.CompletedTask
            : Effect.Deactivate();
    public async Task OnHealthChanged(float before, float current, float max) {
        if (current < max) await TryUnblock();
        else await TryBlock(true);
    }
}
