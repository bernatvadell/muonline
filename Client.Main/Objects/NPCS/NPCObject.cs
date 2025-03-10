
namespace Client.Main.Objects.NPCS
{
    public abstract class NPCObject : WalkerObject
    {
        public NPCObject()
        {
            Interactive = true;
        }
        public override void OnClick()
        {
            Interactive = false;
            HandleClick();       
            Interactive = true;
        }
        protected abstract void HandleClick();
    }
}
