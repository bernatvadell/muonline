using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class GameControl : IChildItem<GameControl>, IDisposable
    {
        public GraphicsDevice GraphicsDevice { get; private set; }
        public GameControl Parent { get; set; }
        public ChildrenCollection<GameControl> Controls { get; private set; }
        public bool Ready { get; private set; } = false;

        protected GameControl()
        {
            Controls = new ChildrenCollection<GameControl>(this);
        }

        public virtual async Task Initialize(GraphicsDevice graphicsDevice)
        {
            var tasks = new Task[Controls.Count];

            await Load(graphicsDevice);

            for (int i = 0; i < Controls.Count; i++)
                tasks[i] = Controls[i].Initialize(graphicsDevice);

            await Task.WhenAll(tasks);

            Ready = true;
        }

        public virtual Task Load(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
            return Task.CompletedTask;
        }

        public virtual void Update(GameTime gameTime)
        {
            if (!Ready) return;
            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Update(gameTime);
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (!Ready) return;

            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Draw(gameTime);
        }

        public virtual void Dispose()
        {
            var controls = Controls.ToArray();

            for (int i = 0; i < controls.Length; i++)
                controls[i].Dispose();

            Controls.Clear();

            Ready = false;
        }

        public void BringToFront()
        {
            if (!Ready) return;
            if (Parent == null) return;
            if (Parent.Controls[^1] == this) return;
            var parent = Parent;
            Parent.Controls.Remove(this);
            parent.Controls.Add(this);
        }
    }
}
