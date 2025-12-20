
namespace Client.Main.Objects.Vehicle;

public static class VehicleDatabase
{
    public static readonly IReadOnlyDictionary<int, VehicleDefinition> VehicleList = new Dictionary<int, VehicleDefinition>
    {
        {
            0, new VehicleDefinition
            {
                Id = 0,
                Name = "Dark Horse",
                TexturePath = "DarkHorse.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 6,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.3f,  // default
                    [2] = 0.34f, // run
                    [6] = 0.34f, // horse attack
                },
            }
        },
        {
            1, new VehicleDefinition
            {
                Id = 1,
                Name = "Divine Horse",
                TexturePath = "Divine_Horse.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            2, new VehicleDefinition
            {
                Id = 2,
                Name = "Giant Dark Wizard 01",
                TexturePath = "Giant_DarkWizard_01.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            3, new VehicleDefinition
            {
                Id = 3,
                Name = "Giant Elf 01",
                TexturePath = "Giant_Elf_01.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            4, new VehicleDefinition
            {
                Id = 4,
                Name = "Giant Grow Lancer 01",
                TexturePath = "Giant_GrowLancer_01.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            5, new VehicleDefinition
            {
                Id = 5,
                Name = "Leviathan",
                TexturePath = "Leviathan.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            6, new VehicleDefinition
            {
                Id = 6,
                Name = "Leviathan rare",
                TexturePath = "Leviathan_rare.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            7, new VehicleDefinition
            {
                Id = 7,
                Name = "Rider 01",
                TexturePath = "Rider01.bmd",
                RiderHeightOffset = 0f, // Uniria - sits correctly
                // SourceMain5.2 (GOBoid.cpp): MODEL_UNICON SetAction uses indices 0..7, Velocity always 0.34f
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 6,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.34f,
                    [1] = 0.34f,
                    [2] = 0.34f,
                    [3] = 0.34f,
                    [4] = 0.34f,
                    [5] = 0.34f,
                    [6] = 0.34f,
                    [7] = 0.34f,
                },
            }
        },
        {
            8, new VehicleDefinition
            {
                Id = 8,
                Name = "Rider 02",
                TexturePath = "Rider02.bmd",
                RiderHeightOffset = 0f, // Dinorant - rider too low
                // SourceMain5.2 (GOBoid.cpp): MODEL_PEGASUS SetAction uses indices 0..7, Velocity always 0.34f
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 6,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.34f,
                    [1] = 0.34f,
                    [2] = 0.34f,
                    [3] = 0.34f,
                    [4] = 0.34f,
                    [5] = 0.34f,
                    [6] = 0.34f,
                    [7] = 0.34f,
                },
            }
        },
        {
            9, new VehicleDefinition
            {
                Id = 9,
                Name = "Ur",
                TexturePath = "Ur.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            10, new VehicleDefinition
            {
                Id = 10,
                Name = "Ur Up",
                TexturePath = "UrUp.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            11, new VehicleDefinition
            {
                Id = 11,
                Name = "Fenril Black",
                TexturePath = "fenril_black.bmd",
                RiderHeightOffset = 0f, // Fenrir variants - rider too low
                // SourceMain5.2 (_define.h): FENRIR_STAND=0, WALK=1, RUN=2, ATTACK=3, ATTACK_SKILL=4, DAMAGE=5
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            12, new VehicleDefinition
            {
                Id = 12,
                Name = "Fenril Blue",
                TexturePath = "fenril_blue.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            13, new VehicleDefinition
            {
                Id = 13,
                Name = "Fenril Gold",
                TexturePath = "fenril_gold.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            14, new VehicleDefinition
            {
                Id = 14,
                Name = "Fenril Red",
                TexturePath = "fenril_red.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            15, new VehicleDefinition
            {
                Id = 15,
                Name = "Fenrir Black",
                TexturePath = "fenrir_black.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            16, new VehicleDefinition
            {
                Id = 16,
                Name = "Fenrir Blue",
                TexturePath = "fenrir_blue.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            17, new VehicleDefinition
            {
                Id = 17,
                Name = "Fenrir Gold",
                TexturePath = "fenrir_gold.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            18, new VehicleDefinition
            {
                Id = 18,
                Name = "Fenrir Red",
                TexturePath = "fenrir_red.bmd",
                RiderHeightOffset = 0f,
                IdleActionIndex = 0,
                RunActionIndex = 2,
                SkillActionIndex = 4,
                ActionPlaySpeedOverrides = new Dictionary<int, float>
                {
                    [0] = 0.4f,
                    [1] = 1.0f,
                    [2] = 0.6f,
                    [3] = 0.4f,
                    [4] = 0.4f,
                    [5] = 0.4f,
                },
            }
        },
        {
            19, new VehicleDefinition
            {
                Id = 19,
                Name = "Fiercelion",
                TexturePath = "fiercelion.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            20, new VehicleDefinition
            {
                Id = 20,
                Name = "Fiercelion Rare",
                TexturePath = "fiercelionR.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            21, new VehicleDefinition
            {
                Id = 21,
                Name = "Ghost Horse",
                TexturePath = "ghost_horse.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            22, new VehicleDefinition
            {
                Id = 22,
                Name = "Griffs Up Ride",
                TexturePath = "griffsUp_ride.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            23, new VehicleDefinition
            {
                Id = 23,
                Name = "Griffs Ride",
                TexturePath = "griffs_ride.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            24, new VehicleDefinition
            {
                Id = 24,
                Name = "Ice Dragon",
                TexturePath = "icedragon.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            25, new VehicleDefinition
            {
                Id = 25,
                Name = "Ice Dragon Rare",
                TexturePath = "icedragon_rare.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            26, new VehicleDefinition
            {
                Id = 26,
                Name = "Magma Horse",
                TexturePath = "magma_horse.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            27, new VehicleDefinition
            {
                Id = 27,
                Name = "Pon Up Ride",
                TexturePath = "ponUp_ride.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            28, new VehicleDefinition
            {
                Id = 28,
                Name = "Pon Ride",
                TexturePath = "pon_ride.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            29, new VehicleDefinition
            {
                Id = 29,
                Name = "Rare Shining Tails",
                TexturePath = "rare_shiningtails.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            30, new VehicleDefinition
            {
                Id = 30,
                Name = "Rippen Up Ride",
                TexturePath = "rippenUp_ride.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            31, new VehicleDefinition
            {
                Id = 31,
                Name = "Rippen Ride",
                TexturePath = "rippen_ride.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            32, new VehicleDefinition
            {
                Id = 32,
                Name = "Shining Tails",
                TexturePath = "shiningtails.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            33, new VehicleDefinition
            {
                Id = 33,
                Name = "Wolfpet Vehicle",
                TexturePath = "wolfpetVehicle.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
        {
            34, new VehicleDefinition
            {
                Id = 34,
                Name = "Wolfpet Vehicle Evol",
                TexturePath = "wolfpetVehicle_evol.bmd",
                RiderHeightOffset = 0f,
                AnimationSpeedMultiplier = 1.0f,
            }
        },
    };

    public static VehicleDefinition GetVehicleDefinition(int index)
    {
        if (VehicleList.TryGetValue(index, out var def))
            return def;
        return null;
    }
}
