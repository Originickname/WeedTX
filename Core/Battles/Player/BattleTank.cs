using System.Diagnostics;
using System.Numerics;
using ConcurrentCollections;
using LinqToDB;
using Vint.Core.Battles.Damage;
using Vint.Core.Battles.Effects;
using Vint.Core.Battles.Mode;
using Vint.Core.Battles.Modules;
using Vint.Core.Battles.Modules.Interfaces;
using Vint.Core.Battles.Modules.Types.Base;
using Vint.Core.Battles.Results;
using Vint.Core.Battles.Type;
using Vint.Core.Battles.Weapons;
using Vint.Core.Config;
using Vint.Core.Config.MapInformation;
using Vint.Core.Database;
using Vint.Core.Database.Models;
using Vint.Core.ECS.Components.Battle;
using Vint.Core.ECS.Components.Battle.Incarnation;
using Vint.Core.ECS.Components.Battle.Movement;
using Vint.Core.ECS.Components.Battle.Parameters.Chassis;
using Vint.Core.ECS.Components.Battle.Round;
using Vint.Core.ECS.Components.Battle.Tank;
using Vint.Core.ECS.Components.Battle.Weapon;
using Vint.Core.ECS.Components.Modules.Inventory;
using Vint.Core.ECS.Components.Server;
using Vint.Core.ECS.Entities;
using Vint.Core.ECS.Events.Battle;
using Vint.Core.ECS.Events.Battle.Damage;
using Vint.Core.ECS.Events.Battle.Module;
using Vint.Core.ECS.Events.Battle.Movement;
using Vint.Core.ECS.Events.Battle.Score;
using Vint.Core.ECS.Events.Battle.Score.Visual;
using Vint.Core.ECS.Movement;
using Vint.Core.ECS.Templates.Battle;
using Vint.Core.ECS.Templates.Battle.Graffiti;
using Vint.Core.ECS.Templates.Battle.Incarnation;
using Vint.Core.ECS.Templates.Battle.Tank;
using Vint.Core.ECS.Templates.Battle.User;
using Vint.Core.ECS.Templates.Battle.Weapon;
using Vint.Core.ECS.Templates.Weapons.Market;
using Vint.Core.Server;
using Vint.Core.Utils;
using HealthComponent = Vint.Core.ECS.Components.Battle.Parameters.Health.HealthComponent;

namespace Vint.Core.Battles.Player;

