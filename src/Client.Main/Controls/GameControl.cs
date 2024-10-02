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

        protected GameControl()
        {
            Controls = new ControlCollection(this);
        }

        public virtual async Task Load(GraphicsDevice graphicsDevice)
        {
            var tasks = new Task[Controls.Count];

            for (int i = 0; i < Controls.Count; i++)
                tasks[i] = Controls[i].Load(graphicsDevice);

            await Task.WhenAll(tasks);
        }

        public virtual void Update(GameTime gameTime)
        {
            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Update(gameTime);
        }

        public virtual void Draw(GameTime gameTime)
        {
            for (int i = 0; i < Controls.Count; i++)
                Controls[i].Draw(gameTime);
        }
    }
}
