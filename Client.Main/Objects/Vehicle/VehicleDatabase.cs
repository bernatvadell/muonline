
namespace Client.Main.Objects.Vehicle;

public static class VehicleDatabase
{
    public static Dictionary<int, VehicleDefinition> VehicleList => new()
    {
        {
            0, new VehicleDefinition
            {
                Id = 0,
                Name = "Dark Horse",
                TexturePath = "DarkHorse.bmd",
            }
        },
        {
            1, new VehicleDefinition
            {
                Id = 1,
                Name = "Divine Horse",
                TexturePath = "Divine_Horse.bmd",
            }
        },
        {
            2, new VehicleDefinition
            {
                Id = 2,
                Name = "Giant Dark Wizard 01",
                TexturePath = "Giant_DarkWizard_01.bmd",
            }
        },
        {
            3, new VehicleDefinition
            {
                Id = 3,
                Name = "Giant Elf 01",
                TexturePath = "Giant_Elf_01.bmd",
            }
        },
        {
            4, new VehicleDefinition
            {
                Id = 4,
                Name = "Giant Grow Lancer 01",
                TexturePath = "Giant_GrowLancer_01.bmd",
            }
        },
        {
            5, new VehicleDefinition
            {
                Id = 5,
                Name = "Leviathan",
                TexturePath = "Leviathan.bmd",
            }
        },
        {
            6, new VehicleDefinition
            {
                Id = 6,
                Name = "Leviathan rare",
                TexturePath = "Leviathan_rare.bmd",
            }
        },
        {
            7, new VehicleDefinition
            {
                Id = 7,
                Name = "Rider 01",
                TexturePath = "Rider01.bmd",
            }
        },
        {
            8, new VehicleDefinition
            {
                Id = 8,
                Name = "Rider 02",
                TexturePath = "Rider02.bmd",
            }
        },
        {
            9, new VehicleDefinition
            {
                Id = 9,
                Name = "Ur",
                TexturePath = "Ur.bmd",
            }
        },
        {
            10, new VehicleDefinition
            {
                Id = 10,
                Name = "Ur Up",
                TexturePath = "UrUp.bmd",
            }
        },
        {
            11, new VehicleDefinition
            {
                Id = 11,
                Name = "Fenril Black",
                TexturePath = "fenril_black.bmd",
            }
        },
        {
            12, new VehicleDefinition
            {
                Id = 12,
                Name = "Fenril Blue",
                TexturePath = "fenril_blue.bmd",
            }
        },
        {
            13, new VehicleDefinition
            {
                Id = 13,
                Name = "Fenril Gold",
                TexturePath = "fenril_gold.bmd",
            }
        },
        {
            14, new VehicleDefinition
            {
                Id = 14,
                Name = "Fenril Red",
                TexturePath = "fenril_red.bmd",
            }
        },
        {
            15, new VehicleDefinition
            {
                Id = 15,
                Name = "Fenrir Black",
                TexturePath = "fenrir_black.bmd",
            }
        },
        {
            16, new VehicleDefinition
            {
                Id = 16,
                Name = "Fenrir Blue",
                TexturePath = "fenrir_blue.bmd",
            }
        },
        {
            17, new VehicleDefinition
            {
                Id = 17,
                Name = "Fenrir Gold",
                TexturePath = "fenrir_gold.bmd",
            }
        },
        {
            18, new VehicleDefinition
            {
                Id = 18,
                Name = "Fenrir Red",
                TexturePath = "fenrir_red.bmd",
            }
        },
        {
            19, new VehicleDefinition
            {
                Id = 19,
                Name = "Fiercelion",
                TexturePath = "fiercelion.bmd",
            }
        },
        {
            20, new VehicleDefinition
            {
                Id = 20,
                Name = "Fiercelion Rare",
                TexturePath = "fiercelionR.bmd",
            }
        },
        {
            21, new VehicleDefinition
            {
                Id = 21,
                Name = "Ghost Horse",
                TexturePath = "ghost_horse.bmd",
            }
        },
        {
            22, new VehicleDefinition
            {
                Id = 22,
                Name = "Griffs Up Ride",
                TexturePath = "griffsUp_ride.bmd",
            }
        },
        {
            23, new VehicleDefinition
            {
                Id = 23,
                Name = "Griffs Ride",
                TexturePath = "griffs_ride.bmd",
            }
        },
        {
            24, new VehicleDefinition
            {
                Id = 24,
                Name = "Ice Dragon",
                TexturePath = "icedragon.bmd",
            }
        },
        {
            25, new VehicleDefinition
            {
                Id = 25,
                Name = "Ice Dragon Rare",
                TexturePath = "icedragon_rare.bmd",
            }
        },
        {
            26, new VehicleDefinition
            {
                Id = 26,
                Name = "Magma Horse",
                TexturePath = "magma_horse.bmd",
            }
        },
        {
            27, new VehicleDefinition
            {
                Id = 27,
                Name = "Pon Up Ride",
                TexturePath = "ponUp_ride.bmd",
            }
        },
        {
            28, new VehicleDefinition
            {
                Id = 28,
                Name = "Pon Ride",
                TexturePath = "pon_ride.bmd",
            }
        },
        {
            29, new VehicleDefinition
            {
                Id = 29,
                Name = "Rare Shining Tails",
                TexturePath = "rare_shiningtails.bmd",
            }
        },
        {
            30, new VehicleDefinition
            {
                Id = 30,
                Name = "Rippen Up Ride",
                TexturePath = "rippenUp_ride.bmd",
            }
        },
        {
            31, new VehicleDefinition
            {
                Id = 31,
                Name = "Rippen Ride",
                TexturePath = "rippen_ride.bmd",
            }
        },
        {
            32, new VehicleDefinition
            {
                Id = 32,
                Name = "Shining Tails",
                TexturePath = "shiningtails.bmd",
            }
        },
        {
            33, new VehicleDefinition
            {
                Id = 33,
                Name = "Wolfpet Vehicle",
                TexturePath = "wolfpetVehicle.bmd",
            }
        },
        {
            34, new VehicleDefinition
            {
                Id = 34,
                Name = "Wolfpet Vehicle Evol",
                TexturePath = "wolfpetVehicle_evol.bmd",
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