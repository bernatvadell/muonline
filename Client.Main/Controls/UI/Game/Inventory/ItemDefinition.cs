namespace Client.Main.Controls.UI.Game.Inventory
{
    public class ItemDefinition
    {
        public int Id { get; set; } // Unique ID of the item type (e.g., from Item.bmd)
        public string Name { get; set; }
        public int Width { get; set; }  // Width in slots
        public int Height { get; set; } // Height in slots
        public string TexturePath { get; set; } // Path to the item's texture/model

        public ItemDefinition(int id, string name, int width, int height, string texturePath = null)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            TexturePath = texturePath;
        }
    }
}