public class BattleTank {
    public BattleTank(BattlePlayer battlePlayer) {
        BattlePlayer = battlePlayer;
        Battle = battlePlayer.Battle;
        StateManager = new TankStateManager(this);

        IPlayerConnection playerConnection = battlePlayer.PlayerConnection;
        Preset preset = playerConnection.Player.CurrentPreset;

        IEntity weapon = preset.Weapon;
        IEntity weaponSkin = preset.WeaponSkin;
        IEntity shell = preset.Shell;

        IEntity hull = preset.Hull;
        IEntity hullSkin = preset.HullSkin;

        IEntity cover = preset.Cover;
        IEntity paint = preset.Paint;
        IEntity graffiti = preset.Graffiti;

        OriginalSpeedComponent = ConfigManager.GetComponent<SpeedComponent>(hull.TemplateAccessor!.ConfigPath!);
        Weight = ConfigManager.GetComponent<WeightComponent>(hull.TemplateAccessor!.ConfigPath!);
        BattleUser = battlePlayer.BattleUser = new BattleUserTemplate().CreateAsTank(playerConnection.User, Battle.Entity, battlePlayer.Team);

        Tank = new TankTemplate().Create(hull, BattlePlayer.BattleUser);
        Shell = new ShellBattleItemTemplate().Create(shell, Tank);
        Weapon = weapon.TemplateAccessor!.Template switch {
            SmokyMarketItemTemplate => new SmokyBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            TwinsMarketItemTemplate => new TwinsBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            ThunderMarketItemTemplate => new ThunderBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            RailgunMarketItemTemplate => new RailgunBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            RicochetMarketItemTemplate => new RicochetBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            IsisMarketItemTemplate => new IsisBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            VulcanMarketItemTemplate => new VulcanBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            FreezeMarketItemTemplate => new FreezeBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            FlamethrowerMarketItemTemplate => new FlamethrowerBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            ShaftMarketItemTemplate => new ShaftBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            HammerMarketItemTemplate => new HammerBattleItemTemplate().Create(Tank, BattlePlayer, Shell),
            _ => throw new UnreachableException()
        };

        HullSkin = new HullSkinBattleItemTemplate().Create(hullSkin, Tank);
        WeaponSkin = new WeaponSkinBattleItemTemplate().Create(weaponSkin, Tank);
        Cover = new WeaponPaintBattleItemTemplate().Create(cover, Tank);
        Paint = new TankPaintBattleItemTemplate().Create(paint, Tank);
        Graffiti = new GraffitiBattleItemTemplate().Create(graffiti, Tank);

        RoundUser = new RoundUserTemplate().Create(BattlePlayer, Tank);
        Incarnation = new TankIncarnationTemplate().Create(this);

        Modules = [];

        WeaponHandler = Weapon.TemplateAccessor!.Template switch {
            SmokyBattleItemTemplate => new SmokyWeaponHandler(this),
            TwinsBattleItemTemplate => new TwinsWeaponHandler(this),
            ThunderBattleItemTemplate => new ThunderWeaponHandler(this),
            RailgunBattleItemTemplate => new RailgunWeaponHandler(this),
            RicochetBattleItemTemplate => new RicochetWeaponHandler(this),
            IsisBattleItemTemplate => new IsisWeaponHandler(this),
            VulcanBattleItemTemplate => new VulcanWeaponHandler(this),
            FreezeBattleItemTemplate => new FreezeWeaponHandler(this),
            FlamethrowerBattleItemTemplate => new FlamethrowerWeaponHandler(this),
            ShaftBattleItemTemplate => new ShaftWeaponHandler(this),
            HammerBattleItemTemplate => new HammerWeaponHandler(this),
            _ => throw new UnreachableException()
        };

        Health = TotalHealth = MaxHealth = ConfigManager.GetComponent<HealthComponent>(hull.TemplateAccessor.ConfigPath!).MaxHealth;
        TemperatureConfig = ConfigManager.GetComponent<TemperatureConfigComponent>(Tank.TemplateAccessor!.ConfigPath!);

        if (!Battle.Properties.DisabledModules) {
            foreach (PresetModule presetModule in preset.Modules) {
                BattleModule module = ModuleRegistry.Get(presetModule.Entity.Id);
                module.Init(this, presetModule.GetSlotEntity(playerConnection), presetModule.Entity).GetAwaiter().GetResult();
                Modules.Add(module);
            }
        }

        Statistics = new BattleTankStatistics();
        BattleEnterTime = DateTimeOffset.UtcNow;
    }

    public static IReadOnlyDictionary<int, int> KillStreakToScore { get; } = new Dictionary<int, int> {
        { 2, 0 }, { 3, 5 }, { 4, 7 }, { 5, 10 }, { 10, 10 }, { 15, 10 }, { 20, 20 }, { 25, 30 }, { 30, 40 }, { 35, 50 }, { 40, 60 }
    };

    public long CollisionsPhase { get; set; } = -1;

    public float SupplyDurationMultiplier { get; set; } = 1;
    public float ModuleCooldownCoeff { get; set; } = 1;

    public ConcurrentHashSet<Effect> Effects { get; } = [];

    public DateTimeOffset BattleEnterTime { get; }
    public BattleTankStatistics Statistics { get; }
    public UserResult Result { get; private set; } = null!;
    public Dictionary<BattleTank, float> KillAssistants { get; } = new();
    public ConcurrentHashSet<TemperatureAssist> TemperatureAssists { get; } = [];
    public float DealtDamage { get; set; }
    public float TakenDamage { get; set; }

    public float Health { get; private set; }
    public float TotalHealth { get; set; }
    public float MaxHealth { get; }
    public WeightComponent Weight { get; }

    public TemperatureConfigComponent TemperatureConfig { get; set; }
    public float Temperature { get; private set; }

