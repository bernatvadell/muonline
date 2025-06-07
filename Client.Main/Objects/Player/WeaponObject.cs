using Client.Data;
using Client.Main.Content;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class WeaponObject : ModelObject
    {
        private int _type;
        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<WeaponObject>();

        public new int Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    _ = OnChangeTypeAsync();
                }
            }
        }

        public bool IsRightHand { get; set; }

        public WeaponObject()
        {
            RenderShadow = true;
            LinkParentAnimation = true;
        }

        private async Task OnChangeTypeAsync()
        {
            ParentBoneLink = IsRightHand ? 10 : 15;

            string modelPath = GetWeaponPath(Type);
            if (string.IsNullOrEmpty(modelPath))
            {
                Model = null;
                return;
            }

            Model = await BMDLoader.Instance.Prepare(modelPath);
            if (Model == null)
            {
                _logger?.LogWarning("WeaponObject: Failed to load model for Type {Type}. Path: {Path}", Type, modelPath);
                Status = Models.GameControlStatus.Error;
            }
        }

        private static string GetWeaponPath(int type)
        {
            var modelType = (ModelType)type;
            int groupBase = type / 512 * 512;
            int id = type - groupBase;

            string category = ((ModelType)groupBase).ToString().Replace("ITEM_GROUP_", "").Split('_')[0];

            if (category == "MACE") category = "Mace";

            if (id >= 0)
            {
                return $"Item/{category}{id + 1:D2}.bmd";
            }

            return null;
        }
    }
}