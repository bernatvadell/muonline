using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class GameControl
    {
        public GameControl Parent { get; set; }
        public ControlCollection Controls { get; private set; }
        public bool Ready { get; private set; } = false;

        protected GameControl()
        {
            Controls = new ControlCollection(this);
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

        public virtual Task Load(GraphicsDevice graphicsDevice) => Task.CompletedTask;

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
    }
}