    public Vector3 PreviousPosition { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Orientation { get; set; }

    public DateTimeOffset? SelfDestructTime { get; set; }
    public bool ForceSelfDestruct { get; set; }
    public bool FullDisabled { get; private set; }

    public SpawnPoint PreviousSpawnPoint { get; private set; }
    public SpawnPoint SpawnPoint { get; private set; }

    public List<BattleModule> Modules { get; }
    public TankWeaponHandler WeaponHandler { get; }
    public BattlePlayer BattlePlayer { get; }
    public Battle Battle { get; }
    public TankStateManager StateManager { get; set; }

    public IEnumerable<IEntity> Entities => [Incarnation, RoundUser, BattleUser, Tank, Weapon, HullSkin, WeaponSkin, Cover, Paint, Graffiti, Shell];

    public IEntity Incarnation { get; private set; }
    public IEntity RoundUser { get; }
    public IEntity BattleUser { get; }

    public IEntity Tank { get; }
    public IEntity Weapon { get; }

    public IEntity HullSkin { get; }
    public IEntity WeaponSkin { get; }

    public IEntity Cover { get; }
    public IEntity Paint { get; }

    public IEntity Graffiti { get; }
    public IEntity Shell { get; }

    public SpeedComponent OriginalSpeedComponent { get; }

    public async Task Tick() {
        if (BattlePlayer.IsPaused &&
            (!BattlePlayer.KickTime.HasValue ||
             DateTimeOffset.UtcNow > BattlePlayer.KickTime)) {
            BattlePlayer.IsPaused = false;
            BattlePlayer.KickTime = null;
            await BattlePlayer.PlayerConnection.Send(new KickFromBattleEvent(), BattleUser);
            await Battle.RemovePlayer(BattlePlayer);
        }

        if (ForceSelfDestruct || SelfDestructTime.HasValue && SelfDestructTime.Value <= DateTimeOffset.UtcNow)
            await SelfDestruct();

        if (CollisionsPhase == Battle.Entity.GetComponent<BattleTankCollisionsComponent>().SemiActiveCollisionsPhase) {
            await Tank.RemoveComponentIfPresent<TankStateTimeOutComponent>();
            await Battle.Entity.ChangeComponent<BattleTankCollisionsComponent>(component =>
                component.SemiActiveCollisionsPhase++);

            await StateManager.SetState(new Active(StateManager));
            await SetHealth(MaxHealth);
        }

        await StateManager.Tick();
        await WeaponHandler.Tick();
        await HandleTemperature();

        foreach (Effect effect in Effects)
            await effect.Tick();

        foreach (BattleModule module in Modules)
            await module.Tick();
    }

    public async Task Enable() {
        if (FullDisabled) return;

        await WeaponHandler.OnTankEnable();
        await Tank.AddComponent<TankMovableComponent>();
    }

    public async Task Disable(bool full) {
        FullDisabled = full;

        foreach (Effect effect in Effects) {
            if (full) effect.CanBeDeactivated = true;
            else if (!effect.CanBeDeactivated) continue;

            effect.UnScheduleAll();
            await effect.Deactivate();
        }

        foreach (IModuleWithoutEffect moduleWithoutEffect in Modules.OfType<IModuleWithoutEffect>()) {
            if (full) moduleWithoutEffect.CanBeDeactivated = true;
            else if (!moduleWithoutEffect.CanBeDeactivated) continue;

            await moduleWithoutEffect.Deactivate();
        }

        TotalHealth = MaxHealth;
        TemperatureAssists.Clear();
        await SetTemperature(0);
        //await Tank.ChangeComponent(OriginalSpeedComponent.Clone());
        await BattlePlayer.PlayerConnection.Send(new ResetTankSpeedEvent(), Tank);

        if (Tank.HasComponent<SelfDestructionComponent>()) {
            await Tank.RemoveComponent<SelfDestructionComponent>();
            SelfDestructTime = null;
            ForceSelfDestruct = false;
        }

        if (full) {
            await Tank.RemoveComponentIfPresent(StateManager.CurrentState.StateComponent);

            foreach (BattleModule module in Modules)
                await module.SetAmmo(module.MaxAmmo);
        }

        await Tank.RemoveComponentIfPresent<TankMovableComponent>();
        await WeaponHandler.OnTankDisable();
    }

    public async Task UpdateModuleCooldownSpeed(float coeff, bool reset = false) {
        if (reset) coeff = 1;

        ModuleCooldownCoeff = coeff;
        await BattleUser.ChangeComponent<BattleUserInventoryCooldownSpeedComponent>(component => component.SpeedCoeff = coeff);
        await BattlePlayer.PlayerConnection.Send(new BattleUserInventoryCooldownSpeedChangedEvent(), BattleUser);
    }

    public async Task Spawn() {
        await Tank.RemoveComponentIfPresent<TankVisibleStateComponent>();

        if (Tank.HasComponent<TankMovementComponent>()) {
            await Tank.RemoveComponent<TankMovementComponent>();

            IEntity incarnation = Incarnation;
            Incarnation = new TankIncarnationTemplate().Create(this);

            foreach (IPlayerConnection playerConnection in incarnation.SharedPlayers) {
                await playerConnection.Unshare(incarnation);
                await playerConnection.Share(Incarnation);
            }
        }

        PreviousSpawnPoint = SpawnPoint;
        SpawnPoint = Battle.ModeHandler.GetRandomSpawnPoint(BattlePlayer);

        Movement movement = new() {
            Position = SpawnPoint.Position,
            Orientation = SpawnPoint.Rotation
        };

        await Tank.AddComponent(new TankMovementComponent(movement, default, 0, 0));
    }

    public async Task SetHealth(float health) {
        float before = Health;
        Health = Math.Clamp(health, 0, MaxHealth);
        await Tank.ChangeComponent<HealthComponent>(component => component.CurrentHealth = MathF.Ceiling(Health));

        /*HealthComponent healthComponent = Tank.GetComponent<HealthComponent>();
        healthComponent.CurrentHealth = MathF.Ceiling(Health);

        await Tank.RemoveComponent<HealthComponent>();
        await Tank.AddComponent(healthComponent);*/

        foreach (BattlePlayer battlePlayer in Battle.Players.Where(player => player.InBattle))
            await battlePlayer.PlayerConnection.Send(new HealthChangedEvent(), Tank);

        foreach (IHealthModule healthModule in Modules.OfType<IHealthModule>())
            await healthModule.OnHealthChanged(before, Health, MaxHealth);
    }

    public async Task SetTemperature(float temperature) {
        float before = Temperature;
        float min = TemperatureConfig.MinTemperature;
        float max = TemperatureConfig.MaxTemperature;

        Temperature = Math.Clamp(temperature, min, max);
        await Tank.ChangeComponent<TemperatureComponent>(component => component.Temperature = Temperature);

        await UpdateSpeed();

        foreach (ITemperatureModule temperatureModule in Modules.OfType<ITemperatureModule>())
            await temperatureModule.OnTemperatureChanged(before, Temperature, min, max);
    }

    public async Task HandleTemperature() {
        if (StateManager.CurrentState is Dead) return;

        TimeSpan period = TimeSpan.FromMilliseconds(TemperatureConfig.TactPeriodInMs);

        if (TemperatureAssists.Count > 0) {
            float newTemperature = TemperatureAssists.Sum(ass => ass.CurrentTemperature);
            await SetTemperature(newTemperature);
        }

        foreach (TemperatureAssist assist in TemperatureAssists) {
            if (StateManager.CurrentState is Dead) break;

            if (DateTimeOffset.UtcNow - assist.LastTick < period) continue;

            float temperatureDelta = assist.CurrentTemperature switch {
                > 0 => -TemperatureConfig.AutoDecrementInMs,
                < 0 => TemperatureConfig.AutoIncrementInMs,
                _ => 0
            } * TemperatureConfig.TactPeriodInMs;

            if (assist is { CurrentTemperature: > 0, Weapon: not IsisWeaponHandler } && (assist.Assistant == this || IsEnemy(assist.Assistant))) {
                float value = MathF.Round(MathUtils.Map(assist.CurrentTemperature, 0, assist.Weapon.TemperatureLimit, 0, assist.MaxDamage));

                CalculatedDamage damage = new(default, value, false, false);
                await Battle.DamageProcessor.Damage(assist.Assistant, this, assist.Weapon.MarketEntity, assist.Weapon.BattleEntity, damage);
            }

            bool wasPositive = assist.CurrentTemperature > 0;
            assist.CurrentTemperature += temperatureDelta;
            bool isPositive = assist.CurrentTemperature > 0;

            if (wasPositive != isPositive) {
                TemperatureAssists.TryRemove(assist);

                if (TemperatureAssists.Count == 0)
                    await SetTemperature(0);
            }

            assist.LastTick = DateTimeOffset.UtcNow;
        }
    }

    public async Task UpdateTemperatureAssists(BattleTank assistant, ITemperatureWeaponHandler weaponHandler, bool normalizeOnly) {
        float maxHeatDamage = (weaponHandler as IHeatWeaponHandler)?.HeatDamage ?? 0;
        float temperatureDelta = weaponHandler switch {
            IsisWeaponHandler isis => Temperature switch {
                < 0 => isis.IncreaseFriendTemperature,
                > 0 => -isis.DecreaseFriendTemperature,
                _ => 0
            },
            _ => weaponHandler.TemperatureDelta
        };

        temperatureDelta =
            Math.Clamp(Temperature + temperatureDelta, TemperatureConfig.MinTemperature, TemperatureConfig.MaxTemperature) - Temperature;

        if (temperatureDelta == 0) return;

        bool deltaIsPositive = temperatureDelta >= 0;

        if (Temperature - temperatureDelta >= 0 != deltaIsPositive) {
            foreach (TemperatureAssist assist in TemperatureAssists) {
                bool assistTemperatureIsPositive = assist.CurrentTemperature > 0;

                if (assistTemperatureIsPositive == deltaIsPositive) continue;

                if (assist.CurrentTemperature + temperatureDelta >= 0 != assistTemperatureIsPositive) {
                    TemperatureAssists.TryRemove(assist);

                    if (deltaIsPositive) temperatureDelta -= assist.CurrentTemperature;
                    else temperatureDelta += assist.CurrentTemperature;

                    deltaIsPositive = temperatureDelta > 0;
                    continue;
                }

                assist.CurrentTemperature += temperatureDelta;
            }

            if (TemperatureAssists.Count == 0)
                await SetTemperature(0);
        }

        if (temperatureDelta == 0 || normalizeOnly) return;

        TemperatureAssist? sourceAssist = TemperatureAssists
            .SingleOrDefault(assist => assist.Assistant == assistant &&
                                       assist.Weapon == weaponHandler);

        if (sourceAssist == null) {
            sourceAssist = new TemperatureAssist(assistant, weaponHandler, maxHeatDamage, temperatureDelta, DateTimeOffset.UtcNow);
            TemperatureAssists.Add(sourceAssist);
        } else {
            float limit = sourceAssist.Weapon.TemperatureLimit;
            float newTemperature = sourceAssist.CurrentTemperature + temperatureDelta;

            sourceAssist.CurrentTemperature = limit switch {
                < 0 => Math.Clamp(newTemperature, limit, 0),
                > 0 => Math.Clamp(newTemperature, 0, limit),
                _ => 0
            };
        }
    }

    public async Task UpdateSpeed() {
        if (Temperature < 0) {
            float minTemperature = TemperatureConfig.MinTemperature;

            float minSpeed = TankUtils.CalculateFrozenSpeed(OriginalSpeedComponent.Speed, 12.5f);
            float minTurnSpeed = TankUtils.CalculateFrozenSpeed(OriginalSpeedComponent.TurnSpeed, 2.5f);
            float minWeaponSpeed = TankUtils.CalculateFrozenSpeed(WeaponHandler.OriginalWeaponRotationComponent.Speed, 7.5f);

            float newSpeed = MathUtils.Map(Temperature, 0, minTemperature, OriginalSpeedComponent.Speed, minSpeed);
            float newTurnSpeed = MathUtils.Map(Temperature, 0, minTemperature, OriginalSpeedComponent.TurnSpeed, minTurnSpeed);
            float newWeaponSpeed = MathUtils.Map(Temperature, 0, minTemperature, WeaponHandler.OriginalWeaponRotationComponent.Speed, minWeaponSpeed);

            await Tank.ChangeComponent<SpeedComponent>(component => {
                component.Speed = newSpeed;
                component.TurnSpeed = newTurnSpeed;
            });
            await Weapon.ChangeComponent<WeaponRotationComponent>(component => component.Speed = newWeaponSpeed);
        } else {
            await Tank.ChangeComponent(OriginalSpeedComponent.Clone());
            await Weapon.ChangeComponent(WeaponHandler.OriginalWeaponRotationComponent.Clone());
        }
    }

    public bool IsEnemy(BattleTank other) => other != null! &&
                                             this != other &&
                                             (Battle.Properties.FriendlyFire ||
                                              Battle.Properties.BattleMode == BattleMode.DM ||
                                              !IsSameTeam(other));

    public bool IsSameTeam(BattleTank other) => other != null! &&
                                                BattlePlayer.TeamColor == other.BattlePlayer.TeamColor;

    public async Task KillBy(BattleTank killer, IEntity weapon) {
        const int baseScore = 10;

        float coeff = TotalHealth / MaxHealth;
        Dictionary<BattleTank, float> assistants = KillAssistants.Where(assist => assist.Key != this).ToDictionary();
        await SelfKill();

        Database.Models.Player currentPlayer = BattlePlayer.PlayerConnection.Player;
        KillEvent killEvent = new(weapon, Tank);

        foreach (IPlayerConnection connection in Battle.Players
                     .Where(battlePlayer => battlePlayer.InBattle)
                     .Select(battlePlayer => battlePlayer.PlayerConnection)) {
            await connection.Send(killEvent, killer.BattleUser);
        }

        await killer.AddKills(1);
        await AddDeaths(1, killer);

        foreach ((BattleTank assistant, float damageDealt) in assistants) {
            float damage = Math.Min(damageDealt, TotalHealth);
            int score = Convert.ToInt32(Math.Round(MathUtils.Map(damage, 0, TotalHealth, 1, baseScore * coeff)));

            if (assistant == killer) {
                score += 5;

                await killer.AddScore(score);
                await killer.BattlePlayer.PlayerConnection.Send(
                    new VisualScoreKillEvent(BattlePlayer.GetScoreWithBonus(score), currentPlayer.Username, currentPlayer.Rank),
                    killer.BattleUser);
            } else {
                int percent = Convert.ToInt32(Math.Round(damage / TotalHealth * 100));

                await assistant.AddAssists(1);
                await assistant.AddScore(score);
                await assistant.CommitStatistics();

                await assistant.BattlePlayer.PlayerConnection.Send(
                    new VisualScoreAssistEvent(BattlePlayer.GetScoreWithBonus(score), percent, currentPlayer.Username),
                    assistant.BattleUser);
            }
        }

        await killer.CommitStatistics();
        await CommitStatistics();

        foreach (IKillModule killModule in killer.Modules.OfType<IKillModule>())
            await killModule.OnKill(this);

        switch (Battle.ModeHandler) {
            case TDMHandler tdm:
                await tdm.UpdateScore(killer.BattlePlayer.Team, 1);
                break;

            case SoloHandler dm:
                await dm.UpdateScore(null, 0);
                break;
        }

        if (Battle.TypeHandler is not MatchmakingHandler) return;

        Database.Models.Player player = killer.BattlePlayer.PlayerConnection.Player;

        await using DbConnection db = new();
        await db.BeginTransactionAsync();

        await db.Hulls
            .Where(hull => hull.PlayerId == player.Id &&
                           hull.Id == player.CurrentPreset.Hull.Id)
            .Set(hull => hull.Kills, hull => hull.Kills + 1)
            .UpdateAsync();

        await db.Weapons
            .Where(w => w.PlayerId == player.Id &&
                        w.Id == player.CurrentPreset.Weapon.Id)
            .Set(w => w.Kills, w => w.Kills + 1)
            .UpdateAsync();

        await db.Statistics
            .Where(stats => stats.PlayerId == player.Id)
            .Set(stats => stats.Kills, stats => stats.Kills + 1)
            .UpdateAsync();

        await db.SeasonStatistics
            .Where(stats => stats.PlayerId == player.Id)
            .Where(stats => stats.SeasonNumber == ConfigManager.ServerConfig.SeasonNumber)
            .Set(stats => stats.Kills, stats => stats.Kills + 1)
            .UpdateAsync();

        await db.CommitTransactionAsync();
    }

    public async Task SelfDestruct() {
        await SelfKill();
        ForceSelfDestruct = false;
        SelfDestructTime = null;

        SelfDestructionBattleUserEvent selfDestructionEvent = new();

        foreach (BattlePlayer battlePlayer in Battle.Players.Where(battlePlayer => battlePlayer.InBattle))
            await battlePlayer.PlayerConnection.Send(selfDestructionEvent, BattleUser);

        await AddKills(-1);
        await AddScore(-10);
        await AddDeaths(1, null);
        await CommitStatistics();

        if (Battle.ModeHandler is TDMHandler tdm)
            await tdm.UpdateScore(BattlePlayer.Team, -1);
    }

    async Task SelfKill() {
        foreach (IDeathModule deathModule in Modules.OfType<IDeathModule>())
            await deathModule.OnDeath();

        await BattlePlayer.PlayerConnection.Send(new SelfTankExplosionEvent(), Tank);
        await StateManager.SetState(new Dead(StateManager));
        KillAssistants.Clear();

        if (Battle.TypeHandler is not MatchmakingHandler) return;

        await using DbConnection db = new();
        await db.Statistics
            .Where(stats => stats.PlayerId == BattlePlayer.PlayerConnection.Player.Id)
            .Set(stats => stats.Deaths, stats => stats.Deaths + 1)
            .UpdateAsync();
    }

    public async Task AddKills(int delta) {
        await RoundUser.ChangeComponent<RoundUserStatisticsComponent>(component => component.Kills = Math.Max(0, component.Kills + delta));

        if (delta > 0)
            await Incarnation.ChangeComponent<TankIncarnationKillStatisticsComponent>(component => component.Kills += delta);

        await UpdateKillStreak();
    }

    public Task AddAssists(int delta) =>
        RoundUser.ChangeComponent<RoundUserStatisticsComponent>(component => component.KillAssists = Math.Max(0, component.KillAssists + delta));

    public async Task AddDeaths(int delta, BattleTank? killer) {
        await RoundUser.ChangeComponent<RoundUserStatisticsComponent>(component => component.Deaths = Math.Max(0, component.Deaths + delta));

        await ResetKillStreak(killer);
    }

    public async Task AddScore(int deltaWithoutBonus) {
        await RoundUser.ChangeComponent<RoundUserStatisticsComponent>(component =>
            component.ScoreWithoutBonuses = Math.Max(0, component.ScoreWithoutBonuses + deltaWithoutBonus));

        if (deltaWithoutBonus <= 0 || Battle.TypeHandler is not MatchmakingHandler) return;

        int deltaWithBonus = BattlePlayer.GetScoreWithBonus(deltaWithoutBonus);
        IPlayerConnection connection = BattlePlayer.PlayerConnection;

        await connection.ChangeExperience(deltaWithBonus);
        await connection.CheckRankUp();
    }

    public async Task CommitStatistics() {
        foreach (IPlayerConnection connection in Battle.Players.Where(player => player.InBattle).Select(player => player.PlayerConnection))
            await connection.Send(new RoundUserStatisticsUpdatedEvent(), RoundUser);

        await Battle.ModeHandler.SortPlayers();
    }

    async Task UpdateKillStreak() {
        int killStreak = Incarnation.GetComponent<TankIncarnationKillStatisticsComponent>().Kills;
        int newStreak = Math.Max(Statistics.KillStrike, killStreak);

        if (Statistics.KillStrike == newStreak) return;

        Statistics.KillStrike = newStreak;

        if (killStreak < 2) return;

        int score = KillStreakToScore.GetValueOrDefault(killStreak, killStreak);

        await RoundUser.ChangeComponent<RoundUserStatisticsComponent>(component => component.ScoreWithoutBonuses += score);
        await BattlePlayer.PlayerConnection.Send(new VisualScoreStreakEvent(BattlePlayer.GetScoreWithBonus(score)), BattleUser);

        if (killStreak < 5 || killStreak % 5 == 0)
            await BattlePlayer.PlayerConnection.Send(new KillStreakEvent(score), Incarnation);
    }

    async Task ResetKillStreak(BattleTank? killer = null) {
        TankIncarnationKillStatisticsComponent incarnationStatisticsComponent =
            Incarnation.GetComponent<TankIncarnationKillStatisticsComponent>();

        if (incarnationStatisticsComponent.Kills >= 2 && killer != null)
            await killer.BattlePlayer.PlayerConnection.Send(
                new StreakTerminationEvent(BattlePlayer.PlayerConnection.Player.Username),
                killer.BattleUser);

        incarnationStatisticsComponent.Kills = 0;
        await Incarnation.ChangeComponent(incarnationStatisticsComponent);
    }

    public void CreateUserResult() =>
        Result = new UserResult(BattlePlayer);

    public override int GetHashCode() => BattlePlayer.GetHashCode();
}
