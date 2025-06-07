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
            base.OnClick();
            HandleClick();
        }
        protected abstract void HandleClick();
    }
}
