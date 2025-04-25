using Client.Main.Controls;
using Client.Main.Objects.Worlds.Login;

namespace Client.Main.Worlds
{
    public class LoginWorld : TourWorldControl
    {
        public LoginWorld() : base(worldIndex: 74)
        {
            Camera.Instance.ViewFar = 2500f;
            Camera.Instance.ViewNear = 1;
            Camera.Instance.FOV = 65f;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[37] = typeof(TorchObject);
            MapTileObjects[79] = typeof(StatueTorchObject);
        }
    }
}
